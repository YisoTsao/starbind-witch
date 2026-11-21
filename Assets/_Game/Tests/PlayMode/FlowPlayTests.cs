using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Starbind.Tests
{
    /// <summary>Scene flow and world interactions: village bridge / NPC / workshop round trip, forest
    /// bridge lever and vines, spawn cap with a frame-time measurement, boss phases, victory and death.</summary>
    public class FlowPlayTests : PlayTestBase
    {
        static string Evidence => Path.GetFullPath(Path.Combine(Application.dataPath, "../docs/evidence/final"));

        [UnityTest]
        public IEnumerator VillageIs48x40WithAllAreas()
        {
            yield return Load("Village");
            var follow = CameraRig.CameraFollow2D.Main;
            Assert.AreEqual(new Rect(0, 0, 48, 40), follow.Bounds);
            Assert.AreEqual(4, Object.FindObjectsByType<TrainingDummy>().Length, "training ground dummies");
            Assert.AreEqual(4, Object.FindObjectsByType<Talkable>().Count(t => t.name == "Talk"), "Ella, Kain, Lia, Lorne");
            Assert.IsNotNull(GameObject.Find("Bridge"));
            Assert.IsNotNull(Object.FindAnyObjectByType<AreaExit>());
            Assert.IsNotNull(SpawnPoint.Find("spawn_from_forest"));
        }

        [UnityTest]
        public IEnumerator BridgeCrossesTheRiverButWaterBlocks()
        {
            yield return Load("Village");
            yield return TeleportTo(new Vector2(10.5f, 15.6f));
            yield return Hold(Vector2.right, 1.6f);
            Assert.That(P.x, Is.GreaterThan(16.2f), "walked across the bridge (river x 12-15)");
            yield return TeleportTo(new Vector2(13.5f, 15.7f));
            yield return Hold(Vector2.down, 1.0f);
            Assert.That(P.y, Is.GreaterThan(14.9f), "bridge rail keeps the witch out of the river");
            yield return TeleportTo(new Vector2(10.4f, 13.5f));
            yield return Hold(Vector2.right, 1.2f);
            Assert.That(P.x, Is.LessThan(12.0f), "river blocks next to the bridge");
        }

        [UnityTest]
        public IEnumerator NpcTalkOpensDialogueAndRecordsQuest()
        {
            yield return Load("Village");
            var ella = Object.FindObjectsByType<Talkable>().First(t => t.Speaker == "艾拉");
            ella.GetComponent<Interactable>().Interact();
            yield return null;
            Assert.IsTrue(GameSession.Instance.UI.DialogueOpen);
            Assert.IsTrue(GameSession.Instance.Profile.HasQuest("talked_ella"));
            Assert.IsTrue(GameSession.Instance.UI.BlocksGameplay, "movement input blocked while talking");
        }

        [UnityTest]
        public IEnumerator CottageDoorToWorkshopAndBack()
        {
            yield return Load("Village");
            yield return TeleportTo(new Vector2(24.53f, 36.4f));
            Assert.IsTrue(player.Interactor.TryInteract());
            yield return WaitForScene("Workshop");
            Assert.That(Vector2.Distance(P, SpawnPoint.Find("spawn_from_village").transform.position), Is.LessThan(0.1f));
            yield return TeleportTo(new Vector2(7f, 3.5f));
            Assert.IsTrue(player.Interactor.TryInteract());
            Assert.AreEqual("loadout", GameSession.Instance.UI.OpenPanel, "grimoire lectern");
            GameSession.Instance.UI.CloseModal();
            yield return TeleportTo(new Vector2(2.4f, 3.7f));
            Assert.IsTrue(player.Interactor.TryInteract());
            Assert.AreEqual("research", GameSession.Instance.UI.OpenPanel, "research desk");
            GameSession.Instance.UI.CloseModal();
            yield return TeleportTo(new Vector2(7f, 1.25f));
            Assert.AreEqual("workshop_door", player.Interactor.Current?.InteractionId);
            Assert.IsTrue(player.Interactor.TryInteract());
            yield return WaitForScene("Village");
            Assert.That(Vector2.Distance(P, SpawnPoint.Find("spawn_from_workshop").transform.position), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator WorkshopWallsBlock()
        {
            yield return Load("Workshop");
            yield return TeleportTo(new Vector2(7f, 2.5f));
            yield return Hold(Vector2.down, 1.2f);
            Assert.That(P.y, Is.GreaterThan(0.9f), "bottom wall");
            yield return TeleportTo(new Vector2(8.5f, 6.5f));
            yield return Hold(Vector2.up, 1.2f);
            Assert.That(P.y, Is.LessThan(8.0f), "back wall");
        }

        [UnityTest]
        public IEnumerator ResearchSpendsMaterialsAndUnlocksSpell()
        {
            yield return Load("Workshop");
            var s = GameSession.Instance;
            s.Profile.starSand = 30;
            float hp0 = player.Vitals.Health.Max;
            Assert.IsTrue(s.TryResearch("hp"));
            Assert.AreEqual(22, s.Profile.starSand);
            Assert.AreEqual(hp0 + 10f, player.Vitals.Health.Max, 0.01f);
            Assert.IsTrue(s.TryResearch("chain_spark"));
            Assert.IsTrue(s.Profile.IsUnlocked("chain_spark"));
            Assert.IsFalse(s.TryResearch("thunder_pulse"), "needs a core shard");
            Assert.IsTrue(File.Exists(s.SaveService.Path), "research saved");
        }

        // ------------------------------------------------------------------ forest
        IEnumerator LoadForestQuiet()
        {
            yield return Load("Forest");
            yield return null;
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BrokenBridgeBlocksUntilLeverAndVinesBurn()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 46.2f));
            yield return Hold(Vector2.up, 1.2f);
            Assert.That(P.y, Is.LessThan(48.0f), "broken bridge gap blocks");
            var lever = Object.FindAnyObjectByType<BridgeSwitch>();
            lever.GetComponent<Interactable>().Interact();
            Assert.IsTrue(lever.Repaired);
            yield return Hold(Vector2.up, 1.6f);
            Assert.That(P.y, Is.GreaterThan(51.0f), "crossed the repaired bridge");

            var vine = Object.FindAnyObjectByType<BurnableVine>();
            var h = vine.GetComponent<Health>();
            CombatWorld.Instance.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = 5, Source = HitSource.Staff }, h);
            Assert.IsFalse(vine.Burnt, "staff does not burn vines");
            CombatWorld.Instance.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = 5, Element = ElementType.Fire, StatusDuration = 2, Source = HitSource.ActiveSpell }, h);
            Assert.IsTrue(vine.Burnt, "fire burns vines");
        }

        [UnityTest]
        public IEnumerator AltarStartsAWaveAndClearingRewards()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 36.5f));
            var altar = Object.FindAnyObjectByType<StarAltar>();
            altar.GetComponent<Interactable>().Interact();
            Assert.IsTrue(altar.Used);
            var director = RunManager.Current.Director;
            yield return WaitUntilOrTimeout(() => director.WaveRemainingToSpawn == 0, 6f);
            Assert.That(EnemySpawner.Instance.Alive, Is.GreaterThan(15), "wave spawned");
            foreach (var e in EnemySpawner.Instance.AliveList.ToList())
                e.Health.Kill();
            yield return WaitUntilOrTimeout(() => altar.Cleared, 2f);
            Assert.IsTrue(altar.Cleared);
            Assert.That(RunManager.Current.Run.PendingLevelUps + (GameSession.Instance.UI.OpenPanel == "upgrade" ? 1 : 0), Is.GreaterThanOrEqualTo(1), "bonus upgrade");
            Assert.IsTrue(RunManager.Current.Director.EliteSpawned, "elite appears after the wave");
        }

        [UnityTest]
        public IEnumerator SpawnCapHoldsAtSixtyAndFrameTimeIsMeasured()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 39f));
            var spawner = EnemySpawner.Instance;
            string[] mix = { "slime", "spore_shroom", "wisp", "slime", "root_guard" };
            for (int i = 0; i < 80; i++)
                spawner.SpawnAt(mix[i % mix.Length], spawner.RandomAround(P, 7f));
            Assert.AreEqual(EnemySpawner.MaxAlive, spawner.Alive);
            Assert.AreEqual(20, spawner.RejectedByCap);
            // auto spells + orbit on, so the measurement includes combat
            player.Build.AddAuto("frost_orbit");
            int startAlive = spawner.Alive;
            var frames = new List<float>();
            float end = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < end)
            {
                frames.Add(Time.unscaledDeltaTime * 1000f);
                yield return null;
            }
            frames.Sort();
            float avg = frames.Average(), p95 = frames[(int)(frames.Count * 0.95f)], max = frames[frames.Count - 1];
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Path.Combine(Evidence, "perf_60_enemies.json"),
                $"{{\"enemies_at_start\":{startAlive},\"enemies_at_end\":{spawner.Alive},\"frames\":{frames.Count},\"avg_ms\":{avg:0.00},\"p95_ms\":{p95:0.00},\"max_ms\":{max:0.00}," +
                $"\"vfx_live\":{VFX.VfxService.Instance.LiveFlipbooks},\"note\":\"batch-mode editor PlayMode on this Mac, Pixel Perfect 480x270; not a player build\"}}");
            Debug.Log($"[Perf] 60 enemies: avg {avg:0.00} ms, p95 {p95:0.00} ms, max {max:0.00} ms over {frames.Count} frames");
            Assert.That(spawner.Alive, Is.LessThanOrEqualTo(EnemySpawner.MaxAlive));
        }

        [UnityTest]
        public IEnumerator BossHasThreePhasesTelegraphsAndVictoryReturnsToVillage()
        {
            yield return LoadForestQuiet();
            var h = player.Vitals.Health;
            h.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 55.6f));
            yield return WaitUntilOrTimeout(() => Enemy.ActiveBoss != null, 2f);
            var boss = Enemy.ActiveBoss;
            Assert.IsNotNull(boss, "entering the arena starts the boss");
            var brain = (BossBrain)boss.Brain;
            Assert.AreEqual(1, brain.Phase);
            yield return WaitUntilOrTimeout(() => Telegraph.Active > 0, 5f);
            Assert.That(Telegraph.Active, Is.GreaterThan(0), "attacks are telegraphed");
            var world = CombatWorld.Instance;
            DamageEvent Big(float amount) => new DamageEvent { CastId = CombatIds.NewCast(), Amount = amount, Source = HitSource.ActiveSpell };
            world.Damage(Big(boss.Health.Max * 0.35f), boss.Health);
            yield return null;
            Assert.AreEqual(2, brain.Phase);
            boss.Health.InvulnerableUntil = 0;
            world.Damage(Big(boss.Health.Max * 0.35f), boss.Health);
            yield return null;
            Assert.AreEqual(3, brain.Phase);
            boss.Health.InvulnerableUntil = 0;
            world.Damage(Big(boss.Health.Max), boss.Health);
            yield return null;
            Assert.IsTrue(RunManager.Current.Run.BossDefeated);
            Assert.IsNotNull(Object.FindAnyObjectByType<ReturnPortal>(), "return portal");
            Object.FindAnyObjectByType<ReturnPortal>().GetComponent<Interactable>().Interact();
            Assert.AreEqual("results", GameSession.Instance.UI.OpenPanel);
            Assert.AreEqual(1, GameSession.Instance.Profile.victories);
            Assert.That(GameSession.Instance.Profile.coreShards, Is.GreaterThanOrEqualTo(3));
            Assert.IsTrue(GameSession.Instance.UI.ContinueResults());
            yield return WaitForScene("Village");
            Assert.That(Vector2.Distance(P, SpawnPoint.Find("spawn_from_forest").transform.position), Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator DeathShowsResultsKeepsHalfMaterialsAndReturnsToWorkshop()
        {
            yield return LoadForestQuiet();
            var run = RunManager.Current;
            run.Run.StarSand = 10;
            player.Vitals.Health.Kill();
            yield return null;
            Assert.AreEqual("results", GameSession.Instance.UI.OpenPanel);
            Assert.AreEqual(5, GameSession.Instance.Profile.starSand, "death keeps 50% materials");
            Assert.AreEqual(1, GameSession.Instance.Profile.deaths);
            Assert.IsTrue(GameSession.Instance.UI.ContinueResults());
            yield return WaitForScene("Workshop");
            Assert.IsFalse(player.Vitals.Health.IsDead, "revived in the workshop");
        }

        [UnityTest]
        public IEnumerator AmbientSpawnsRespectDistanceAndWalkableCells()
        {
            yield return Load("Forest");
            var director = RunManager.Current.Director;
            player.Vitals.Health.InvulnerableUntil = 9999f;
            yield return TeleportTo(new Vector2(32f, 10f));
            director.Elapsed = 300f; // mid-run pacing
            var spawner = EnemySpawner.Instance;
            var bad = new List<string>();
            spawner.Spawned += e =>
            {
                if (e.Def.isElite) return;
                float d = Vector2.Distance(e.Position, P);
                if (d < EnemySpawner.MinSpawnDistance - 0.01f && !director.WaveActive) bad.Add("too close " + d);
                if (!spawner.Grid.IsSpawnable(e.Position) && !director.WaveActive) bad.Add("not spawnable " + e.Position);
            };
            yield return new WaitForSeconds(5f);
            Assert.That(spawner.TotalSpawned, Is.GreaterThan(3), "ambient spawns happened");
            CollectionAssert.IsEmpty(bad);
        }
    }
}
