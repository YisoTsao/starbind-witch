using System.Linq;
using NUnit.Framework;
using Starbind.Editor.WorldStudio;
using Starbind.World.Studio;
using Starbind.World.Tiles;
using UnityEditor;
using UnityEngine;

namespace Starbind.Tests
{
    /// <summary>Witch World Studio (docs/WORLD_GENERATOR_SPEC.md §8): determinism, reachability, bridges,
    /// cliff stairs, spawn layer and the exported JSON contract. The synthetic blueprint tests need no
    /// assets; the Moonveil tests use the preset assets written by World Studio / Build All.</summary>
    public class WorldStudioTests
    {
        static RegionBlueprint Synthetic(int seed = 7)
        {
            var bp = ScriptableObject.CreateInstance<RegionBlueprint>();
            var clearing = ScriptableObject.CreateInstance<PoiDefinition>();
            clearing.kind = PoiKind.Gathering;
            clearing.displayName = "A";
            clearing.clearing = new Vector2(5, 4);
            var arena = ScriptableObject.CreateInstance<PoiDefinition>();
            arena.kind = PoiKind.BossArena;
            arena.displayName = "B";
            arena.clearing = new Vector2(6, 4);
            arena.safe = true;
            arena.objects = new[] { new PoiObject { kind = "boss_arena", offset = Vector2.zero } };
            bp.id = "test";
            bp.width = 60;
            bp.height = 50;
            bp.seed = seed;
            bp.entrySpawn = new Vector2(10.5f, 10.5f);
            bp.entrySafeRadius = 4f;
            bp.pois = new[]
            {
                new PoiPlacement { key = "a", poi = clearing, position = new Vector2(10, 10) },
                new PoiPlacement { key = "b", poi = arena, position = new Vector2(45, 38) },
            };
            // horizontal road crossing a north-south river, then north through cliff stairs at x = 45
            bp.roads = new[] { new RoadSpec { from = "a", to = "b", kind = RoadKind.Main, via = new[] { new Vector2(30, 12), new Vector2(45, 14), new Vector2(45, 28) }, meander = 0 } };
            bp.water = new[] { new WaterSpec { kind = WaterKind.River, points = new[] { new Vector2(22, 49), new Vector2(22, 1) }, size = new Vector2(2.5f, 0) } };
            bp.cliffs = new[] { new CliffSpec { line = new[] { new Vector2(30, 24), new Vector2(59.5f, 24) }, stairs = new[] { new Vector2(45, 5) }, gateStairs = true } };
            return bp;
        }

        [Test]
        public void SameSeedSameMap()
        {
            var a = WorldGenerator.Generate(Synthetic(), 11);
            var b = WorldGenerator.Generate(Synthetic(), 11);
            CollectionAssert.AreEqual(a.Blocked, b.Blocked);
            CollectionAssert.AreEqual(a.Surface, b.Surface);
            CollectionAssert.AreEqual(a.Water, b.Water);
            CollectionAssert.AreEqual(a.Detail, b.Detail);
            Assert.AreEqual(a.Props.Count, b.Props.Count);
        }

        [Test]
        public void DifferentSeedChangesShapes()
        {
            var a = WorldGenerator.Generate(Synthetic(), 11);
            var b = WorldGenerator.Generate(Synthetic(), 12);
            Assert.That(a.Open.Where((v, i) => v != b.Open[i]).Count(), Is.GreaterThan(10), "organic edges depend on the seed");
        }

        [Test]
        public void SyntheticRegionIsConnectedWithBridgeStairsAndGate()
        {
            var d = WorldGenerator.Generate(Synthetic(), 3);
            var v = d.Validate();
            Assert.IsTrue(v.Ok, v.ToString());
            Assert.That(d.Bridge.Count(b => b != 0), Is.GreaterThan(0), "road over the river becomes a bridge");
            Assert.That(d.Objects.Count(o => o.kind == "bridge"), Is.EqualTo(1));
            Assert.That(d.Cliff.Count(c => c == CliffCell.Face), Is.GreaterThan(20));
            var arena = d.Objects.Single(o => o.kind == "boss_arena");
            Assert.AreEqual(1, arena.gates.Count, "gate on the stairs");
            // every walkable cell is reachable from the entry, every spawnable cell is walkable
            for (int i = 0; i < d.Spawn.Length; i++)
                if (d.Spawn[i] == SpawnCell.Spawnable)
                    Assert.AreEqual(0, d.Blocked[i]);
        }

        [Test]
        public void SafePoiAndEntryNeverSpawn()
        {
            var d = WorldGenerator.Generate(Synthetic(), 5);
            var e = WorldMapData.Cell(new Vector2(10.5f, 10.5f));
            Assert.AreNotEqual(SpawnCell.Spawnable, d.Spawn[d.Index(e.x, e.y)]);
            var b = WorldMapData.Cell(new Vector2(45, 38));
            Assert.AreNotEqual(SpawnCell.Spawnable, d.Spawn[d.Index(b.x, b.y)], "boss arena is safe");
        }

        [Test]
        public void PeriodicTileCoversTextureInOrder()
        {
            // cell (0,3) is the top-left of a 4x4 block (texture row 0 = top), cell (3,0) the bottom-right
            Assert.AreEqual(0, PeriodicTile.Index(new Vector3Int(0, 3, 0), 4, 4));
            Assert.AreEqual(15, PeriodicTile.Index(new Vector3Int(3, 0, 0), 4, 4));
            Assert.AreEqual(PeriodicTile.Index(new Vector3Int(1, 1, 0), 4, 4), PeriodicTile.Index(new Vector3Int(-3, -3, 0), 4, 4));
        }

        [Test]
        public void NorthSouthRoadGetsAVerticalBridgeAndGatesKeepEveryPoiReachable()
        {
            var bp = Synthetic();
            // second river east-west across the south, crossed by a north-south road; a vine gate on that road
            bp.water = bp.water.Concat(new[] { new WaterSpec { kind = WaterKind.River, points = new[] { new Vector2(1, 5), new Vector2(21, 5) }, size = new Vector2(3.4f, 0) } }).ToArray();
            var south = ScriptableObject.CreateInstance<PoiDefinition>();
            south.kind = PoiKind.Camp;
            south.clearing = new Vector2(3, 2);
            bp.pois = bp.pois.Concat(new[] { new PoiPlacement { key = "s", poi = south, position = new Vector2(10, 2.5f) } }).ToArray();
            bp.roads = bp.roads.Concat(new[] { new RoadSpec { from = "a", to = "s", kind = RoadKind.Side, meander = 0, vineGateAt = 0.15f } }).ToArray();
            var d = WorldGenerator.Generate(bp, 9);
            var bridges = d.Objects.Where(o => o.kind == "bridge").ToList();
            Assert.That(bridges.Any(b => b.sprite == "PROP_mv_bridge_vertical"), "north-south crossing uses the vertical bridge");
            Assert.AreEqual(1, d.Objects.Count(o => o.kind == "vine"), "vine gate placed");
            // the only way to the south camp is through the vine: validation must reject that
            var v = d.Validate();
            Assert.That(v.errors.Any(e => e.Contains("only reachable through an optional gate")), v.ToString());
        }

        [Test]
        public void BrokenBridgeHasLeverOnTheApproachSide()
        {
            var bp = Synthetic();
            bp.roads[0].brokenBridge = true;
            var d = WorldGenerator.Generate(bp, 4);
            var b = d.Objects.Single(o => o.kind == "bridge_stone");
            Assert.AreEqual("PROP_mv_bridge_broken", b.sprite);
            Assert.AreEqual("PROP_mv_bridge", b.openSprite);
            Assert.That(b.lever.x, Is.LessThan(b.rect.xMin), "lever on the west (road start) side");
            var lc = WorldMapData.Cell(b.lever);
            Assert.IsTrue(d.Walkable(lc.x, lc.y), "lever stands on walkable ground");
            Assert.That(d.Validate().errors.Any(e => e.Contains("optional gate")), "the boss lies beyond the only bridge");
        }

        [Test]
        public void MoonveilCarriesTheChapterOnePuzzles()
        {
            var bp = Moonveil();
            var d = WorldGenerator.Generate(bp);
            Assert.AreEqual(1, d.Objects.Count(o => o.kind == "bridge_stone"), "lever bridge over the lake outflow");
            Assert.AreEqual(1, d.Objects.Count(o => o.kind == "vine"), "burnable vine shortcut");
            Assert.That(d.Cliff.Count(c => c == CliffCell.Stairs), Is.GreaterThanOrEqualTo(8), "stairs cut into the cliff");
            Assert.That(d.Props.Any(p => p.ground && p.sprite == "PROP_mv_magic_circle"), "summoning circle in the boss clearing");
            Assert.IsTrue(d.Validate().Ok, d.Validate().ToString());
        }

        static RegionBlueprint Moonveil()
        {
            var bp = AssetDatabase.LoadAssetAtPath<RegionBlueprint>(MoonveilPreset.BlueprintPath);
            if (bp == null)
                Assert.Inconclusive("Moonveil preset not created yet (Tools/Witch World/World Studio)");
            return bp;
        }

        [Test]
        public void MoonveilValidatesAndKeepsTheOverviewTopology()
        {
            var bp = Moonveil();
            var d = WorldGenerator.Generate(bp);
            var v = d.Validate();
            Assert.IsTrue(v.Ok, v.ToString());
            Assert.AreEqual(bp.pois.Length, v.reachablePois, "every POI reachable");
            foreach (var kind in new[] { PoiKind.Entrance, PoiKind.Altar, PoiKind.Lake, PoiKind.Camp, PoiKind.Gathering, PoiKind.BossArena, PoiKind.CrystalGrove, PoiKind.MushroomGrove, PoiKind.AncientTree })
                Assert.IsTrue(d.Pois.Any(p => p.kind == kind), kind + " present (references/01)");
            Assert.That(d.Objects.Count(o => o.kind == "bridge"), Is.GreaterThanOrEqualTo(2), "lake outflow and waterfall river bridges");
            Assert.That(d.Cliff.Count(c => c == CliffCell.Waterfall), Is.GreaterThan(0), "waterfall where the river leaves the plateau");
            Assert.AreEqual(1, d.Objects.Count(o => o.kind == "boss_arena"));
            Assert.That(d.Objects.Count(o => o.kind == "chest"), Is.GreaterThanOrEqualTo(3));
            Assert.That(v.spawnable, Is.GreaterThan(v.walkable / 3), "enough room for waves");
            Assert.That(d.Props.Count, Is.InRange(600, 4000), "dense but bounded prop count");
        }

        [Test]
        public void MoonveilBossOnlyReachableByTheStairs()
        {
            var bp = Moonveil();
            var d = WorldGenerator.Generate(bp);
            var gate = d.Objects.Single(o => o.kind == "boss_arena").gates.Single();
            // block the gate row and the boss clearing must become unreachable from the entry
            for (int x = Mathf.FloorToInt(gate.xMin); x < Mathf.CeilToInt(gate.xMax); x++)
                for (int y = Mathf.FloorToInt(gate.yMin); y < Mathf.CeilToInt(gate.yMax); y++)
                    d.Blocked[d.Index(x, y)] = 1;
            var dist = d.DistanceFrom(WorldMapData.Cell(bp.entrySpawn));
            var boss = WorldMapData.Cell(bp.pois.Single(p => p.key == "boss").position);
            Assert.AreEqual(-1, dist[d.Index(boss.x, boss.y)], "no back door onto the boss plateau");
        }

        [Test]
        public void MoonveilJsonMatchesTheSceneBuilderContract()
        {
            var bp = Moonveil();
            var d = WorldGenerator.Generate(bp);
            string json = WorldMapExporter.ToJson(d, bp);
            var probe = JsonUtility.FromJson<Probe>(json);
            Assert.AreEqual(bp.id, probe.id);
            Assert.AreEqual(bp.width, probe.width);
            Assert.AreEqual(bp.height, probe.layers.collision.Length);
            Assert.IsTrue(probe.layers.collision.All(r => r.Length == bp.width));
            Assert.AreEqual(d.Props.Count, probe.props.Length);
            Assert.That(probe.props.Count(p => p.light != null && p.light.Length == 7), Is.InRange(8, WorldMapExporter.MaxLights));
            StringAssert.Contains("\"gates\": [[", json);
        }

        [System.Serializable] class Probe { public string id; public int width; public ProbeLayers layers; public ProbeProp[] props; }
        [System.Serializable] class ProbeLayers { public string[] collision; }
        [System.Serializable] class ProbeProp { public string sprite; public float[] light; }
    }
}
