using System;
using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Items;
using Starbind.Player;
using Starbind.VFX;
using Starbind.World;
using UnityEngine;

namespace Starbind.Progression
{
    /// <summary>One chapter run (GDD §8, §10, §11): starts the seeded run, turns kills into drops/XP,
    /// queues level-up choices, starts the chapter boss, and ends the run on death or victory (results
    /// screen, bank rewards into the profile, first-clear spell rewards, the finale's ending choice, save,
    /// return to the village/workshop). The ChapterDefinition of the scene supplies every name and id.
    /// Does not own combat, spawning or UI internals.</summary>
    public sealed class RunManager : MonoBehaviour
    {
        public const int BaseSeed = 20261121;
        public const float DeathMaterialKeep = 0.5f;

        public static RunManager Current { get; private set; }

        public RunState Run { get; private set; }
        public UpgradeDirector Upgrades { get; private set; }
        public SpawnDirector Director { get; private set; }
        public bool Finished { get; private set; }
        public bool Victory { get; private set; }
        public Enemy Boss { get; private set; }
        public ChapterDefinition Chapter { get; private set; }
        public int SeedOverride { get; set; } = -1;
        public event Action<bool> RunEnded;

        GameRng cardRng;
        GameRng lootRng;
        CombatWorld world;
        PlayerController player;
        bool choosing;
        Action onBossDone;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        void Awake() => Current = this;

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
            if (world != null)
                world.Bus.Killed -= OnKilled;
            Pickup.Collected -= OnCollected;
        }

        void Start()
        {
            var session = GameSession.Instance;
            Chapter = ResolveChapter();
            int seed = SeedOverride >= 0 ? SeedOverride : BaseSeed + session.Profile.runs + (Chapter.order - 1) * 7919;
            Run = new RunState(seed);
            cardRng = new GameRng(seed ^ 0x5A5A);
            lootRng = new GameRng(seed ^ 0x1F1F);
            EnemySpawner.Instance.SetSeed(seed);
            EnemySpawner.Instance.HpScale = Chapter.enemyHpScale;
            Upgrades = new UpgradeDirector(GameDatabase.Instance.upgrades);
            Director = FindAnyObjectByType<SpawnDirector>();
            if (Director != null)
            {
                Director.Chapter = Chapter;
                Director.Running = true;
            }
            world = CombatWorld.Instance;
            world.Bus.Killed += OnKilled;
            world.Bus.ReactionTriggered += (r, p, h) => Run.ReactionsTriggered++;
            Pickup.Collected += OnCollected;
            session.RefillRunPotions();
            session.Profile.runs++;
            session.UI.Notice(Chapter.introNotice);
            Debug.Log($"[RunManager] {Chapter.id} run started, seed {seed}");
        }

        /// <summary>The scene's chapter (SceneConfig.chapterId, else by scene name). A scene without one
        /// (tests building a bare run) falls back to the forest so the old behaviour is kept.</summary>
        static ChapterDefinition ResolveChapter()
        {
            var db = GameDatabase.Instance;
            var cfg = SceneConfig.Current;
            var c = cfg != null && !string.IsNullOrEmpty(cfg.chapterId) ? db.Chapter(cfg.chapterId) : null;
            if (c == null)
                c = db.ChapterForScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            if (c == null)
                c = db.Chapter("forest");
            if (c == null)
            {
                c = ScriptableObject.CreateInstance<ChapterDefinition>();
                c.id = "forest"; c.order = 1; c.bossId = "mushroom_king"; c.bossName = "月蝕菇王"; c.arenaName = "菇王棲地";
                c.eliteId = "spore_elite"; c.victoryFlag = "defeat_mushroom_king"; c.introNotice = "月影森林　Z01 森林入口";
                c.returnSpawn = "spawn_from_forest"; c.title = "月影森林";
            }
            return c;
        }

        public void BindPlayer(PlayerController p)
        {
            player = p;
            p.DeathHandler = _ => Finish(false);
        }

        void Update()
        {
            if (Finished || Run == null)
                return;
            if (!GamePause.IsPaused)
                Run.Elapsed += Time.deltaTime;
            if (player == null)
                BindPlayer(PlayerController.Current);
            // auto slots unlock in-run (GDD §6.1: start small, unlock more as the run grows)
            if (player != null)
                player.Build.Auto.UnlockedSlots = Run.Level >= 9 ? 4 : Run.Level >= 5 ? 3 : 2;
            if (!choosing && Run.PendingLevelUps > 0 && !GameSession.Instance.UI.BlocksGameplay)
                OfferUpgrade();
        }

        // ------------------------------------------------------------------ drops
        void OnKilled(Health h, DamageEvent e)
        {
            if (Finished || !h.TryGetComponent(out Enemy enemy) || enemy.Def == null)
                return;
            var def = enemy.Def;
            Run.Kills++;
            if (def.isElite)
                Run.EliteKills++;
            Vector2 p = h.Center;
            int orbs = Mathf.Clamp(def.xp, 1, 12);
            for (int i = 0; i < orbs; i++)
                Pickup.Drop(PickupKind.Xp, p, Mathf.Max(1, def.xp / orbs), null, Scatter(1.5f));
            if (lootRng.Value() < def.lootChance)
            {
                string mat = !string.IsNullOrEmpty(def.lootMaterial) ? def.lootMaterial
                    : def.id == "spore_shroom" || def.id == "spore_elite" ? Inventory.Spore : def.id == "wisp" ? Inventory.MoonDew : Inventory.StarSand;
                Pickup.Drop(PickupKind.Material, p, def.isElite ? 6 : 1, mat, Scatter(1.2f));
            }
            if (lootRng.Value() < (def.isElite ? 1f : 0.025f))
                Pickup.Drop(PickupKind.HpPotion, p, 1, null, Scatter(1.2f));
            if (def.isElite)
                Pickup.Drop(PickupKind.Material, p, 1, Inventory.CoreShard, Scatter(1f));
            if (def.isBoss)
                OnBossKilled(enemy);
        }

        Vector2 Scatter(float s) => new Vector2(lootRng.Range(-s, s), lootRng.Range(-s, s));

        void OnCollected(Pickup p)
        {
            if (Finished || player == null)
                return;
            var bag = player.Combat.Bag;
            switch (p.Kind)
            {
                case PickupKind.Xp:
                    Run.AddXp(p.Amount);
                    break;
                case PickupKind.HpPotion: bag.Add(Inventory.HpPotion, 1); break;
                case PickupKind.MpPotion: bag.Add(Inventory.MpPotion, 1); break;
                case PickupKind.Page:
                    Run.Pages++;
                    GameSession.Instance.UI.Notice("獲得魔典殘頁");
                    break;
                case PickupKind.Material:
                    if (p.ItemId == Inventory.Spore) Run.Spores += p.Amount;
                    else if (p.ItemId == Inventory.MoonDew) Run.MoonDew += p.Amount;
                    else if (p.ItemId == Inventory.CoreShard) Run.CoreShards += p.Amount;
                    else Run.StarSand += p.Amount;
                    break;
            }
        }

        // ------------------------------------------------------------------ level up
        public void GrantBonusLevelUp() => Run.PendingLevelUps++;

        void OfferUpgrade()
        {
            if (player == null)
                return;
            var cards = Upgrades.Roll(cardRng, player.Build, 3);
            Run.PendingLevelUps--;
            if (cards.Count == 0)
                return;
            choosing = true;
            VfxService.Instance.Play("VFX_levelup", player.Combat.Center, 0, 1.6f);
            Sfx.Play("sfx_levelup", 0.9f);
            GameSession.Instance.UI.ShowUpgrades(cards, Run.Level, picked =>
            {
                Upgrades.Apply(picked, player.Build);
                choosing = false;
            });
        }

        /// <summary>Test/debug hook: picks the card directly without UI.</summary>
        public bool ApplyUpgrade(string id)
        {
            foreach (var u in GameDatabase.Instance.upgrades)
                if (u.id == id)
                    return Upgrades.Apply(u, player.Build);
            return false;
        }

        // ------------------------------------------------------------------ boss
        public Enemy StartBoss(Vector2 at, Action onDone)
        {
            onBossDone = onDone;
            if (Director != null)
                Director.BossActive = true;
            foreach (var e in new List<Enemy>(EnemySpawner.Instance.AliveList))
                if (!e.Def.isBoss && Vector2.Distance(e.Position, at) < 9f)
                    e.Health.Kill();
            Boss = EnemySpawner.Instance.SpawnAt(Chapter.bossId, at);
            if (Boss != null && Starbind.CameraRig.CameraFollow2D.Main != null)
                Starbind.CameraRig.CameraFollow2D.Main.SecondaryFocus = Boss.transform;
            GameSession.Instance.UI.Notice(Chapter.bossName + " 出現！");
            Sfx.Play("sfx_boss_roar", 1f);
            Starbind.CameraRig.CameraFollow2D.Main?.Shake(0.2f, 0.8f);
            return Boss;
        }

        void OnBossKilled(Enemy boss)
        {
            if (Starbind.CameraRig.CameraFollow2D.Main != null)
                Starbind.CameraRig.CameraFollow2D.Main.SecondaryFocus = null;
            Run.BossDefeated = true;
            Run.CoreShards += 3;
            if (Director != null)
            {
                Director.BossActive = false;
                Director.Running = false;
            }
            foreach (var e in new List<Enemy>(EnemySpawner.Instance.AliveList))
                if (e != boss)
                    e.Health.Kill();
            Vector2 p = boss.Position;
            Pickup.Drop(PickupKind.Page, p + new Vector2(0, -1f));
            onBossDone?.Invoke();
            GameSession.Instance.SpawnReturnPortal(p);
            GameSession.Instance.UI.Notice($"擊敗{Chapter.bossName}！魔典殘頁出現，走進光門返回村莊");
        }

        // ------------------------------------------------------------------ end
        public void Finish(bool victory)
        {
            if (Finished)
                return;
            Finished = true;
            Victory = victory;
            if (Director != null)
                Director.Running = false;
            var session = GameSession.Instance;
            var profile = session.Profile;
            float keep = victory ? 1f : DeathMaterialKeep;
            int sand = Mathf.FloorToInt(Run.StarSand * keep);
            int spores = Mathf.FloorToInt(Run.Spores * keep);
            int dew = Mathf.FloorToInt(Run.MoonDew * keep);
            int shards = Mathf.FloorToInt(Run.CoreShards * keep);
            int research = (victory ? 3 : 0) + Run.Level / 3;
            profile.starSand += sand;
            profile.spores += spores;
            profile.moonDew += dew;
            profile.coreShards += shards;
            profile.researchPoints += research;
            profile.pages += Run.Pages;
            var unlocked = new List<string>();
            if (victory)
            {
                bool firstClear = !profile.HasQuest(Chapter.victoryFlag);
                profile.victories++;
                profile.CompleteQuest(Chapter.victoryFlag);
                if (firstClear)
                    foreach (var id in Chapter.rewardSpells)
                        if (!profile.IsUnlocked(id))
                        {
                            profile.Unlock(id);
                            var def = GameDatabase.Instance.Spell(id);
                            unlocked.Add(def != null ? def.displayName : id);
                        }
                if (profile.bestClearTime <= 0 || Run.Elapsed < profile.bestClearTime)
                    profile.bestClearTime = Run.Elapsed;
            }
            else
                profile.deaths++;
            session.Save();
            var lines = new List<string>
            {
                victory ? $"勝利！{Chapter.bossName}已被擊敗" : "露米倒下了……被送回工坊",
                $"時間　{(int)(Run.Elapsed / 60)}:{(int)(Run.Elapsed % 60):00}　　等級　{Run.Level}　　擊殺　{Run.Kills}",
                $"元素反應　{Run.ReactionsTriggered}　　精英　{Run.EliteKills}",
                $"帶回：星砂 {sand}　孢子 {spores}　月露 {dew}　星核碎片 {shards}　書頁 {Run.Pages}",
                $"研究點數 +{research}" + (victory ? "" : "（死亡只保留 50% 材料）"),
            };
            if (unlocked.Count > 0)
                lines.Add("新書頁：" + string.Join("、", unlocked) + (string.IsNullOrEmpty(Chapter.rewardNote) ? "" : "　" + Chapter.rewardNote));
            RunEnded?.Invoke(victory);
            session.UI.ShowResults(victory, lines, () =>
            {
                if (!victory)
                    SceneTransition.Go("Workshop", "spawn_bed");
                else if (Chapter.finale)
                    session.UI.ShowEnding(() => SceneTransition.Go("Village", Chapter.returnSpawn));
                else
                    SceneTransition.Go("Village", Chapter.returnSpawn);
            });
        }
    }
}
