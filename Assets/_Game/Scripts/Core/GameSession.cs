using System;
using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Items;
using Starbind.Player;
using Starbind.Progression;
using Starbind.Save;
using Starbind.UI;
using Starbind.VFX;
using Starbind.World;
using UnityEngine;

namespace Starbind.Core
{
    /// <summary>Persistent services that outlive scene changes (the "Bootstrap" role of
    /// WORLD_STRUCTURE §1): profile + save, the session bag (potions), the UI root, and binding the
    /// player of each loaded scene to the profile loadout and research bonuses. Created on demand.</summary>
    public sealed class GameSession : MonoBehaviour
    {
        public struct ResearchNode
        {
            public string id, name, description;
            public int maxLevel;
            public int[] sandCost;
            public int sporeCost;
            public int shardCost;
            public string unlocksSpell;
            /// <summary>Profile flag needed before the node is listed ("" = always).</summary>
            public string requiresFlag;
            /// <summary>Profile flag a one-level node sets instead of unlocking a spell.</summary>
            public string setsFlag;

            public string CostText(int level)
            {
                var parts = new List<string>();
                if (sandCost != null && level < sandCost.Length) parts.Add($"星砂 {sandCost[level]}");
                if (sporeCost > 0) parts.Add($"孢子 {sporeCost}");
                if (shardCost > 0) parts.Add($"碎片 {shardCost}");
                return parts.Count > 0 ? string.Join(" ", parts) : "免費";
            }
        }

        public static readonly ResearchNode[] ResearchNodes =
        {
            new ResearchNode { id = "hp", name = "星痕護體", description = "最大 HP +10", maxLevel = 3, sandCost = new[] { 8, 16, 28 } },
            new ResearchNode { id = "mp", name = "魔力迴路", description = "MP 回復 +15%", maxLevel = 3, sandCost = new[] { 8, 16, 28 } },
            new ResearchNode { id = "potion", name = "藥劑配方", description = "出發時多帶 1 瓶生命藥水", maxLevel = 2, sandCost = new[] { 6, 14 }, sporeCost = 4 },
            new ResearchNode { id = "chain_spark", name = "書頁：雷弧落擊", description = "解鎖主動法術 雷弧落擊", maxLevel = 1, sandCost = new[] { 10 }, unlocksSpell = "chain_spark" },
            new ResearchNode { id = "split_flame", name = "書頁：分裂火球", description = "解鎖主動法術 分裂火球", maxLevel = 1, sandCost = new[] { 14 }, unlocksSpell = "split_flame" },
            new ResearchNode { id = "frost_orbit", name = "書頁：冰晶環繞", description = "解鎖開局自動法術 冰晶環繞", maxLevel = 1, sandCost = new[] { 12 }, sporeCost = 3, unlocksSpell = "frost_orbit" },
            new ResearchNode { id = "thunder_pulse", name = "書頁：自動雷脈衝", description = "解鎖開局自動法術 自動雷脈衝", maxLevel = 1, sandCost = new[] { 12 }, shardCost = 1, unlocksSpell = "thunder_pulse" },
            new ResearchNode { id = "star_fire", name = "書頁：星火護符", description = "解鎖開局自動法術 星火護符", maxLevel = 1, sandCost = new[] { 10 }, unlocksSpell = "star_fire" },
            new ResearchNode { id = "rampant", name = "禁書：失控超載", description = "共鳴 50 以上即可按 R，以 HP 換取提前超載（冷卻 30 秒）", maxLevel = 1, sandCost = new[] { 24 }, shardCost = 3, requiresFlag = "defeat_star_guardian", setsFlag = "research_rampant" },
        };

        public bool ResearchVisible(ResearchNode n) => string.IsNullOrEmpty(n.requiresFlag) || Profile.HasQuest(n.requiresFlag);

        static GameSession instance;

        /// <summary>Tests point saves at a temp folder so they never touch the player's real save.</summary>
        public static string SaveDirectoryOverride { get; set; }

        public ProfileData Profile { get; private set; }
        public SaveService SaveService { get; private set; }
        public Inventory Bag { get; } = new Inventory();
        public GameUI UI { get; private set; }

        public static bool Exists => instance != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => _ = Instance;

        public static GameSession Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("[GameSession]");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<GameSession>();
                    instance.Init();
                }
                return instance;
            }
        }

        void Init()
        {
            // enemies and the player never push each other; contact damage is distance based
            int player = LayerMask.NameToLayer(StarbindLayers.Player), enemy = LayerMask.NameToLayer(StarbindLayers.Enemy);
            int interact = LayerMask.NameToLayer(StarbindLayers.Interactable), trigger = LayerMask.NameToLayer(StarbindLayers.Trigger);
            if (player >= 0 && enemy >= 0)
                Physics2D.IgnoreLayerCollision(player, enemy, true);
            if (enemy >= 0 && interact >= 0)
                Physics2D.IgnoreLayerCollision(enemy, interact, true);
            if (enemy >= 0 && trigger >= 0)
                Physics2D.IgnoreLayerCollision(enemy, trigger, true);
            string dir = SaveDirectoryOverride ?? Application.persistentDataPath;
            SaveService = new SaveService(dir);
            Profile = SaveService.Load();
            Sfx.Volume = Profile.sfxVolume;
            VfxService.ScreenFlashStrength = Profile.screenFlash;
            var uiGo = new GameObject("UI");
            uiGo.transform.SetParent(transform, false);
            UI = uiGo.AddComponent<GameUI>();
            UI.Build();
            RefillRunPotions();
        }

        /// <summary>Points saving at another folder and starts from <paramref name="fresh"/> (tests use a
        /// temp folder so the real save is never touched).</summary>
        public void RedirectSaves(string directory, ProfileData fresh = null)
        {
            SaveService = new SaveService(directory);
            ResetProfile(fresh ?? new ProfileData());
        }

        /// <summary>Closes UI, clears pauses and dialogue: a clean state for tests and scene resets.</summary>
        public void ResetTransientState()
        {
            UI.CloseModal();
            UI.CloseDialogue();
            UI.ClearNotices();
            GamePause.Clear();
        }

        /// <summary>Replaces the profile (tests, new game).</summary>
        public void ResetProfile(ProfileData p = null)
        {
            Profile = p ?? new ProfileData();
            VfxService.ScreenFlashStrength = Profile.screenFlash;
            RefillRunPotions();
        }

        public void Save()
        {
            try
            {
                SaveService.Save(Profile);
            }
            catch (Exception e)
            {
                Debug.LogError("[GameSession] save failed: " + e.Message);
            }
        }

        public string SceneTitle => SceneConfig.Current != null ? SceneConfig.Current.title : "";

        public string Objective()
        {
            var run = RunManager.Current;
            if (run != null && run.Chapter != null)
            {
                if (run.Run != null && run.Run.BossDefeated) return "走進光門返回村莊";
                if (run.Boss != null) return string.IsNullOrEmpty(run.Chapter.bossObjective) ? "擊敗" + run.Chapter.bossName : run.Chapter.bossObjective;
                return run.Chapter.pushObjective;
            }
            if (SceneConfig.Current != null && !string.IsNullOrEmpty(SceneConfig.Current.objective))
            {
                var next = World.ObjectiveTracker.NextChapter();
                if (next != null)
                    return $"在傳送星盤選擇「{next.title}」出發";
                if (Profile.HasQuest(EndingChoice.SealFlag) || Profile.HasQuest(EndingChoice.PactFlag))
                    return "艾斯特拉恢復平靜。可以自由重返各地區";
                if (Profile.victories > 0)
                    return "研究新書頁，或再次前往森林";
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Village" && World.ObjectiveTracker.TrainingDone)
                    return "訓練完成！從南路前往月影森林";
                return SceneConfig.Current.objective;
            }
            return "";
        }

        public void RefillRunPotions()
        {
            int hp = 2 + ResearchLevel("potion");
            Bag.TryRemove(Inventory.HpPotion, Bag.Count(Inventory.HpPotion));
            Bag.TryRemove(Inventory.MpPotion, Bag.Count(Inventory.MpPotion));
            Bag.Add(Inventory.HpPotion, hp);
            Bag.Add(Inventory.MpPotion, 1);
        }

        public void RestAndSave()
        {
            RefillRunPotions();
            PlayerController.Current?.Vitals.FullRestore();
            Save();
        }

        // ------------------------------------------------------------------ research
        public int ResearchLevel(string id)
        {
            switch (id)
            {
                case "hp": return Profile.researchHp;
                case "mp": return Profile.researchMp;
                case "potion": return Profile.researchPotion;
            }
            foreach (var n in ResearchNodes)
            {
                if (n.id == id && n.unlocksSpell != null)
                    return Profile.IsUnlocked(n.unlocksSpell) ? 1 : 0;
                if (n.id == id && n.setsFlag != null)
                    return Profile.HasQuest(n.setsFlag) ? 1 : 0;
            }
            return 0;
        }

        public bool TryResearch(string id)
        {
            foreach (var n in ResearchNodes)
            {
                if (n.id != id)
                    continue;
                int level = ResearchLevel(id);
                if (level >= n.maxLevel || !ResearchVisible(n))
                    return false;
                int sand = n.sandCost != null && level < n.sandCost.Length ? n.sandCost[level] : 0;
                if (Profile.starSand < sand || Profile.spores < n.sporeCost || Profile.coreShards < n.shardCost)
                    return false;
                Profile.starSand -= sand;
                Profile.spores -= n.sporeCost;
                Profile.coreShards -= n.shardCost;
                switch (id)
                {
                    case "hp": Profile.researchHp++; break;
                    case "mp": Profile.researchMp++; break;
                    case "potion": Profile.researchPotion++; break;
                    default:
                        if (n.setsFlag != null) Profile.CompleteQuest(n.setsFlag);
                        else Profile.Unlock(n.unlocksSpell);
                        break;
                }
                if (Profile.victories > 0)
                    Profile.CompleteQuest("research_after_victory");
                ApplyResearch(PlayerController.Current);
                Save();
                return true;
            }
            return false;
        }

        public void ApplyEquipment(PlayerController p)
        {
            if (p == null)
                return;
            Equipment.Apply(p.Vitals.Stats, Profile.equippedStaff, Profile.equippedCharm);
            p.Combat.BoltReturns = Profile.equippedStaff == "moonring_staff";
            p.Combat.RampantUnlocked = Profile.HasQuest("research_rampant");
            p.Combat.StormArc = Profile.equippedStaff == "storm_staff";
        }

        void ApplyResearch(PlayerController p)
        {
            if (p == null)
                return;
            var st = p.Vitals.Stats;
            st.RemoveSource("research");
            if (Profile.researchHp > 0)
                st.AddModifier("research", StatId.MaxHp, add: 10f * Profile.researchHp);
            if (Profile.researchMp > 0)
                st.AddModifier("research", StatId.MpRegen, mul: 0.15f * Profile.researchMp);
        }

        // ------------------------------------------------------------------ player binding
        public void BindPlayer(PlayerController p)
        {
            p.Combat.Bag = Bag;
            p.Build.LoadFrom(Profile);
            ApplyResearch(p);
            ApplyEquipment(p);
            p.Vitals.FullRestore();
            UI.Hud.Bind(p);
            RunManager.Current?.BindPlayer(p);
        }

        public void ApplyLoadoutToPlayer()
        {
            var p = PlayerController.Current;
            if (p != null)
            {
                p.Build.LoadFrom(Profile);
                ApplyEquipment(p);
            }
        }

        public ReturnPortal SpawnReturnPortal(Vector2 at)
        {
            var go = new GameObject("ReturnPortal") { layer = LayerMask.NameToLayer(StarbindLayers.Interactable) };
            go.transform.position = at;
            var trig = go.AddComponent<CircleCollider2D>();
            trig.isTrigger = true;
            trig.radius = 0.8f;
            go.AddComponent<Interactable>().Configure("return_portal", new Vector2(0, 1.6f));
            var portal = go.AddComponent<ReturnPortal>();
            VfxService.Instance.Play("VFX_portal", at + new Vector2(0, 0.5f), 0, 2f, null, StarbindLayers.SortVfx, 0, true);
            VfxService.Instance.Light(at, new Color(1f, 0.9f, 0.6f), 4f, 1.5f, 2f);
            return portal;
        }
    }
}
