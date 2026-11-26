using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starbind.Core;
using Starbind.World.Studio;
using Starbind.World.Tiles;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

namespace Starbind.Editor.WorldStudio
{
    /// <summary>Imports a biome art manifest (tools/art/moonveil_art.py): slices the terrain sheets into
    /// tiles (PeriodicTile ground, 256-mask autotiles, cliff face/rim/waterfall, detail variants), registers
    /// every prop sprite in the GameDatabase (so the scene builder and UI resolve them by name), fills the
    /// biome's prop catalogue, and packs one Sprite Atlas per biome. Re-runnable; assets update in place.</summary>
    public static class WorldArtImporter
    {
        const int T = 32;
        const string DatabasePath = "Assets/_Game/Resources/GameDatabase.asset";

        [Serializable] class Manifest { public string status; public TileItem[] tiles; public PropItem[] props; }
        [Serializable] class TileItem { public string name, path, kind; public int cols, rows; public string[] labels; }
        [Serializable] class PropItem { public string name, path, category; public int w, h, frames, pivotX, pivotY; public bool fade; public float[] box, light; }

        public sealed class Result
        {
            public TileBase ground, dark, path, paver, water, face, rim, waterfall, stairs;
            public TileBase[] details = Array.Empty<TileBase>();
            public PropSpec[] catalog = Array.Empty<PropSpec>();
            public int sprites;
        }

        /// <param name="manifestPath">e.g. Assets/_Game/Art/Biomes/Moonveil/moonveil_manifest.json</param>
        /// <param name="tileFolder">e.g. Assets/_Game/Tiles/Moonveil/</param>
        public static Result Import(string manifestPath, string tileFolder, string atlasPath)
        {
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("biome art manifest missing (run tools/art/moonveil_art.py)", manifestPath);
            var man = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            AssetDatabase.Refresh();
            Directory.CreateDirectory(tileFolder);
            var res = new Result();
            var c = new Vector2(0.5f, 0.5f);
            foreach (var it in man.tiles)
            {
                AssetDatabase.ImportAsset(it.path, ImportAssetOptions.ForceSynchronousImport);
                var sprites = GameContentBuilder.Slice(it.path, T, T, it.cols, it.rows, c);
                string tp = tileFolder + "TL_" + it.name.Replace("TILE_", "") + ".asset";
                switch (it.kind)
                {
                    case "periodic":
                        res.ground = GameContentBuilder.Upsert<PeriodicTile>(tp, t => { t.columns = it.cols; t.rows = it.rows; t.sprites = sprites; });
                        break;
                    case "mask256":
                        var tile = GameContentBuilder.Upsert<NeighborMaskTile>(tp, t => { t.mode = NeighborMaskMode.Full8; t.sprites = sprites; });
                        if (it.name.Contains("_dark")) res.dark = tile;
                        else if (it.name.Contains("_path")) res.path = tile;
                        else if (it.name.Contains("_paver")) res.paver = tile;
                        else if (it.name.Contains("_water")) res.water = tile;
                        break;
                    case "cliff":
                        // sheet: upper [mid, left_end, right_end, single], lower [same], waterfall upper, lower.
                        // EastWestNorth index = E same + 2*W same + 4*N same; a left end has no west neighbour.
                        var face = GameContentBuilder.Upsert<NeighborMaskTile>(tileFolder + "TL_mv_cliff_face.asset", t =>
                        {
                            t.mode = NeighborMaskMode.EastWestNorth;
                            t.sprites = new[] { sprites[3], sprites[1], sprites[2], sprites[0], sprites[7], sprites[5], sprites[6], sprites[4] };
                        });
                        var fall = GameContentBuilder.Upsert<NeighborMaskTile>(tileFolder + "TL_mv_waterfall.asset", t =>
                        {
                            t.mode = NeighborMaskMode.EastWestNorth;
                            t.sprites = new[] { sprites[8], sprites[8], sprites[8], sprites[8], sprites[9], sprites[9], sprites[9], sprites[9] };
                            t.alsoMatches = new TileBase[] { face };
                        });
                        face.alsoMatches = new TileBase[] { fall };
                        EditorUtility.SetDirty(face);
                        res.face = face;
                        res.waterfall = fall;
                        break;
                    case "stairs":
                        // same layout as the face rows: [upper mid, left end, right end, single][lower ...]
                        res.stairs = GameContentBuilder.Upsert<NeighborMaskTile>(tp, t =>
                        {
                            t.mode = NeighborMaskMode.EastWestNorth;
                            t.sprites = new[] { sprites[3], sprites[1], sprites[2], sprites[0], sprites[7], sprites[5], sprites[6], sprites[4] };
                        });
                        break;
                    case "rim":
                        res.rim = GameContentBuilder.Upsert<NeighborMaskTile>(tp, t => { t.mode = NeighborMaskMode.NorthEastWestOpen; t.sprites = sprites; });
                        break;
                    case "detail":
                        var labels = it.labels ?? Array.Empty<string>();
                        var list = new List<TileBase>();
                        for (int k = 0; k < labels.Length; k++)
                        {
                            int i0 = k * 2;
                            list.Add(GameContentBuilder.Upsert<VariantTile>(tileFolder + "TL_mv_detail_" + labels[k] + ".asset",
                                t => t.sprites = new[] { sprites[i0], sprites[i0 + 1] }));
                        }
                        res.details = list.ToArray();
                        break;
                }
            }

            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var catalog = new List<PropSpec>();
            var atlasObjects = new List<UnityEngine.Object>();
            foreach (var p in man.props)
            {
                AssetDatabase.ImportAsset(p.path, ImportAssetOptions.ForceSynchronousImport);
                var frames = GameContentBuilder.Slice(p.path, p.w, p.h, 1, 1, new Vector2(p.pivotX / (float)p.w, p.pivotY / (float)p.h));
                if (db != null)
                {
                    var set = db.sprites.Find(s => s.name == p.name);
                    if (set == null)
                        db.sprites.Add(new SpriteSet { name = p.name, fps = 1, frames = frames });
                    else
                        set.frames = frames;
                }
                atlasObjects.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(p.path));
                var spec = new PropSpec
                {
                    sprite = p.name, category = p.category, pixelSize = new Vector2Int(p.w, p.h), fade = p.fade,
                    box = p.box != null && p.box.Length == 4 ? new Vector4(p.box[0], p.box[1], p.box[2], p.box[3]) : Vector4.zero,
                };
                if (p.light != null && p.light.Length == 7)
                {
                    spec.hasLight = true;
                    spec.lightColor = new Color(p.light[0], p.light[1], p.light[2]);
                    spec.light = new Vector4(p.light[3], p.light[4], p.light[5], p.light[6]);
                }
                catalog.Add(spec);
                res.sprites++;
            }
            if (db != null)
            {
                EditorUtility.SetDirty(db);
                AssetDatabase.SaveAssetIfDirty(db);
            }
            foreach (var it in man.tiles)
                atlasObjects.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(it.path));
            PackAtlas(atlasPath, atlasObjects.Where(o => o != null).ToArray());
            res.catalog = catalog.ToArray();
            AssetDatabase.SaveAssets();
            return res;
        }

        /// <summary>One V2 Sprite Atlas per biome (tiles + props): fewer texture switches when the
        /// region is drawn. Point filter, no compression, no padding bleed (pixel art rules).</summary>
        static void PackAtlas(string path, UnityEngine.Object[] textures)
        {
            if (string.IsNullOrEmpty(path) || textures.Length == 0)
                return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var atlas = new SpriteAtlasAsset();
                atlas.Add(textures);
                SpriteAtlasAsset.Save(atlas, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(path) is SpriteAtlasImporter imp)
                {
                    imp.packingSettings = new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 2, enableAlphaDilation = false };
                    imp.textureSettings = new SpriteAtlasTextureSettings { filterMode = FilterMode.Point, generateMipMaps = false, sRGB = true, readable = false };
                    var plat = imp.GetPlatformSettings("DefaultTexturePlatform");
                    plat.maxTextureSize = 4096;
                    plat.textureCompression = TextureImporterCompression.Uncompressed;
                    imp.SetPlatformSettings(plat);
                    imp.SaveAndReimport();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WorldArtImporter] sprite atlas not packed: " + e.Message);
            }
        }
    }
}
