using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starbind.World
{
    /// <summary>"Where do I go next" for the minimap and world map. Not a quest system: the target is derived
    /// from state the game already has (scene, training dummy hits, run / boss / portal state).
    ///   Village  : training ground until the witch has practised (TrainingHits), then the south road exit;
    ///              once a later chapter is unlocked, the star plate (region selector).
    ///   Workshop : the door back to the village (DoorPortal).
    ///   Chapters : the boss arena → the living boss → the return portal.
    /// Scene lookups are cached and refreshed every <see cref="RefreshInterval"/> seconds.</summary>
    public static class ObjectiveTracker
    {
        public const int TrainingHits = 5;
        public const float RefreshInterval = 0.5f;

        static bool trained;
        static float nextRefresh;
        static Vector2 cachedPos;
        static string cachedLabel;
        static bool cachedHas;
        static Transform cachedFollow;
        static Scene cachedScene;

        /// <summary>Forget training progress and cached targets (tests; a new game session).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void ResetStatics()
        {
            trained = false;
            nextRefresh = 0;
            cachedHas = false;
            cachedFollow = null;
            cachedLabel = null;
        }

        /// <summary>True once the witch has landed <see cref="TrainingHits"/> hits on training dummies (this session).</summary>
        public static bool TrainingDone
        {
            get
            {
                if (trained)
                    return true;
                if (GameSession.Exists && GameSession.Instance.Profile != null && GameSession.Instance.Profile.victories > 0)
                    return trained = true;
                return false;
            }
        }

        public static void MarkTrained() => trained = true;

        /// <summary>Current target in world space. False when the scene has no destination.</summary>
        public static bool TryGet(out Vector2 position, out string label)
        {
            var scene = SceneManager.GetActiveScene();
            if (Time.unscaledTime >= nextRefresh || scene != cachedScene)
            {
                nextRefresh = Time.unscaledTime + RefreshInterval;
                cachedScene = scene;
                cachedFollow = null;
                cachedHas = Resolve(scene.name, out cachedPos, out cachedLabel, out cachedFollow);
            }
            position = cachedFollow != null ? (Vector2)cachedFollow.position : cachedPos;
            label = cachedLabel;
            return cachedHas && (cachedFollow == null || cachedFollow.gameObject.activeInHierarchy);
        }

        static bool Resolve(string scene, out Vector2 pos, out string label, out Transform follow)
        {
            pos = default;
            label = null;
            follow = null;
            var run = RunManager.Current;
            if (run != null)
            {
                if (run.Run != null && run.Run.BossDefeated)
                {
                    var portal = Object.FindAnyObjectByType<ReturnPortal>();
                    if (portal == null)
                        return false;
                    pos = portal.transform.position;
                    label = "光門";
                    return true;
                }
                if (run.Boss != null && run.Boss.Health != null && !run.Boss.Health.IsDead)
                {
                    follow = run.Boss.transform;
                    pos = follow.position;
                    label = run.Chapter != null ? run.Chapter.bossName : "Boss";
                    return true;
                }
                var arena = Object.FindAnyObjectByType<BossArena>();
                if (arena == null)
                    return false;
                pos = arena.Trigger.center;
                label = run.Chapter != null ? run.Chapter.arenaName : "Boss";
                return true;
            }
            if (scene == "Village")
            {
                if (!TrainingDone)
                {
                    int hits = 0;
                    TrainingDummy best = null;
                    foreach (var d in Object.FindObjectsByType<TrainingDummy>())
                    {
                        hits += d.Hits;
                        if (best == null || d.transform.position.y < best.transform.position.y)
                            best = d;
                    }
                    if (hits >= TrainingHits)
                        trained = true;
                    else if (best != null)
                    {
                        pos = best.transform.position;
                        label = "訓練場";
                        return true;
                    }
                }
                if (NextChapter() != null)
                {
                    var plate = Object.FindAnyObjectByType<RegionSelector>();
                    if (plate != null)
                    {
                        pos = plate.transform.position;
                        label = "傳送星盤";
                        return true;
                    }
                }
                return FindExit("MoonveilForest", out pos, out label);
            }
            return FindExit(null, out pos, out label);
        }

        /// <summary>The first unlocked, uncleared chapter after the forest (the star plate's goal), or null.</summary>
        public static Progression.ChapterDefinition NextChapter()
        {
            if (!GameSession.Exists || GameDatabase.Instance == null)
                return null;
            var p = GameSession.Instance.Profile;
            Progression.ChapterDefinition best = null;
            foreach (var c in GameDatabase.Instance.chapters)
                if (c != null && c.order > 1 && !p.HasQuest(c.victoryFlag) && (string.IsNullOrEmpty(c.requiresFlag) || p.HasQuest(c.requiresFlag)))
                    if (best == null || c.order < best.order)
                        best = c;
            return best;
        }

        static bool FindExit(string target, out Vector2 pos, out string label)
        {
            pos = default;
            label = null;
            foreach (var e in Object.FindObjectsByType<AreaExit>())
                if (target == null || e.TargetScene == target)
                {
                    pos = e.Area.center;
                    label = e.TargetScene == "MoonveilForest" ? "南路・往紫幻森林" : e.TargetScene == "Village" ? "出口・星露村" : "出口";
                    return true;
                }
            foreach (var d in Object.FindObjectsByType<DoorPortal>())   // workshop: the door is the only way out
                if (target == null)
                {
                    pos = d.transform.position;
                    label = "門・回星露村";
                    return true;
                }
            return false;
        }
    }
}
