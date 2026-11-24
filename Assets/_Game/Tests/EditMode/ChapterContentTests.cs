using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.Save;
using Starbind.Spells;
using Starbind.World;
using UnityEditor;
using UnityEngine;

namespace Starbind.Tests
{
    /// <summary>Chapters 2-5 content and the pure rules added with them: chapter chain, enemy and boss
    /// data, quest log, beam refraction, rampant overdrive, later-school elements, echo/rebound, gated map
    /// routes (every gate has a reachable solution) and story-stage dialogue.</summary>
    public class ChapterContentTests
    {
        static GameDatabase Db()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>("Assets/_Game/Resources/GameDatabase.asset");
            Assert.IsNotNull(db, "run Starbind/Build All Game Content first");
            return db;
        }

        [Test]
        public void FiveChaptersFormOneUnlockChain()
        {
            var db = Db();
            var chapters = db.chapters.OrderBy(c => c.order).ToList();
            CollectionAssert.AreEqual(new[] { "forest", "mine", "lake", "observatory", "citadel" }, chapters.Select(c => c.id).ToArray());
            Assert.AreEqual("", chapters[0].requiresFlag, "the forest is always open");
            for (int i = 1; i < chapters.Count; i++)
                Assert.AreEqual(chapters[i - 1].victoryFlag, chapters[i].requiresFlag, chapters[i].id + " unlocks after " + chapters[i - 1].id);
            Assert.AreEqual(1, chapters.Count(c => c.finale), "one finale");
            Assert.IsTrue(chapters.Last().finale);
            var scenes = EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path)).ToList();
            foreach (var c in chapters)
            {
                CollectionAssert.Contains(scenes, c.scene, c.id + " scene in build settings");
                var boss = db.Enemy(c.bossId);
                Assert.IsNotNull(boss, c.bossId);
                Assert.IsTrue(boss.isBoss, c.bossId + " is a boss");
                Assert.IsTrue(db.Enemy(c.eliteId).isElite, c.eliteId + " is an elite");
                Assert.That(c.enemyHpScale, Is.GreaterThanOrEqualTo(1f));
                foreach (var z in c.zones)
                {
                    Assert.AreEqual(z.enemies.Length, z.weights.Length, c.id + "/" + z.zone);
                    foreach (var id in z.enemies)
                        Assert.IsNotNull(db.Enemy(id), c.id + "/" + z.zone + ": " + id);
                }
                foreach (var s in c.rewardSpells)
                    Assert.IsTrue(SpellFactory.IsAllowed(db.Spell(s)), c.id + " reward " + s + " whitelisted");
            }
            for (int i = 1; i < chapters.Count; i++)
                Assert.That(chapters[i].enemyHpScale, Is.GreaterThan(chapters[i - 1].enemyHpScale), "difficulty rises chapter by chapter");
        }

        [Test]
        public void EveryEnemyHasArtAndValidReferences()
        {
            var db = Db();
            Assert.AreEqual(32, db.enemies.Count, "6 forest + 26 for chapters 2-5");
            foreach (var e in db.enemies)
            {
                var set = db.Sprites(e.sprite);
                Assert.IsNotNull(set, e.id + " sprite " + e.sprite);
                Assert.AreEqual(e.isBoss ? 6 : 4, set.frames.Length, e.id + (e.isBoss ? ": idle 0-3 + windup 4-5" : ": 4-frame loop"));
                if (!string.IsNullOrEmpty(e.projectileSprite))
                    Assert.IsNotNull(db.Sprites(e.projectileSprite), e.id + " shot " + e.projectileSprite);
                foreach (var s in new[] { e.summonId, e.summonId2 })
                    if (!string.IsNullOrEmpty(s))
                        Assert.IsFalse(db.Enemy(s).isBoss, e.id + " summons " + s);
            }
            foreach (var b in new[] { EnemyBehaviour.GolemBoss, EnemyBehaviour.ShadowBoss, EnemyBehaviour.GuardianBoss, EnemyBehaviour.GrimoireBoss })
                Assert.IsInstanceOf<BossBrainBase>(EnemyBrain.For(b), b.ToString());
            Assert.IsInstanceOf<BossBrainBase>(EnemyBrain.For(EnemyBehaviour.Boss), "mushroom king shares the boss rules");
            foreach (var s in db.spells)
                Assert.IsNotNull(db.Sprite(s.icon), s.id + " icon " + s.icon);
        }

        [Test]
        public void QuestLogFollowsProfileFlags()
        {
            var db = Db();
            var quests = db.quests;
            Assert.AreEqual(quests.Count, quests.Select(q => q.id).Distinct().Count(), "unique ids");
            Assert.AreEqual(quests.Count, quests.Select(q => q.flag).Distinct().Count(), "one flag per quest");
            var known = new HashSet<string>(quests.Select(q => q.flag).Concat(db.chapters.Select(c => c.victoryFlag)));
            foreach (var q in quests)
                Assert.IsTrue(string.IsNullOrEmpty(q.requires) || known.Contains(q.requires), q.id + " requires unknown flag " + q.requires);
            foreach (var c in db.chapters)
                Assert.IsTrue(quests.Any(q => q.flag == c.victoryFlag && q.kind == QuestKind.Main), c.id + " victory is a main quest");

            var p = new ProfileData();
            Assert.AreEqual("M1-01", QuestLog.CurrentMain(quests, p).id);
            Assert.AreEqual(QuestStatus.Locked, QuestLog.Status(quests.Single(q => q.id == "M2-03"), p));
            foreach (var f in new[] { "talked_ella", "configure_first_spell", "defeat_mushroom_king" })
                p.CompleteQuest(f);
            Assert.AreEqual(QuestStatus.Done, QuestLog.Status(quests.Single(q => q.id == "M1-03"), p));
            Assert.AreEqual(QuestStatus.Active, QuestLog.Status(quests.Single(q => q.id == "M2-03"), p), "mine boss quest opens after the forest");
            foreach (var q in quests)
                p.CompleteQuest(q.flag);
            Assert.IsNull(QuestLog.CurrentMain(quests, p), "story complete: no main quest left");
        }

        [Test]
        public void BeamReflectsOffMirrorsAndStopsAtWalls()
        {
            Assert.AreEqual(Vector2Int.right, BeamTracer.Reflect(Vector2Int.up, 0), "'/' turns up into right");
            Assert.AreEqual(Vector2Int.left, BeamTracer.Reflect(Vector2Int.up, 1), "'\\' turns up into left");
            Assert.AreEqual(Vector2Int.down, BeamTracer.Reflect(Vector2Int.right, 1));
            var mirrors = new Dictionary<Vector2Int, int> { [new Vector2Int(0, 5)] = 0, [new Vector2Int(5, 5)] = 1 };
            int At(Vector2Int c) => mirrors.TryGetValue(c, out var k) ? k : -1;
            bool Solid(Vector2Int c) => c.x < -3 || c.x > 8 || c.y < -1 || c.y > 9;
            var path = new List<Vector2Int>();
            Assert.IsTrue(BeamTracer.Trace(Vector2Int.zero, Vector2Int.up, At, Solid, new Vector2Int(5, 1), path));
            Assert.AreEqual(new Vector2Int(5, 1), path[path.Count - 1]);
            mirrors[new Vector2Int(5, 5)] = 0;
            Assert.IsFalse(BeamTracer.Trace(Vector2Int.zero, Vector2Int.up, At, Solid, new Vector2Int(5, 1), path), "wrong mirror sends the beam into the wall");
            Assert.That(path.Count, Is.LessThanOrEqualTo(BeamTracer.MaxSteps));
        }

        [Test]
        public void ObservatoryPuzzleStartsUnsolvedAndHasASolution()
        {
            var map = JsonUtility.FromJson<MapJson>(File.ReadAllText("Assets/_Game/Data/Maps/observatory.json"));
            var beam = JsonUtility.FromJson<BeamFile>(ExtractObject(File.ReadAllText("Assets/_Game/Data/Maps/observatory.json"), "\"kind\": \"beam\""));
            bool Solid(Vector2Int c) => c.x < 0 || c.y < 0 || c.x >= map.width || c.y >= map.height || map.layers.collision[map.height - 1 - c.y][c.x] == '#';
            var kinds = beam.mirrors.ToDictionary(m => new Vector2Int(m.x, m.y), m => m.kind);
            int At(Vector2Int c) => kinds.TryGetValue(c, out var k) ? k : -1;
            var from = new Vector2Int(beam.x, beam.y);
            var dir = new Vector2Int(beam.dirX, beam.dirY);
            var to = new Vector2Int(beam.tx, beam.ty);
            Assert.IsFalse(BeamTracer.Trace(from, dir, At, Solid, to, null), "unsolved at start");
            bool solvable = false;
            var cells = kinds.Keys.ToList();
            for (int mask = 0; mask < 1 << cells.Count; mask++)
            {
                for (int i = 0; i < cells.Count; i++)
                    kinds[cells[i]] = (mask >> i) & 1;
                solvable |= BeamTracer.Trace(from, dir, At, Solid, to, null);
            }
            Assert.IsTrue(solvable, "some mirror setting reaches the receiver");
        }

        [Test]
        public void RampantOverdriveHasCostThresholdAndCooldown()
        {
            var r = new ResonanceMeter();
            Assert.IsFalse(r.CanRampant(0, 100, 100), "needs 50 resonance");
            r.Add(HitSource.Reaction, 0, 0, reaction: true);
            while (r.Value < 60f)
                r.Add(HitSource.Reaction, r.Value, 0, reaction: true);
            float cost = r.RampantCost();
            Assert.That(cost, Is.GreaterThan(0).And.LessThanOrEqualTo(ResonanceMeter.RampantMaxCost));
            Assert.IsFalse(r.CanRampant(10, 30, 100), "never below 30% HP after paying");
            Assert.AreEqual(cost, r.TryRampant(10, 100, 100), 0.001f);
            Assert.IsTrue(r.IsActive(10.1f));
            Assert.AreEqual(0, r.Value);
            for (int i = 0; i < 8; i++)
                r.Add(HitSource.Reaction, 20f, 0, reaction: true);
            Assert.That(r.Value, Is.GreaterThanOrEqualTo(ResonanceMeter.RampantMinResonance));
            Assert.IsFalse(r.CanRampant(20f, 100, 100), "30 s cooldown");
            Assert.IsTrue(r.CanRampant(10 + ResonanceMeter.RampantCooldown + 0.1f, 100, 100), "ready again after the cooldown");
            r.ForceFill();
            Assert.IsFalse(r.CanRampant(60f, 100, 100), "full resonance uses the normal overdrive");
        }

        [Test]
        public void LaterSchoolsMarkAndReactWithinBudget()
        {
            Assert.AreEqual(ReactionType.Wildfire, ReactionResolver.Pair(ElementType.Nature, ElementType.Fire));
            Assert.AreEqual(ReactionType.None, ReactionResolver.Pair(ElementType.Moon, ElementType.Ice));
            Assert.AreEqual(ReactionType.None, ReactionResolver.Pair(ElementType.Star, ElementType.Fire));
            var res = new ReactionResolver();
            var st = new StatusSet();
            var nature = new DamageEvent { TargetEntityId = 7, Element = ElementType.Nature, StatusDuration = 5 };
            Assert.AreEqual(ReactionType.None, res.Resolve(nature, st, 0));
            Assert.IsTrue(st.Has(ElementType.Nature, 1));
            var fire = new DamageEvent { TargetEntityId = 7, Element = ElementType.Fire, StatusDuration = 4 };
            Assert.AreEqual(ReactionType.Wildfire, res.Resolve(fire, st, 1));
            Assert.IsFalse(st.Has(ElementType.Nature, 1), "reaction consumes the poison mark");
            var star = new DamageEvent { TargetEntityId = 8, Element = ElementType.Star, StatusDuration = 0 };
            var st2 = new StatusSet();
            res.Resolve(star, st2, 2);
            Assert.AreEqual(0, st2.Count, "star leaves no mark");
            var moon = new DamageEvent { TargetEntityId = 8, Element = ElementType.Moon, StatusDuration = 4 };
            res.Resolve(moon, st2, 2);
            Assert.IsTrue(st2.Has(ElementType.Moon, 3), "curse mark");
        }

        [Test]
        public void EchoAndReboundComeFromThePagesAndAreCapped()
        {
            var db = Db();
            var echo = SpellFactory.Create(db.Spell("moon_echo"));
            Assert.AreEqual(1, echo.Echo);
            var star = SpellFactory.Create(db.Spell("star_shot"));
            Assert.AreEqual(2, star.Bounce);
            Assert.AreEqual(0, SpellFactory.Create(db.Spell("fire_bolt")).Bounce);
            var beam = SpellFactory.Create(db.Spell("prism_beam"));
            Assert.AreEqual(SpellForm.Beam, beam.Form);
            Assert.IsFalse(SpellFactory.IsAllowed(SpellForm.Beam, ElementType.Fire, SpellModifier.None), "only whitelisted beams");
            Assert.IsFalse(SpellFactory.IsAllowed(SpellForm.Missile, ElementType.Fire, SpellModifier.Echo), "echo only on the moon missile");
            Assert.That(AbilityRunner.MaxEcho, Is.EqualTo(2));
            Assert.That(AbilityRunner.EchoDamage, Is.LessThan(1f));
        }

        [TestCase("mine")]
        [TestCase("lake")]
        [TestCase("observatory")]
        [TestCase("citadel")]
        public void MapIsGatedButCompletable(string id)
        {
            string raw = File.ReadAllText($"Assets/_Game/Data/Maps/{id}.json");
            var map = JsonUtility.FromJson<MapJson>(raw);
            int W = map.width, H = map.height;
            bool Free(int x, int y) => x >= 0 && y >= 0 && x < W && y < H && map.layers.collision[H - 1 - y][x] != '#';
            var blockers = new List<Rect>();
            foreach (var g in map.objects.Where(o => o.kind == "gate"))
                blockers.Add(new Rect(g.x - g.w / 2, g.y - g.h / 2, g.w, g.h));
            foreach (var b in map.objects.Where(o => o.kind == "bridge_stone"))
                blockers.Add(new Rect(b.x - b.gap[0] / 2, b.y - b.gap[1] / 2, b.gap[0], b.gap[1]));
            var arena = map.objects.Single(o => o.kind == "boss_arena");
            var arenaRect = new Rect(arena.trigger[0], arena.trigger[1], arena.trigger[2], arena.trigger[3]);
            var start = new Vector2Int((int)map.spawns[0].x, (int)map.spawns[0].y);

            bool ReachesArena(bool gatesClosed)
            {
                var seen = new HashSet<Vector2Int> { start };
                var q = new Queue<Vector2Int>();
                q.Enqueue(start);
                while (q.Count > 0)
                {
                    var c = q.Dequeue();
                    if (arenaRect.Contains(new Vector2(c.x + 0.5f, c.y + 0.5f)))
                        return true;
                    foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    {
                        var n = c + d;
                        if (!Free(n.x, n.y) || seen.Contains(n))
                            continue;
                        var cell = new Rect(n.x, n.y, 1, 1);
                        if (gatesClosed && blockers.Any(r => r.Overlaps(cell)))
                            continue;
                        seen.Add(n);
                        q.Enqueue(n);
                    }
                }
                return false;
            }

            Assert.That(blockers.Count, Is.GreaterThan(0), id + " has mechanism gates");
            Assert.IsFalse(ReachesArena(true), id + ": mechanisms gate the boss");
            Assert.IsTrue(ReachesArena(false), id + ": the boss is reachable once the gates open");
            Assert.AreEqual(id, map.scene.chapter);
            Assert.AreEqual(6, map.zones.Length, "six graybox rooms");
        }

        [Test]
        public void TalkableStagesUseTheLatestReachedFlag()
        {
            var go = new GameObject("npc");
            go.AddComponent<CircleCollider2D>();
            go.AddComponent<Interactable>();
            var t = go.AddComponent<Talkable>();
            t.Configure("艾拉", new[] { "first" }, new[] { "after" });
            t.ConfigureStages(new[]
            {
                new StageLines { flag = "a", lines = new[] { "stage a" } },
                new StageLines { flag = "b", lines = new[] { "stage b" } },
            }, null);
            var p = new ProfileData();
            Assert.AreEqual("first", t.LinesFor(p)[0]);
            p.victories = 1;
            Assert.AreEqual("after", t.LinesFor(p)[0]);
            p.CompleteQuest("a");
            Assert.AreEqual("stage a", t.LinesFor(p)[0]);
            p.CompleteQuest("b");
            Assert.AreEqual("stage b", t.LinesFor(p)[0]);
            Object.DestroyImmediate(go);
        }

        // ---------------------------------------------------------------- map json shapes
        [System.Serializable] class MapJson { public int width, height; public LayersJson layers; public ObjJson[] objects; public SpawnJson[] spawns; public ZoneJson[] zones; public SceneJson scene; }
        [System.Serializable] class LayersJson { public string[] collision; }
        [System.Serializable] class ObjJson { public string kind; public float x, y, w, h; public float[] gap, trigger; }
        [System.Serializable] class SpawnJson { public string id; public float x, y; }
        [System.Serializable] class ZoneJson { public string id; }
        [System.Serializable] class SceneJson { public string chapter; }
        [System.Serializable] class BeamFile { public int x, y, dirX, dirY, tx, ty; public MirrorJson[] mirrors; }
        [System.Serializable] class MirrorJson { public int x, y, kind; }

        /// <summary>The JSON object (braces) containing <paramref name="marker"/>.</summary>
        static string ExtractObject(string raw, string marker)
        {
            int i = raw.IndexOf(marker, System.StringComparison.Ordinal);
            int start = raw.LastIndexOf('{', i);
            int depth = 0;
            for (int k = start; k < raw.Length; k++)
            {
                if (raw[k] == '{') depth++;
                else if (raw[k] == '}' && --depth == 0)
                    return raw.Substring(start, k - start + 1);
            }
            return "{}";
        }
    }
}
