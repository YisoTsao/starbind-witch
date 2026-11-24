using System.Collections;
using System.Linq;
using NUnit.Framework;
using Starbind.CameraRig;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.Save;
using Starbind.Spells;
using Starbind.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Starbind.Tests
{
    /// <summary>Chapters 2-5 in Play mode: each scene runs its chapter, every mechanism opens its gate,
    /// each boss keeps the shared phase rules plus its own twist, the later-school spells hit, victory
    /// unlocks rewards and the next chapter on the star plate, and the finale's ending choice is saved.</summary>
    public class ChapterPlayTests : PlayTestBase
    {
        static readonly (string scene, string id, int w, int h)[] Chapters =
        {
            ("Mine", "mine", 64, 80), ("Lake", "lake", 80, 64), ("Observatory", "observatory", 64, 96), ("Citadel", "citadel", 96, 96),
        };

        static ProfileData Progress(int clearedChapters)
        {
            var p = new ProfileData();
            string[] flags = { "defeat_mushroom_king", "defeat_crystal_golem", "defeat_lake_shadow", "defeat_star_guardian", "defeat_twisted_grimoire" };
            for (int i = 0; i < clearedChapters; i++)
                p.CompleteQuest(flags[i]);
            p.victories = clearedChapters;
            return p;
        }

        IEnumerator LoadQuiet(string scene, int cleared)
        {
            yield return Load(scene, Progress(cleared));
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            player.Vitals.Health.InvulnerableUntil = 9999f;
        }

        static DamageEvent Hit(float amount, ElementType el = ElementType.None, float status = 0) =>
            new DamageEvent { CastId = CombatIds.NewCast(), Amount = amount, Element = el, StatusDuration = status, Source = HitSource.ActiveSpell };

        [UnityTest]
        public IEnumerator EachChapterSceneRunsItsChapter()
        {
            for (int i = 0; i < Chapters.Length; i++)
            {
                var (scene, id, w, h) = Chapters[i];
                yield return Load(scene, Progress(i + 1));
                var run = RunManager.Current;
                Assert.IsNotNull(run, scene + " is a run scene");
                Assert.AreEqual(id, run.Chapter.id);
                Assert.AreEqual(new Rect(0, 0, w, h), CameraFollow2D.Main.Bounds, scene + " size");
                Assert.IsNotNull(Object.FindAnyObjectByType<BossArena>(), scene + " boss arena");
                Assert.AreEqual(run.Chapter.enemyHpScale, EnemySpawner.Instance.HpScale);
                Assert.That(Vector2.Distance(P, SpawnPoint.Find("spawn_from_village").transform.position), Is.LessThan(0.2f));
                var zoneEnemy = run.Chapter.zones[0].enemies[0];
                var e = EnemySpawner.Instance.SpawnAt(zoneEnemy, P + new Vector2(0, 4f));
                Assert.AreEqual(GameDatabase.Instance.Enemy(zoneEnemy).hp * run.Chapter.enemyHpScale, e.Health.Max, 0.01f, "chapter difficulty scale");
                var start = P;
                yield return Hold(Vector2.up, 0.6f);
                Assert.That(P.y - start.y, Is.GreaterThan(0.8f), scene + ": the witch walks north from the entrance");
            }
        }

        [UnityTest]
        public IEnumerator MineCartCrystalsAndLeverOpenTheMine()
        {
            yield return LoadQuiet("Mine", 1);
            var cart = Object.FindAnyObjectByType<MineCart>();
            Assert.IsFalse(PowerGate.IsGateOpen("mine_rubble"));
            cart.GetComponent<Interactable>().Interact();
            yield return WaitUntilOrTimeout(() => cart.Exploded, 5f);
            Assert.IsTrue(cart.Exploded, "the cart rolls into the rubble");
            Assert.IsTrue(PowerGate.IsGateOpen("mine_rubble"), "rubble cleared");

            var crystals = Object.FindObjectsByType<ConductiveCrystal>().Where(c => c.Circuit == "mine_core").ToArray();
            Assert.AreEqual(2, crystals.Length);
            CombatWorld.Instance.Damage(Hit(10, ElementType.Lightning, 2f), crystals[0].GetComponent<Health>());
            Assert.IsTrue(crystals[0].Lit, "a lightning hit lights a crystal from range");
            Assert.IsFalse(PowerGate.IsGateOpen("mine_core"), "one of two");
            crystals[1].GetComponentInChildren<Interactable>().Interact();
            Assert.IsTrue(crystals[1].Lit, "F lights it by hand");
            Assert.IsTrue(PowerGate.IsGateOpen("mine_core"), "circuit complete");

            var lever = Object.FindAnyObjectByType<BridgeSwitch>();
            lever.GetComponent<Interactable>().Interact();
            Assert.IsTrue(lever.Repaired, "chasm bridge repaired");
        }

        [UnityTest]
        public IEnumerator GolemAbsorbsLightningAndOverchargeExposesItsCore()
        {
            yield return LoadQuiet("Mine", 1);
            Object.FindAnyObjectByType<BossArena>().Begin();
            var boss = Enemy.ActiveBoss;
            Assert.AreEqual("crystal_golem", boss.Def.id);
            var brain = (GolemBrain)boss.Brain;
            yield return null;
            float hp = boss.Health.Current;
            CombatWorld.Instance.Damage(Hit(50, ElementType.Lightning, 2f), boss.Health);
            Assert.AreEqual(hp, boss.Health.Current, 0.01f, "lightning deals no damage");
            Assert.AreEqual(GolemBrain.ChargePerLightningHit, brain.Charge, 0.01f, "it charges instead");
            CombatWorld.Instance.Damage(Hit(50, ElementType.Fire, 4f), boss.Health);
            Assert.Less(boss.Health.Current, hp, "fire hurts");
            for (int i = 0; i < 9; i++)
                CombatWorld.Instance.Damage(Hit(1, ElementType.Lightning, 2f), boss.Health);
            yield return WaitUntilOrTimeout(() => brain.Novas > 0, 6f);
            Assert.AreEqual(1, brain.Novas, "full charge releases the overcharge nova");
            yield return WaitUntilOrTimeout(() => brain.Exposed, 4f);
            Assert.IsTrue(brain.Exposed, "the cracked core is exposed after the nova");
            Assert.AreEqual(BossBrainBase.ExposedDamageTaken, boss.Health.DamageTakenMultiplier, 0.001f);
        }

        [UnityTest]
        public IEnumerator LakePylonsRaiseTheLightBridgeAndTheShadowMirrorsTheBuild()
        {
            yield return LoadQuiet("Lake", 2);
            Assert.IsFalse(PowerGate.IsGateOpen("lake_west"));
            foreach (var c in Object.FindObjectsByType<ConductiveCrystal>().Where(c => c.Circuit == "lake_west"))
                c.Light();
            Assert.IsTrue(PowerGate.IsGateOpen("lake_west"), "both west pylons raise the west bridge");
            Assert.IsFalse(PowerGate.IsGateOpen("lake_east"), "the east bridge is independent");

            player.Build.SetActive(0, "fire_bolt");
            Object.FindAnyObjectByType<BossArena>().Begin();
            var brain = (ShadowBrain)Enemy.ActiveBoss.Brain;
            yield return WaitUntilOrTimeout(() => brain.AttacksStarted > 0, 4f);
            Assert.AreEqual(ElementType.Fire, brain.LastMirrored, "the reflection copies the witch's Q element");
        }

        [UnityTest]
        public IEnumerator ObservatoryMirrorsGuideStarlightToTheDoor()
        {
            yield return LoadQuiet("Observatory", 3);
            var puzzle = Object.FindAnyObjectByType<BeamPuzzle>();
            Assert.IsFalse(puzzle.Solved, "unsolved at start");
            Assert.IsFalse(PowerGate.IsGateOpen("obs_door"));
            foreach (var m in Object.FindObjectsByType<BeamMirror>())
                m.GetComponent<Interactable>().Interact();
            yield return null;
            Assert.IsTrue(puzzle.Solved, "flipping both mirrors reaches the receiver");
            Assert.IsTrue(PowerGate.IsGateOpen("obs_door"), "star door open");
        }

        [UnityTest]
        public IEnumerator CitadelTrialsOpenTheHall()
        {
            yield return LoadQuiet("Citadel", 4);
            var seals = Object.FindObjectsByType<TrialSeal>();
            Assert.AreEqual(3, seals.Length);
            CollectionAssert.AreEquivalent(new[] { "自然", "月影", "星痕" }, seals.Select(s => s.School).ToArray());
            foreach (var seal in seals)
            {
                Assert.IsFalse(PowerGate.IsGateOpen("hall"), "hall sealed until every trial is done");
                seal.Begin(null);
                Assert.IsTrue(seal.Started);
                float end = Time.realtimeSinceStartup + 25f;
                while (!seal.Cleared && Time.realtimeSinceStartup < end)
                {
                    if (GameSession.Instance.UI.OpenPanel == "upgrade")
                        GameSession.Instance.UI.ChooseCard(0); // each cleared trial grants a level-up choice
                    foreach (var e in EnemySpawner.Instance.AliveList.ToArray())
                        e.Health.Kill();
                    yield return null;
                }
                Assert.IsTrue(seal.Cleared, seal.School + " trial cleared");
            }
            Assert.IsTrue(PowerGate.IsGateOpen("hall"), "three seals open the central hall");
        }

        [UnityTest]
        public IEnumerator EveryChapterBossHasThreePhasesTelegraphsAndAPortal()
        {
            for (int i = 0; i < Chapters.Length; i++)
            {
                var (scene, id, _, _) = Chapters[i];
                yield return LoadQuiet(scene, i + 1);
                var run = RunManager.Current;
                Object.FindAnyObjectByType<BossArena>().Begin();
                var boss = Enemy.ActiveBoss;
                Assert.AreEqual(run.Chapter.bossId, boss.Def.id, scene);
                var brain = (BossBrainBase)boss.Brain;
                yield return WaitUntilOrTimeout(() => Telegraph.Active > 0, 6f);
                Assert.That(Telegraph.Active, Is.GreaterThan(0), scene + ": attacks are telegraphed");
                var world = CombatWorld.Instance;
                world.Damage(Hit(boss.Health.Max * 0.35f), boss.Health);
                yield return null;
                Assert.AreEqual(2, brain.Phase, scene + " phase 2");
                boss.Health.InvulnerableUntil = 0;
                world.Damage(Hit(boss.Health.Max * 0.35f), boss.Health);
                yield return null;
                Assert.AreEqual(3, brain.Phase, scene + " phase 3");
                boss.Health.InvulnerableUntil = 0;
                world.Damage(Hit(boss.Health.Max), boss.Health);
                yield return null;
                Assert.IsTrue(run.Run.BossDefeated, scene + " boss defeated");
                Assert.IsNotNull(Object.FindAnyObjectByType<ReturnPortal>(), scene + " return portal");
                GameSession.Instance.ResetTransientState();
            }
        }

        [UnityTest]
        public IEnumerator VictoryUnlocksRewardsAndTheStarPlateOffersTheNextChapter()
        {
            yield return LoadQuiet("Mine", 1);
            RunManager.Current.Finish(true);
            var profile = GameSession.Instance.Profile;
            Assert.IsTrue(profile.HasQuest("defeat_crystal_golem"));
            Assert.IsTrue(profile.IsUnlocked("prism_beam") && profile.IsUnlocked("thorn_bloom"), "chapter 2 rewards: beam form and nature");
            Assert.IsTrue(GameSession.Instance.UI.ContinueResults());
            yield return WaitForScene("Village");
            Assert.That(Vector2.Distance(P, SpawnPoint.Find("spawn_from_starplate").transform.position), Is.LessThan(0.1f), "back at the star plate");
            Assert.AreEqual("lake", ObjectiveTracker.NextChapter().id, "the lake is next");

            GameSession.Instance.UI.ToggleQuests();
            Assert.AreEqual("quests", GameSession.Instance.UI.OpenPanel, "quest log");
            GameSession.Instance.UI.ToggleQuests();

            Object.FindAnyObjectByType<RegionSelector>().GetComponent<Interactable>().Interact();
            Assert.AreEqual("regions", GameSession.Instance.UI.OpenPanel);
            Assert.AreEqual(4, GameSession.Instance.UI.OfferedRegions, "cottage door + forest, mine, lake");
            Assert.IsTrue(GameSession.Instance.UI.Choose(3));
            yield return WaitForScene("Lake");
            yield return null;
            Assert.AreEqual("lake", RunManager.Current.Chapter.id);
        }

        [UnityTest]
        public IEnumerator FinaleVictoryAsksForTheEndingAndSavesIt()
        {
            yield return LoadQuiet("Citadel", 4);
            RunManager.Current.Finish(true);
            Assert.IsTrue(GameSession.Instance.UI.ContinueResults());
            Assert.AreEqual("ending", GameSession.Instance.UI.OpenPanel, "the finale asks for the ending");
            Assert.IsTrue(GameSession.Instance.UI.Choose(1));
            var p = GameSession.Instance.Profile;
            Assert.IsTrue(p.HasQuest(EndingChoice.PactFlag) && p.HasQuest("story_complete"));
            Assert.IsFalse(p.HasQuest(EndingChoice.SealFlag));
            Assert.IsTrue(GameSession.Instance.UI.DialogueOpen, "epilogue");
            yield return WaitForScene("Village");
            var restored = new SaveService(TempSaves).Load();
            Assert.IsTrue(restored.HasQuest(EndingChoice.PactFlag), "saved to disk");
            var ella = Object.FindObjectsByType<Talkable>().Single(t => t.Speaker == "艾拉");
            StringAssert.Contains("星痕魔女", ella.LinesFor(p)[0], "Ella reacts to the pact ending");
        }

        [UnityTest]
        public IEnumerator LaterSchoolSpellsHitAsDesigned()
        {
            yield return LoadQuiet("Mine", 1);
            yield return TeleportTo(new Vector2(44f, 37f)); // crystal hall
            foreach (var col in GameObject.Find("World").GetComponentsInChildren<Collider2D>())
                if (!col.isTrigger)
                    col.enabled = false; // decor (altar, crates) would stop missiles: this test is about the spells
            var spawner = EnemySpawner.Instance;
            var world = CombatWorld.Instance;
            player.Vitals.FullRestore();

            // prism beam: one instant line through three enemies
            var line = new[] { 1.6f, 3.0f, 4.4f }.Select(dx => spawner.SpawnAt("rock_golemling", P + new Vector2(dx, 0.45f - 0.65f))).ToArray();
            yield return null;
            player.Build.SetActive(0, "prism_beam");
            Assert.AreEqual(CastResult.Cast, player.Combat.CastSlot(0, P + new Vector2(7f, 0.45f)));
            Assert.That(line.Count(e => e.Health.TotalDamageTaken > 0), Is.EqualTo(3), "the beam pierces the whole line");
            spawner.DespawnAll();

            // moon echo: the cast and its weaker echo both land
            var target = spawner.SpawnAt("lens_turret", P + new Vector2(0, 3f));
            int moonHits = 0;
            target.Health.Damaged += (h, e, r) => { if (r.Applied && e.Element == ElementType.Moon) moonHits++; };
            player.Build.SetActive(0, "moon_echo");
            player.Combat.CastSlot(0, target.Center);
            yield return new WaitForSeconds(1.2f);
            Assert.AreEqual(2, moonHits, "cast + echo");
            Assert.IsTrue(target.Health.Status.Has(ElementType.Moon, Time.time), "cursed");
            spawner.DespawnAll();

            // star shot: one missile rebounds to the neighbours
            var group = new[] { new Vector2(0, 3f), new Vector2(1.6f, 3.6f), new Vector2(-1.6f, 3.6f) }.Select(o => spawner.SpawnAt("lens_turret", P + o)).ToArray();
            yield return null;
            player.Build.SetActive(0, "star_shot");
            player.Combat.CastSlot(0, group[0].Center);
            yield return new WaitForSeconds(1.5f);
            Assert.That(group.Count(e => e.Health.TotalDamageTaken > 0), Is.GreaterThanOrEqualTo(2), "rebounds to a second enemy");
            spawner.DespawnAll();

            // thorn bloom (auto) poisons; fire on poison = wildfire
            var victim = spawner.SpawnAt("lens_turret", P + new Vector2(0, 3f));
            player.Build.Auto.Clear();
            Assert.IsTrue(player.Build.AddAuto("thorn_bloom"));
            yield return WaitUntilOrTimeout(() => victim.Health.Status.Has(ElementType.Nature, Time.time), 6f);
            Assert.IsTrue(victim.Health.Status.Has(ElementType.Nature, Time.time), "auto thorn bloom poisons the nearest enemy");
            player.Build.Auto.Clear();
            int wildfires = 0;
            world.Bus.ReactionTriggered += (r, p, h) => { if (r == ReactionType.Wildfire) wildfires++; };
            world.Damage(Hit(5, ElementType.Fire, 4f), victim.Health);
            Assert.AreEqual(1, wildfires, "fire + poison = wildfire");
        }

        [UnityTest]
        public IEnumerator RampantOverdrivePaysHpToStartEarly()
        {
            yield return LoadQuiet("Mine", 1);
            player.Vitals.Health.InvulnerableUntil = 0;
            var c = player.Combat;
            for (int i = 0; i < 8; i++)
                c.Resonance.Add(HitSource.Reaction, Time.time, 0, reaction: true);
            Assert.IsFalse(c.TryOverdrive(), "not researched: needs full resonance");
            c.RampantUnlocked = true;
            float hp = player.Vitals.Health.Current;
            float cost = c.Resonance.RampantCost();
            Assert.IsTrue(c.TryOverdrive(), "researched: starts early");
            Assert.IsTrue(c.OverdriveActive);
            Assert.AreEqual(hp - cost, player.Vitals.Health.Current, 0.01f, "pays the shown HP cost");
            yield return null;
        }
    }
}
