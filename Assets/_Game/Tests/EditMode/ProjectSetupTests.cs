using System.IO;
using System.Linq;
using NUnit.Framework;
using Starbind.Core;
using Starbind.World.Tiles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Starbind.Tests
{
    public class ProjectSetupTests
    {
        [Test]
        public void SortingLayersExistInDesignOrder()
        {
            var names = SortingLayer.layers.Select(l => l.name).ToList();
            int last = -1;
            foreach (var n in StarbindLayers.SortingOrder)
            {
                int i = names.IndexOf(n);
                Assert.That(i, Is.GreaterThan(last), $"sorting layer {n} missing or out of order: {string.Join(",", names)}");
                last = i;
            }
        }

        [Test]
        public void PhysicsLayersExist()
        {
            foreach (var n in new[] { StarbindLayers.Player, StarbindLayers.WorldSolid, StarbindLayers.Interactable, StarbindLayers.Water })
                Assert.That(LayerMask.NameToLayer(n), Is.GreaterThanOrEqualTo(0), n);
        }

        [Test]
        public void Renderer2DSortsByCustomYAxis()
        {
            var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("Assets/_Game/Settings/Renderer2D.asset"));
            Assert.AreEqual((int)TransparencySortMode.CustomAxis, so.FindProperty("m_TransparencySortMode").intValue);
            Assert.AreEqual(new Vector3(0, 1, 0), so.FindProperty("m_TransparencySortAxis").vector3Value);
        }

        [Test]
        public void GravityIsZero() => Assert.AreEqual(Vector2.zero, Physics2D.gravity);

        [Test]
        public void ArtTexturesImportAsPixelArt()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Art", "Assets/_Game/Tiles" });
            Assert.That(guids.Length, Is.GreaterThan(10));
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.AreEqual(32f, ti.spritePixelsPerUnit, path);
                Assert.AreEqual(FilterMode.Point, ti.filterMode, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, ti.textureCompression, path);
                Assert.IsFalse(ti.mipmapEnabled, path);
            }
        }

        [Test]
        public void PropPivotsSitOnWholePixels()
        {
            foreach (var g in AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/_Game/Art/Props" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                foreach (var s in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    Assert.AreEqual(Mathf.Round(s.pivot.x), s.pivot.x, 1e-3, path);
                    Assert.AreEqual(Mathf.Round(s.pivot.y), s.pivot.y, 1e-3, path);
                    Assert.AreEqual(0, (int)s.rect.width % 2, "odd width " + path);
                    Assert.AreEqual(0, (int)s.rect.height % 2, "odd height " + path);
                }
            }
        }

        [Test]
        public void MapActionIsBoundToM()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>("Assets/_Game/Input/StarbindControls.inputactions");
            var map = asset.FindActionMap("Gameplay", true).FindAction("Map");
            Assert.IsNotNull(map, "Map action");
            Assert.That(map.bindings.Any(b => b.path == "<Keyboard>/m"));
        }

        [Test]
        public void UiFramesAreNineSliced()
        {
            foreach (var name in new[] { "UI_panel_draft", "UI_slot_draft", "UI_bar_frame_draft", "UI_keycap_draft" })
            {
                var s = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/UI/Draft/" + name + ".png").OfType<Sprite>().FirstOrDefault();
                Assert.IsNotNull(s, name);
                Assert.That(s.border.x, Is.GreaterThan(0), name + " has a 9-slice border");
            }
            var icon = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Art/UI/Draft/ICON_fire_bolt_draft.png").OfType<Sprite>().First();
            Assert.AreEqual(32, (int)icon.rect.width, "32x32 icons");
        }

        [Test]
        public void VillageSceneIsInBuild()
        {
            Assert.That(EditorBuildSettings.scenes.Any(s => s.path == "Assets/_Game/Scenes/Village.unity" && s.enabled));
            Assert.That(File.Exists("Assets/_Game/Scenes/Village.unity"));
        }

        [Test]
        public void PathAutotilePicksSpriteFromNeighbourMask()
        {
            var tile = AssetDatabase.LoadAssetAtPath<NeighborMaskTile>("Assets/_Game/Tiles/Village/TL_path_draft.asset");
            Assert.IsNotNull(tile);
            Assert.AreEqual(256, tile.sprites.Length);
            var go = new GameObject("TestGrid", typeof(Grid));
            try
            {
                var tm = new GameObject("TM", typeof(Tilemap)).GetComponent<Tilemap>();
                tm.transform.SetParent(go.transform);
                // plus shape: centre has N, E, S, W neighbours -> mask 1|4|16|64 = 85
                foreach (var c in new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 0, 0), new Vector3Int(0, -1, 0), new Vector3Int(-1, 0, 0) })
                    tm.SetTile(c, tile);
                Assert.AreSame(tile.sprites[85], tm.GetSprite(Vector3Int.zero), "centre");
                Assert.AreSame(tile.sprites[16 | 8 | 32], tm.GetSprite(new Vector3Int(0, 1, 0)), "north arm: S + diagonals SE, SW");
                Assert.AreSame(tile.sprites[64 | 128 | 32], tm.GetSprite(new Vector3Int(1, 0, 0)), "east arm: W + diagonals NW, SW");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GrassVariantsAreDeterministicAndAllUsed()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    var p = new Vector3Int(x, y, 0);
                    int a = VariantTile.Pick(p, 4);
                    Assert.AreEqual(a, VariantTile.Pick(p, 4));
                    seen.Add(a);
                }
            Assert.AreEqual(4, seen.Count);
        }
    }
}
