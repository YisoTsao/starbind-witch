using System.Collections;
using System.Linq;
using NUnit.Framework;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.UI;
using Starbind.World;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Starbind.Tests
{
    /// <summary>Moonveil Forest (Witch World Studio region): the generated scene loads with the run
    /// systems, collision keeps the player out of the forest wall, bridges carry the road over water,
    /// and the boss plateau is gated at its stairs.</summary>
    public class MoonveilPlayTests : PlayTestBase
    {
        const string Scene = "MoonveilForest";

        IEnumerator LoadQuiet()
        {
            yield return Load(Scene);
            var run = RunManager.Current;
            Assert.IsNotNull(run, "run systems in the region");
            run.Director.Running = false;
            EnemySpawner.Instance.DespawnAll();
            player.Vitals.Health.InvulnerableUntil = 9999f;
        }

        static Tilemap Layer(string name) => Object.FindFirstObjectByType<Grid>().transform.Find(name).GetComponent<Tilemap>();

        static int Cells(Tilemap t)
        {
            t.CompressBounds();
            return t.GetTilesBlock(t.cellBounds).Count(x => x != null);
        }

        static bool Blocked(Vector2 p) => Layer("Collision").HasTile(new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y), 0));

        [UnityTest]
        public IEnumerator RegionLoadsWithLayersRunAndMap()
        {
            yield return LoadQuiet();
            Assert.AreEqual("紫幻森林", SceneConfig.Current.title);
            foreach (var layer in new[] { "GroundBase", "GroundDark", "Water", "Path", "Paver", "Detail", "Cliff", "Collision" })
                Assert.That(Cells(Layer(layer)), Is.GreaterThan(0), layer + " painted");
            Assert.AreEqual(160 * 120, Cells(Layer("GroundBase")));
            Assert.IsFalse(Blocked(P), "spawn is walkable");
            Assert.IsNotNull(MapData.Current, "minimap built from the region");
            Assert.That(MapData.Current.Pois.Any(p => p.kind == "altar"), "moon altar on the map");
            Assert.IsNotNull(Object.FindAnyObjectByType<BossArena>());
            Assert.That(Object.FindObjectsByType<SpriteRenderer>().Count(s => s.sprite != null && s.sprite.name.StartsWith("PROP_mv_")), Is.GreaterThan(500));
        }

        [UnityTest]
        public IEnumerator ForestWallStopsThePlayer()
        {
            yield return LoadQuiet();
            yield return TeleportTo(new Vector2(77.5f, 10f));
            yield return Hold(Vector2.left, 3f);
            Assert.IsFalse(Blocked(P), "never inside a collision cell");
            Assert.That(P.x, Is.GreaterThan(62f), "the entrance clearing is closed by forest to the west");
        }

        [UnityTest]
        public IEnumerator BridgeCarriesTheMainRoadOverTheRiver()
        {
            yield return LoadQuiet();
            var water = Layer("Water");
            // the main road east of the altar crosses the waterfall river on a bridge (x ~ 99-101)
            var bridges = Object.FindObjectsByType<SpriteRenderer>().Where(s => s.name == "Bridge").ToList();
            Assert.That(bridges.Count, Is.GreaterThanOrEqualTo(2), "bridges built");
            var east = bridges.OrderByDescending(b => b.transform.position.x).First().transform.position;
            // find the row of the bridge lane that is walkable from 5 tiles west to 5 tiles east of the bridge
            int cx = Mathf.FloorToInt(east.x), row = int.MinValue;
            for (int y = Mathf.FloorToInt(east.y); y <= Mathf.FloorToInt(east.y) + 4 && row == int.MinValue; y++)
            {
                bool clear = true;
                for (int x = cx - 5; x <= cx + 5; x++)
                    clear &= !Blocked(new Vector2(x + 0.5f, y + 0.5f));
                if (clear)
                    row = y;
            }
            Assert.AreNotEqual(int.MinValue, row, "a straight walkable lane crosses the bridge");
            yield return TeleportTo(new Vector2(cx - 4.5f, row + 0.5f));
            float startX = P.x;
            yield return Hold(Vector2.right, 2.6f);
            Assert.That(P.x, Is.GreaterThan(startX + 6f), "walked across the river");
            Assert.That(Cells(water), Is.GreaterThan(100));
        }

        [UnityTest]
        public IEnumerator BossPlateauIsGatedAtTheStairs()
        {
            yield return LoadQuiet();
            var arena = Object.FindAnyObjectByType<BossArena>();
            var gates = arena.GetComponentsInChildren<BoxCollider2D>(true);
            Assert.AreEqual(1, gates.Length, "one gate on the stairs");
            var g = gates[0];
            Assert.That(g.transform.position.y, Is.LessThan(arena.Trigger.yMin), "gate is below the arena trigger");
            Assert.That(g.transform.position.x, Is.InRange(arena.Trigger.xMin, arena.Trigger.xMax));
        }
    }
}
