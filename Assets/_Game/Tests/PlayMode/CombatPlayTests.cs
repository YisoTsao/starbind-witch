using System.Collections;
using System.Linq;
using NUnit.Framework;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.Spells;
using Starbind.World;
using UnityEngine;
using UnityEngine.TestTools;

namespace Starbind.Tests
{
    /// <summary>Combat in real scenes: staff combo, charged bolt, spells and reactions on the village
    /// training dummies; enemies, auto spells, reactions, overdrive and level-up in the forest.</summary>
    public class CombatPlayTests : PlayTestBase
    {
        static readonly Vector2 DummyFront = new Vector2(37.0f, 10.85f); // in front of the dummy at (37, 11.6)
        static readonly Vector2 DummySide = new Vector2(33.4f, 11.75f);  // west of it, clear line of fire
        static readonly Vector2 DummyCenter = new Vector2(37.0f, 12.2f);

        TrainingDummy Dummy => Object.FindObjectsByType<TrainingDummy>().OrderBy(d => Vector2.Distance(d.transform.position, new Vector2(37f, 11.6f))).First();

        [UnityTest]
        public IEnumerator StaffComboDealsTenTwelveEighteen()
        {
            yield return Load("Village");
            player.Build.Auto.Enabled = false; // auto spells would also hit the dummy
            yield return TeleportTo(DummyFront);
            var dummy = Dummy;
            var combat = player.Combat;
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, combat.Staff(DummyCenter), "combo step");
                yield return new WaitForSeconds(StaffCombo.StepDuration[i] + 0.05f);
            }
            Assert.AreEqual(3, dummy.Hits);
            Assert.AreEqual(40f, dummy.TotalDamage, 0.01f, "10 + 12 + 18");
            Assert.That(combat.Resonance.Value, Is.EqualTo(2f), "finisher hit gives +2 resonance");
        }

        [UnityTest]
        public IEnumerator ChargedBoltHitsForFullChargeDamage()
        {
            yield return Load("Village");
            player.Build.Auto.Enabled = false;
            yield return TeleportTo(DummySide);
            var dummy = Dummy;
            Assert.IsNotNull(player.Combat.FireBolt(DummyCenter, 1.2f));
            yield return WaitUntilOrTimeout(() => dummy.Hits > 0, 2f);
            Assert.AreEqual(34f, dummy.TotalDamage, 0.01f);
        }

        [UnityTest]
        public IEnumerator FireThenIceOnDummyTriggersSteamBurst()
        {
            yield return Load("Village");
            player.Build.Auto.Enabled = false;
            yield return TeleportTo(DummySide);
            var dummy = Dummy;
            player.Build.SetActive(1, "piercing_ice");
            int reactions = 0;
            CombatWorld.Instance.Bus.ReactionTriggered += (r, p, h) => { if (r == ReactionType.SteamBurst) reactions++; };
            Assert.AreEqual(CastResult.Cast, player.Combat.CastSlot(0, DummyCenter));
            yield return WaitUntilOrTimeout(() => dummy.Hits > 0, 2f);
            Assert.IsTrue(dummy.GetComponent<Health>().Status.Has(ElementType.Fire, Time.time), "burning");
            Assert.AreEqual(CastResult.Cast, player.Combat.CastSlot(1, DummyCenter));
            yield return WaitUntilOrTimeout(() => reactions > 0, 2f);
            Assert.AreEqual(1, reactions);
            Assert.That(dummy.Reactions, Is.GreaterThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator DashMovesAboutTwoPointSixWithInvulnerability()
        {
            yield return Load("Village");
            yield return TeleportTo(new Vector2(26f, 22.5f));
            player.Motor.Face(Vector2.right);
            float x0 = P.x;
            Assert.IsTrue(player.Motor.TryDash());
            player.Vitals.Health.InvulnerableUntil = Time.time + PlayerMotor_DashInvuln;
            Assert.IsTrue(player.Motor.IsDashing);
            yield return new WaitForSeconds(0.4f);
            Assert.That(P.x - x0, Is.EqualTo(2.6f).Within(0.35f));
            Assert.IsFalse(player.Motor.TryDash(), "2.5 s cooldown");
        }

        const float PlayerMotor_DashInvuln = Player.PlayerMotor.DashInvulnerable;

        // ------------------------------------------------------------------ forest
        IEnumerator LoadForestQuiet()
        {
            yield return Load("MoonveilForest");
            var run = RunManager.Current;
            Assert.IsNotNull(run);
            yield return null;
            run.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FireBoltKillsSlimeAndXpIsCollected()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            var slime = EnemySpawner.Instance.SpawnAt("slime", P + new Vector2(0f, 3f));
            Assert.IsNotNull(slime);
            var h = slime.Health;
            for (int i = 0; i < 6 && !h.IsDead; i++)
            {
                player.Combat.CastSlot(0, h.Center);
                yield return new WaitForSeconds(0.7f);
            }
            Assert.IsTrue(h.IsDead, "45 HP slime dies to fire bolts + burn");
            var deathPos = h.Center;
            yield return null;
            yield return TeleportTo(deathPos + new Vector2(0, -1.2f)); // walk onto the drop (magnet radius 2.4)
            var run = RunManager.Current.Run;
            Assert.AreEqual(1, run.Kills);
            yield return WaitUntilOrTimeout(() => run.Xp > 0 || run.Level > 1, 3f);
            Assert.That(run.Xp + (run.Level - 1) * 5, Is.GreaterThan(0),
                $"XP star shard auto-collected (pickups: {string.Join(";", Pickup.Active.Select(x => x.Kind + "@" + (Vector2)x.transform.position + " active " + x.isActiveAndEnabled))}, player {P}, death at {deathPos}, timeScale {Time.timeScale})");
        }

        [UnityTest]
        public IEnumerator AutoSpellNeverFiresWithoutTargetAndFiresWithOne()
        {
            yield return LoadForestQuiet();
            var auto = player.Build.Auto;
            Assert.That(auto.Slots.Count, Is.GreaterThan(0), "homing missile from the profile");
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(0, auto.AutoCasts);
            Assert.That(auto.SkippedNoTarget, Is.GreaterThan(0));
            Assert.AreEqual(0, SpellPools.Instance.Projectiles.ActiveCount, "no missiles fired at nothing");
            player.Vitals.Health.InvulnerableUntil = 9999f;
            EnemySpawner.Instance.SpawnAt("slime", P + new Vector2(-3f, 2f));
            yield return WaitUntilOrTimeout(() => auto.AutoCasts > 0, 4f);
            Assert.That(auto.AutoCasts, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator FireAndWindMakeABoundedFireTornado()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            var c = P + new Vector2(0, 4f);
            for (int i = 0; i < 4; i++)
                EnemySpawner.Instance.SpawnAt("root_guard", c + new Vector2(i * 0.6f - 0.9f, 0));
            yield return null;
            int tornadoes = 0, total = 0;
            CombatWorld.Instance.Bus.ReactionTriggered += (r, p, h) => { total++; if (r == ReactionType.FireTornado) tornadoes++; };
            player.Combat.CastSlot(0, c + new Vector2(0, 0.6f)); // fire bolt -> burning
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(CastResult.Cast, player.Combat.CastSlot(1, c)); // wind pull on burning targets
            yield return WaitUntilOrTimeout(() => tornadoes > 0, 3f);
            Assert.That(tornadoes, Is.GreaterThanOrEqualTo(1));
            Assert.That(CombatWorld.Instance.ActiveTornadoes, Is.LessThanOrEqualTo(CombatWorld.MaxTornadoes));
            yield return new WaitForSeconds(5f);
            Assert.That(total, Is.LessThan(40), "reactions stay bounded (no infinite chain)");
            Assert.AreEqual(0, CombatWorld.Instance.ActiveTornadoes, "tornadoes expire after 3 s");
        }

        [UnityTest]
        public IEnumerator ShatterStormChainsToNearbyEnemies()
        {
            yield return LoadForestQuiet();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            var c = P + new Vector2(0, 3.5f);
            var enemies = Enumerable.Range(0, 5).Select(i => EnemySpawner.Instance.SpawnAt("root_guard", c + new Vector2(i * 0.9f - 1.8f, 0))).ToList();
            int arcs = 0;
            CombatWorld.Instance.Bus.Arc += (a, b) => arcs++;
            var target = enemies[2].Health;
            var world = CombatWorld.Instance;
            world.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = 1, Element = ElementType.Ice, StatusDuration = 3, Source = HitSource.ActiveSpell }, target);
            world.Damage(new DamageEvent { CastId = CombatIds.NewCast(), Amount = 1, Element = ElementType.Lightning, StatusDuration = 3, Source = HitSource.ActiveSpell }, target);
            yield return null;
            Assert.AreEqual(3, arcs, "3 base jumps");
        }

        [UnityTest]
        public IEnumerator OverdriveBoostsCooldownsAndReactionsForEightSeconds()
        {
            yield return LoadForestQuiet();
            var combat = player.Combat;
            Assert.IsFalse(combat.TryOverdrive(), "needs 100 resonance");
            combat.Resonance.ForceFill();
            Assert.IsTrue(combat.TryOverdrive());
            yield return null;
            Assert.IsTrue(combat.OverdriveActive);
            Assert.AreEqual(0.75f, player.GetComponent<AbilityRunner>().CooldownMultiplier, 1e-3);
            Assert.AreEqual(1.35f, CombatWorld.Instance.ReactionDamageMultiplier, 1e-3);
            yield return new WaitForSeconds(8.2f);
            Assert.IsFalse(combat.OverdriveActive);
            Assert.AreEqual(1f, player.GetComponent<AbilityRunner>().CooldownMultiplier, 1e-3);
        }

        [UnityTest]
        public IEnumerator LevelUpOffersThreeCardsAndPausesUntilChosen()
        {
            yield return LoadForestQuiet();
            var run = RunManager.Current;
            run.Run.AddXp(RunState.XpToNext(1));
            yield return null;
            yield return null;
            var ui = GameSession.Instance.UI;
            Assert.AreEqual("upgrade", ui.OpenPanel);
            Assert.AreEqual(3, ui.OfferedCards.Count);
            Assert.AreEqual(0f, Time.timeScale, "paused while choosing");
            var chosen = ui.OfferedCards[0].id;
            Assert.IsTrue(ui.ChooseCard(0));
            Assert.AreEqual(1, run.Upgrades.StackOf(chosen));
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator StormStaffAddsAnArcEveryThirdCast()
        {
            yield return Load("MoonveilForest", new Save.ProfileData { equippedStaff = "storm_staff" });
            RunManager.Current.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            player.Vitals.Health.InvulnerableUntil = 9999f;
            var e = EnemySpawner.Instance.SpawnAt("root_guard", P + new Vector2(0, 3.5f));
            Assert.IsTrue(player.Combat.StormArc);
            for (int i = 0; i < 3; i++)
            {
                player.Combat.CastSlot(0, e.Center);
                yield return new WaitForSeconds(0.7f);
            }
            Assert.AreEqual(3, player.Combat.EffectiveCasts);
            Assert.AreEqual(1, player.Combat.StormArcs);
        }

        [UnityTest]
        public IEnumerator EnemyAttacksHurtThePlayerAndDashDodges()
        {
            yield return LoadForestQuiet();
            var h = player.Vitals.Health;
            var slime = EnemySpawner.Instance.SpawnAt("slime", P + new Vector2(0.4f, 0.1f));
            yield return WaitUntilOrTimeout(() => h.Current < h.Max, 3f);
            Assert.That(h.Current, Is.LessThan(h.Max), "slime contact damage");
            Assert.That(h.InvulnerableUntil, Is.GreaterThan(Time.time - 0.5f), "hurt invulnerability set");
            float hp = h.Current;
            yield return new WaitForSeconds(0.5f);
            player.Motor.TryDash();
            Assert.IsFalse(slime.HitPlayer(10, 1), "attack during dash is dodged");
            Assert.IsTrue(player.Combat.PerfectDodgeReady);
            Assert.AreEqual(hp, h.Current, 0.01f);
        }
    }
}
