using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Items;
using Starbind.Progression;
using Starbind.Save;
using Starbind.Spells;
using UnityEditor;
using UnityEngine;

namespace Starbind.Tests
{
    /// <summary>EditMode tests of the pure combat / progression rules (GDD §14.2).</summary>
    public class CombatLogicTests
    {
        static DamageEvent Hit(ElementType el, int target = 1, bool noReact = false, int depth = 0) => new DamageEvent
        {
            TargetEntityId = target, Element = el, StatusDuration = 3f, CannotTriggerReaction = noReact, ProcDepth = depth, Amount = 10,
        };

        // ------------------------------------------------------------------ status + reactions
        [Test]
        public void StatusKeepsAtMostTwoMarksAndReplacesTheOldest()
        {
            var s = new StatusSet();
            s.Apply(ElementType.Fire, 5, 0f);
            s.Apply(ElementType.Ice, 5, 1f);
            s.Apply(ElementType.Lightning, 5, 2f);
            Assert.AreEqual(2, s.Count);
            Assert.IsFalse(s.Has(ElementType.Fire, 2f), "oldest (fire) replaced");
            Assert.IsTrue(s.Has(ElementType.Ice, 2f));
            Assert.IsTrue(s.Has(ElementType.Lightning, 2f));
            Assert.IsFalse(s.Has(ElementType.Ice, 6.5f), "expired");
        }

        [TestCase(ElementType.Fire, ElementType.Ice, ReactionType.SteamBurst)]
        [TestCase(ElementType.Ice, ElementType.Fire, ReactionType.SteamBurst)]
        [TestCase(ElementType.Fire, ElementType.Lightning, ReactionType.Overload)]
        [TestCase(ElementType.Lightning, ElementType.Ice, ReactionType.ShatterStorm)]
        [TestCase(ElementType.Wind, ElementType.Fire, ReactionType.FireTornado)]
        [TestCase(ElementType.Ice, ElementType.Wind, ReactionType.None)]
        [TestCase(ElementType.Fire, ElementType.Fire, ReactionType.None)]
        public void ReactionPairs(ElementType first, ElementType second, ReactionType expected)
        {
            var r = new ReactionResolver();
            var s = new StatusSet();
            Assert.AreEqual(ReactionType.None, r.Resolve(Hit(first), s, 0f));
            Assert.AreEqual(expected, r.Resolve(Hit(second), s, 0.1f));
        }

        [Test]
        public void ReactionConsumesBothMarks()
        {
            var r = new ReactionResolver();
            var s = new StatusSet();
            r.Resolve(Hit(ElementType.Fire), s, 0f);
            Assert.AreEqual(ReactionType.SteamBurst, r.Resolve(Hit(ElementType.Ice), s, 0.1f));
            Assert.AreEqual(0, s.Count, "fire and ice consumed");
        }

        [Test]
        public void SecondaryDamageNeverTriggersReactions()
        {
            var r = new ReactionResolver();
            var s = new StatusSet();
            r.Resolve(Hit(ElementType.Fire), s, 0f);
            Assert.AreEqual(ReactionType.None, r.Resolve(Hit(ElementType.Ice, 1, noReact: true), s, 0.1f));
            Assert.AreEqual(ReactionType.None, r.Resolve(Hit(ElementType.Lightning, 1, depth: 1), s, 0.2f), "proc depth limit");
            Assert.AreEqual(0, r.TriggeredCount);
        }

        [Test]
        public void SameReactionOnSameTargetHasHalfSecondProtection()
        {
            var r = new ReactionResolver();
            var s = new StatusSet();
            r.Resolve(Hit(ElementType.Fire), s, 0f);
            Assert.AreEqual(ReactionType.SteamBurst, r.Resolve(Hit(ElementType.Ice), s, 0.1f));
            r.Resolve(Hit(ElementType.Fire), s, 0.2f);
            Assert.AreEqual(ReactionType.None, r.Resolve(Hit(ElementType.Ice), s, 0.3f), "inside 0.5 s");
            Assert.AreEqual(1, r.BlockedByProtection);
            s.Clear();
            r.Resolve(Hit(ElementType.Fire), s, 0.7f);
            Assert.AreEqual(ReactionType.SteamBurst, r.Resolve(Hit(ElementType.Ice), s, 0.8f), "after 0.5 s");
        }

        [Test]
        public void GlobalReactionBudgetCapsPerSecond()
        {
            var r = new ReactionResolver { MaxReactionsPerSecond = 5 };
            int fired = 0;
            for (int target = 0; target < 20; target++)
            {
                var s = new StatusSet();
                r.Resolve(Hit(ElementType.Fire, target), s, 0.01f * target);
                if (r.Resolve(Hit(ElementType.Ice, target), s, 0.01f * target + 0.001f) != ReactionType.None)
                    fired++;
            }
            Assert.AreEqual(5, fired);
            Assert.AreEqual(15, r.BlockedByBudget);
        }

        [Test]
        public void HitRegistryResolvesEachCastTargetPairOnce()
        {
            var h = new HitRegistry();
            Assert.IsTrue(h.TryRegister(7, 1, 0f));
            Assert.IsFalse(h.TryRegister(7, 1, 0.1f), "same cast, same target");
            Assert.IsTrue(h.TryRegister(7, 2, 0.1f), "other target");
            Assert.IsTrue(h.TryRegister(8, 1, 0.1f), "other cast");
            Assert.IsTrue(h.TryRegister(9, 1, 0f, 0.6f));
            Assert.IsFalse(h.TryRegister(9, 1, 0.5f, 0.6f), "orbit re-hit interval");
            Assert.IsTrue(h.TryRegister(9, 1, 0.61f, 0.6f));
        }

        // ------------------------------------------------------------------ staff / charge / mana / resonance
        [Test]
        public void StaffComboRunsThreeStepsForFortyDamage()
        {
            var c = new StaffCombo();
            float t = 0f, total = 0f;
            var steps = new List<int>();
            for (int i = 0; i < 3; i++)
            {
                int s = c.Press(t);
                steps.Add(s);
                total += StaffCombo.Damage[s];
                t += StaffCombo.StepDuration[s] + 0.05f;
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, steps);
            Assert.AreEqual(40f, total, "10 + 12 + 18");
            t += StaffCombo.FinisherRecovery + 0.01f;
            Assert.AreEqual(0, c.Press(t), "finisher restarts the chain");
        }

        [Test]
        public void StaffComboResetsAfterWindowAndBuffersEarlyPresses()
        {
            var c = new StaffCombo();
            Assert.AreEqual(0, c.Press(0f));
            Assert.AreEqual(-1, c.Press(0.05f), "buffered while swinging");
            Assert.AreEqual(1, c.Tick(StaffCombo.StepDuration[0] + 0.001f), "buffer fires when step ends");
            Assert.AreEqual(0, c.Press(5f), "window expired -> back to first step");
        }

        [Test]
        public void ChargedBoltScalesFrom18To34()
        {
            Assert.AreEqual(0, ChargeRules.Level(0.1f));
            Assert.IsFalse(ChargeRules.IsCharge(0.1f));
            Assert.AreEqual(18f, ChargeRules.Damage(0.25f));
            Assert.AreEqual(26f, ChargeRules.Damage(0.7f));
            Assert.AreEqual(34f, ChargeRules.Damage(2f));
        }

        [Test]
        public void ManaOnHitIsCappedPerSecond()
        {
            var m = new ManaOnHitLimiter();
            float sum = 0;
            for (int i = 0; i < 20; i++)
                sum += m.Grant(0.01f * i);
            Assert.AreEqual(ManaOnHitLimiter.CapPerSecond, sum);
            Assert.AreEqual(2f, m.Grant(1.5f));
        }

        [Test]
        public void ResonanceRulesAndOverdrive()
        {
            var r = new ResonanceMeter();
            for (int i = 0; i < 10; i++)
                r.Add(HitSource.Enemy, 0.05f * i, kill: true);
            Assert.AreEqual(ResonanceMeter.KillCapPerSecond, r.Value, "kill cap per second");
            for (int i = 0; i < 5; i++)
                r.Add(HitSource.ActiveSpell, 2f, castId: 42);
            Assert.AreEqual(3f + ResonanceMeter.SpellHitCapPerCast, r.Value, "spell hit cap per cast");
            r.Add(HitSource.Reaction, 2f, reaction: true);
            Assert.AreEqual(13f, r.Value);
            Assert.IsFalse(r.TryActivate(2f), "needs 100");
            int ended = 0;
            r.Ended += () => ended++;
            r.ForceFill();
            Assert.IsTrue(r.TryActivate(3f));
            Assert.AreEqual(0f, r.Value);
            Assert.AreEqual(0f, r.Add(HitSource.Reaction, 4f, reaction: true), "no gain while active");
            r.Tick(5f);
            r.Tick(11.01f);
            r.Tick(12f);
            Assert.AreEqual(1, ended, "ends once after 8 s");
        }

        // ------------------------------------------------------------------ spells / stats / items / save / rng
        static SpellDefinition Def(string id, SpellForm f, ElementType e, SpellModifier m, SpellTrigger t = SpellTrigger.Active)
        {
            var d = ScriptableObject.CreateInstance<SpellDefinition>();
            d.id = id; d.form = f; d.element = e; d.modifier = m; d.trigger = t; d.damage = 10; d.count = 1;
            return d;
        }

        [Test]
        public void SpellFactoryOnlyBuildsWhitelistedCombinations()
        {
            Assert.DoesNotThrow(() => SpellFactory.Create(Def("fire_bolt", SpellForm.Missile, ElementType.Fire, SpellModifier.None)));
            Assert.Throws<System.InvalidOperationException>(() => SpellFactory.Create(Def("x", SpellForm.Orbit, ElementType.Fire, SpellModifier.Split)));
            Assert.IsFalse(SpellFactory.IsAllowed(SpellForm.Area, ElementType.Ice, SpellModifier.Pierce));
            var split = SpellFactory.Create(Def("split_flame", SpellForm.Missile, ElementType.Fire, SpellModifier.Split));
            Assert.AreEqual(2, split.Split);
        }

        [Test]
        public void DatabaseContentMatchesDesign()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Game/Resources/GameDatabase.asset");
            Assert.IsNotNull(db, "run Starbind/Build All Game Content first");
            Assert.AreEqual(9, db.spells.Count);
            foreach (var s in db.spells)
                Assert.IsTrue(SpellFactory.IsAllowed(s), s.id + " not whitelisted");
            Assert.AreEqual(15, db.upgrades.Select(u => u.id).Distinct().Count(), "15 distinct upgrade cards");
            var hp = db.enemies.ToDictionary(e => e.id, e => e.hp);
            Assert.AreEqual(45f, hp["slime"]); Assert.AreEqual(55f, hp["spore_shroom"]); Assert.AreEqual(140f, hp["root_guard"]);
            Assert.AreEqual(35f, hp["wisp"]); Assert.AreEqual(350f, hp["spore_elite"]);
            Assert.IsTrue(db.enemies.Single(e => e.id == "mushroom_king").isBoss);
            Assert.IsNotNull(db.Sprites("UI_digits"));
            Assert.AreEqual(18, db.Sprites("CHR_witch_draft").frames.Length, "legacy draft witch: 3 directions x 6 frames");
            var witch = db.Sprites("CHR_witch_ref");
            Assert.AreEqual(36, witch.frames.Length, "witch from the chibi mage sheet: 3 directions x 12 frames");
            Assert.AreEqual(5, witch.walkFrames);
            Assert.AreEqual(36, db.Sprites("NPC_prince_ref").frames.Length, "prince from the chibi knight sheet");
            Assert.AreEqual(4, db.Sprites("NPC_prince_ref").walkFrames);
            Assert.That(db.sprites.Count(x => x.name.StartsWith("NPC_villager_") && x.frames.Length == 36), Is.GreaterThanOrEqualTo(10), "villager variants");
            Assert.That(db.sounds.Count, Is.GreaterThan(20));
            Assert.IsNotNull(db.unlitMaterial);
            Assert.AreEqual("Starbind/SpriteFlash", db.characterMaterial.shader.name);
            Assert.IsTrue(db.characterMaterial.shader.isSupported, "flash shader compiled");
        }

        [Test]
        public void StatModifiersStackAndRemoveCleanly()
        {
            var s = new StatBlock();
            s.SetBase(StatId.MaxHp, 100);
            s.AddModifier("a", StatId.MaxHp, add: 15);
            s.AddModifier("b", StatId.MaxHp, mul: 0.1f);
            Assert.AreEqual(126.5f, s.Get(StatId.MaxHp), 1e-4);
            s.RemoveSource("a");
            Assert.AreEqual(110f, s.Get(StatId.MaxHp), 1e-4);
            s.RemoveSource("b");
            Assert.AreEqual(100f, s.Get(StatId.MaxHp), 1e-4);
        }

        [Test]
        public void EquipmentModifiersApplyAndSwapCleanly()
        {
            var s = new StatBlock();
            s.SetBase(StatId.ManaOnHit, 1f);
            s.SetBase(StatId.BurnDuration, 1f);
            Equipment.Apply(s, "sprout_staff", "ember_charm");
            Assert.AreEqual(2f, s.Get(StatId.ManaOnHit), 1e-4);
            Assert.AreEqual(1.25f, s.Get(StatId.BurnDuration), 1e-4);
            Equipment.Apply(s, "storm_staff", "frost_brooch");
            Assert.AreEqual(1f, s.Get(StatId.ManaOnHit), 1e-4, "old staff removed");
            Assert.AreEqual(1f, s.Get(StatId.BurnDuration), 1e-4, "old charm removed");
            Assert.AreEqual(0.2f, s.Get(StatId.ChilledDamage), 1e-4);
            var m = new ManaOnHitLimiter();
            Assert.AreEqual(4f, m.Grant(0f, 2f), "sprout staff doubles MP on hit");
        }

        [Test]
        public void InventoryNeverGoesNegativeOrOverCap()
        {
            var inv = new Inventory();
            Assert.AreEqual(5, inv.Add(Inventory.HpPotion, 9), "cap 5");
            Assert.AreEqual(0, inv.Add(Inventory.HpPotion, 1));
            Assert.IsFalse(inv.TryRemove(Inventory.HpPotion, 6));
            Assert.AreEqual(5, inv.Count(Inventory.HpPotion));
            Assert.IsTrue(inv.TryRemove(Inventory.HpPotion, 5));
            Assert.IsFalse(inv.TryRemove(Inventory.HpPotion, 1));
            Assert.AreEqual(0, inv.Count(Inventory.HpPotion));
            Assert.AreEqual(0, inv.Add(Inventory.StarSand, -3));
        }

        [Test]
        public void SaveRoundTripsAndSurvivesCorruption()
        {
            var dir = Path.Combine(Path.GetTempPath(), "starbind_save_test_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var svc = new SaveService(dir);
                var p = new ProfileData { starSand = 12, researchHp = 2 };
                p.Unlock("chain_spark");
                svc.Save(p);
                var loaded = svc.Load();
                Assert.AreEqual(12, loaded.starSand);
                Assert.IsTrue(loaded.IsUnlocked("chain_spark"));

                p.starSand = 30;
                svc.Save(p); // first file becomes .bak
                File.WriteAllText(svc.Path, File.ReadAllText(svc.Path).Replace("30", "99")); // tamper -> checksum mismatch
                var recovered = svc.Load();
                Assert.IsTrue(svc.LoadedFromBackup);
                Assert.AreEqual(12, recovered.starSand, "backup used");

                File.WriteAllText(svc.Path, "{garbage");
                File.WriteAllText(svc.Path + ".bak", "");
                var fresh = svc.Load();
                Assert.AreEqual(0, fresh.starSand, "defaults, no crash");
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [Test]
        public void OlderSaveWithoutFlashSettingKeepsTheDefault()
        {
            var dir = Path.Combine(Path.GetTempPath(), "starbind_save_test_" + System.Guid.NewGuid().ToString("N"));
            try
            {
                var svc = new SaveService(dir);
                var p = new ProfileData { starSand = 7, screenFlash = 0.5f };
                svc.Save(p);
                Assert.AreEqual(0.5f, svc.Load().screenFlash);

                // a save written before the setting existed: payload has no screenFlash field
                string payload = JsonUtility.ToJson(new ProfileData { starSand = 7 }).Replace(",\"screenFlash\":1.0", "");
                StringAssert.DoesNotContain("screenFlash", payload);
                File.WriteAllText(svc.Path, JsonUtility.ToJson(new SaveEnvelopeForTest { saveVersion = 1, payload = payload, checksum = SaveService.Checksum(payload) }));
                var old = svc.Load();
                Assert.AreEqual(7, old.starSand);
                Assert.AreEqual(1f, old.screenFlash, "missing field falls back to strong, not 0");
            }
            finally
            {
                if (Directory.Exists(dir))
                    Directory.Delete(dir, true);
            }
        }

        [System.Serializable]
        class SaveEnvelopeForTest
        {
            public int saveVersion;
            public string checksum;
            public string payload;
        }

        [Test]
        public void SameSeedGivesSameSequence()
        {
            var a = new GameRng(123);
            var b = new GameRng(123);
            var c = new GameRng(124);
            var sa = Enumerable.Range(0, 20).Select(_ => a.Range(0, 1000)).ToArray();
            var sb = Enumerable.Range(0, 20).Select(_ => b.Range(0, 1000)).ToArray();
            var sc = Enumerable.Range(0, 20).Select(_ => c.Range(0, 1000)).ToArray();
            CollectionAssert.AreEqual(sa, sb);
            CollectionAssert.AreNotEqual(sa, sc);
            var w = new GameRng(5);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(1, w.Weighted(new[] { 0f, 3f, 0f }));
        }

        // ------------------------------------------------------------------ upgrades / run
        sealed class FakeBuild : IBuildTarget
        {
            public readonly List<RuntimeSpell> spells = new List<RuntimeSpell>();
            public int autoSlots = 2, autos;
            public StatBlock Stats { get; } = new StatBlock();
            public RuntimeSpell FindSpell(string id) => spells.FirstOrDefault(s => s.Id == id);
            public bool CanAddAuto => autos < autoSlots;
            public bool AddAuto(string id)
            {
                if (!CanAddAuto) return false;
                spells.Add(SpellFactory.Create(Def(id, id == "frost_orbit" ? SpellForm.Orbit : id == "thunder_pulse" ? SpellForm.Pulse : SpellForm.Homing,
                    id == "frost_orbit" ? ElementType.Ice : id == "thunder_pulse" ? ElementType.Lightning : id == "star_fire" ? ElementType.Fire : ElementType.None, SpellModifier.None, SpellTrigger.Auto)));
                autos++;
                return true;
            }
            public void RefreshSpell(RuntimeSpell s) { }
            public float healed;
            public void Heal(float amount) => healed += amount;
            public int ChainBonus { get; set; }
        }

        static List<UpgradeDefinition> Cards() => AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Game/Resources/GameDatabase.asset").upgrades;

        [Test]
        public void UpgradeOffersRespectPrerequisitesSlotsAndStacks()
        {
            var cards = Cards();
            var d = new UpgradeDirector(cards);
            var b = new FakeBuild();
            var u01 = cards.Single(c => c.id == "U01");
            var u08 = cards.Single(c => c.id == "U08");
            Assert.IsFalse(d.IsEligible(u01, b), "needs fire_bolt");
            b.spells.Add(SpellFactory.Create(Def("fire_bolt", SpellForm.Missile, ElementType.Fire, SpellModifier.None)));
            Assert.IsTrue(d.IsEligible(u01, b));
            Assert.IsTrue(d.Apply(u01, b));
            Assert.AreEqual(2, b.FindSpell("fire_bolt").Split);
            Assert.IsFalse(d.IsEligible(u01, b), "max 1 stack");
            b.autos = 2;
            Assert.IsFalse(d.IsEligible(u08, b), "no free auto slot");
            b.autos = 0;
            Assert.IsTrue(d.Apply(u08, b));
            Assert.IsNotNull(b.FindSpell("thunder_pulse"));
            Assert.IsFalse(d.IsEligible(u08, b), "already owned (excludes)");
            var u04 = cards.Single(c => c.id == "U04");
            for (int i = 0; i < 10; i++)
                d.Apply(u04, b);
            Assert.AreEqual(UpgradeDirector.MaxChainBonus, b.ChainBonus, "chain jumps capped at 8 targets");
            var u12 = cards.Single(c => c.id == "U12");
            b.Stats.SetBase(StatId.MaxHp, 100);
            d.Apply(u12, b);
            Assert.AreEqual(115f, b.Stats.Get(StatId.MaxHp));
            Assert.AreEqual(15f, b.healed);
        }

        [Test]
        public void UpgradeRollsAreSeededAndDistinct()
        {
            var cards = Cards();
            string Roll(int seed)
            {
                var b = new FakeBuild();
                b.spells.Add(SpellFactory.Create(Def("fire_bolt", SpellForm.Missile, ElementType.Fire, SpellModifier.None)));
                var r = new UpgradeDirector(cards).Roll(new GameRng(seed), b, 3);
                Assert.AreEqual(3, r.Select(c => c.id).Distinct().Count());
                return string.Join(",", r.Select(c => c.id));
            }
            Assert.AreEqual(Roll(99), Roll(99));
        }

        [Test]
        public void RunXpLevelsUp()
        {
            var run = new RunState(1);
            Assert.AreEqual(1, run.AddXp(RunState.XpToNext(1)));
            Assert.AreEqual(2, run.Level);
            Assert.AreEqual(1, run.PendingLevelUps);
            Assert.AreEqual(0, run.AddXp(0));
            Assert.AreEqual(2, run.AddXp(RunState.XpToNext(2) + RunState.XpToNext(3)));
        }

        [Test]
        public void SpawnPacingFollowsDesignBandsAndCap()
        {
            Assert.That(SpawnDirector.TargetAlive(30f), Is.InRange(5, 12));
            Assert.That(SpawnDirector.TargetAlive(180f), Is.InRange(12, 25));
            Assert.That(SpawnDirector.TargetAlive(420f), Is.InRange(35, 50));
            Assert.That(SpawnDirector.TargetAlive(570f), Is.InRange(40, 60));
            for (float t = 0; t < 900; t += 7)
                Assert.That(SpawnDirector.TargetAlive(t), Is.LessThanOrEqualTo(EnemySpawner.MaxAlive));
        }
    }
}
