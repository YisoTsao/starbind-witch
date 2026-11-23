using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starbind.CameraRig;
using Starbind.Combat;
using Starbind.Controls;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Player;
using Starbind.Progression;
using Starbind.Spells;
using Starbind.VFX;
using Starbind.World;
using Starbind.World.Tiles;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace Starbind.Editor
{
    /// <summary>Builds every piece of draft game content from the generated art and map JSON:
    /// sprite slicing (draft_manifest.json), materials, spell/enemy/upgrade data assets, the runtime
    /// GameDatabase, tiles, the player prefab and the Village (48x40), Workshop and Forest (64x64)
    /// scenes. Re-runnable: assets are updated in place, scenes are regenerated from the JSON.</summary>
    public static class GameContentBuilder
    {
        const string ArtRoot = "Assets/_Game/Art/";
        const string DraftManifest = ArtRoot + "draft_manifest.json";
        const string CharacterManifest = ArtRoot + "character_manifest.json"; // tools/art/extract_characters.py
        const string DecorManifest = ArtRoot + "decor_manifest.json";         // tools/art/village_decor.py
        const string UiManifest = ArtRoot + "ui_manifest.json";               // tools/art/ui_art.py (9-slice borders, icons)
        const string SpellVfxManifest = ArtRoot + "spell_vfx_manifest.json";  // tools/art/spell_vfx.py (blasts, sky bolts, flashes)
        public const string WitchSheet = "CHR_witch_ref";
        const string ExtractedArt = "Assets/_Game/Art/Props/Draft/";
        const string Maps = "Assets/_Game/Data/Maps/";
        const string DataRoot = "Assets/_Game/Data/";
        const string MaterialsRoot = "Assets/_Game/Materials/";
        const string DatabasePath = "Assets/_Game/Resources/GameDatabase.asset";
        const string PlayerPrefabPath = "Assets/_Game/Prefabs/Player/Player.prefab";
        const string PropPrefabOut = "Assets/_Game/Prefabs/Props/Draft/";
        const string ControlsPath = "Assets/_Game/Input/StarbindControls.inputactions";
        const string PromptArt = "Assets/_Game/Art/UI/Draft/UI_prompt_F_draft.png";
        const string DebugTileArt = "Assets/_Game/Tiles/Debug/DBG_block.png";
        const string SfxRoot = "Assets/_Game/Audio/SFX/Draft/";
        public const string VillageScene = "Assets/_Game/Scenes/Village.unity";
        public const string WorkshopScene = "Assets/_Game/Scenes/Workshop.unity";
        public const string ForestScene = "Assets/_Game/Scenes/Forest.unity";
        const int T = 32;

        // ---------------------------------------------------------------- JSON shapes
        [Serializable] class Manifest { public ManifestItem[] items; }
        [Serializable] class ManifestItem { public string name; public string path; public int w, h, frames, pivotX, pivotY, walkFrames, border; public float fps; }
        [Serializable] class ExtractManifest { public ExtractItem[] items; }
        [Serializable] class ExtractItem { public string name; public int w, h, pivotX, pivotY; }
        [Serializable] class MapDef
        {
            public string id, biome;
            public int width, height;
            public Layers layers;
            public PropDef[] props;
            public ObjDef[] objects;
            public SpawnDef[] spawns;
            public ZoneDef[] zones;
            public SceneDef scene;
        }
        [Serializable] class Layers { public string[] water, path, cliff, collision, wall, spawn; }
        [Serializable] class PropDef { public string sprite; public float x, y; public bool fade; public float[] box; public DoorDef door; public string layer; public int order; }
        [Serializable] class DoorDef { public float dx, dy; public string target, spawn; }
        [Serializable] class SpawnDef { public string id; public float x, y; }
        [Serializable] class ZoneDef { public string id, name; public float[] rect; public bool noSpawn; }
        [Serializable] class SceneDef { public string title, objective; public float dim; public bool safe; }
        [Serializable]
        class ObjDef
        {
            public string kind, id, sheet, name, facing, quest, sprite, scene, spawn, label, target, station;
            public float x, y, radius, prompt;
            public string[] lines, after;
            public bool heals, page;
            public float[] rect, block, gap, lever, trigger;
            public float[] bossSpawn;
            public RailDef[] railsParsed;
        }
        class RailDef { public float x, y, w, h; }

        // ---------------------------------------------------------------- entry points
        [MenuItem("Starbind/Build All Game Content (draft art)")]
        public static void BuildAll()
        {
            ProjectConfigurator.Apply();
            AssetDatabase.Refresh();
            var sets = ImportDraftSprites();
            var extracted = ImportExtractedProps();
            var mats = CreateMaterials();
            var db = CreateDatabase(sets, mats);
            CreateTiles();
            AssetDatabase.SaveAssets(); // NewScene(Single) unloads unsaved assets; tiles are reloaded by path per scene
            var player = CreatePlayerPrefab(db, mats);
            var ctx = new Ctx { db = db, sets = sets, extracted = extracted, mats = mats, player = player };
            BuildScene(ctx, Maps + "village.json", VillageScene);
            BuildScene(ctx, Maps + "workshop.json", WorkshopScene);
            BuildScene(ctx, Maps + "forest.json", ForestScene);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(VillageScene, true),
                new EditorBuildSettingsScene(WorkshopScene, true),
                new EditorBuildSettingsScene(ForestScene, true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("[GameContentBuilder] built Village, Workshop, Forest");
        }

        /// <summary>Batch entry: Unity -batchmode -executeMethod Starbind.Editor.GameContentBuilder.BuildAllBatch</summary>
        public static void BuildAllBatch()
        {
            try
            {
                BuildAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Starbind/Import Spell VFX")]
        public static void ImportSpellVfx()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            var items = JsonUtility.FromJson<Manifest>(File.ReadAllText(SpellVfxManifest)).items;
            foreach (var item in items)
            {
                var frames = ImportSpriteSet(item);
                var set = db.sprites.Find(s => s.name == item.name);
                if (set == null)
                    db.sprites.Add(new SpriteSet { name = item.name, fps = item.fps, frames = frames });
                else
                {
                    set.fps = item.fps;
                    set.frames = frames;
                }
            }
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssetIfDirty(db);
        }

        /// <summary>Batch entry: builds a macOS player (no Windows module on this machine) into
        /// Builds/mac for launch and benchmark checks. Path from -buildOut or the default.</summary>
        public static void BuildMacPlayerBatch()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildOut");
            string outPath = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/mac/StarbindWitch.app";
            var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { VillageScene, WorkshopScene, ForestScene },
                locationPathName = outPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            Debug.Log($"[GameContentBuilder] build {report.summary.result}: {report.summary.totalSize / 1048576f:0.0} MB, {report.summary.totalErrors} errors, {report.summary.totalTime}");
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        class Ctx
        {
            public GameDatabase db;
            public Dictionary<string, Sprite[]> sets;
            public Dictionary<string, Sprite> extracted;
            public (Material unlit, Material character) mats;
            public GameObject player;
            public readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            public readonly HashSet<string> prefabFiles = new HashSet<string>();

            public Sprite Sprite(string name)
            {
                if (extracted.TryGetValue(name, out var s)) return s;
                if (sets.TryGetValue(name, out var f) && f.Length > 0) return f[0];
                throw new KeyError("sprite not found: " + name);
            }
        }

        class KeyError : Exception { public KeyError(string m) : base(m) { } }

        // ---------------------------------------------------------------- sprites
        /// <summary>Procedural drafts, the character sheets cut from the chibi references and the village decor.</summary>
        static ManifestItem[] LoadManifestItems()
        {
            var items = new List<ManifestItem>(JsonUtility.FromJson<Manifest>(File.ReadAllText(DraftManifest)).items);
            if (File.Exists(CharacterManifest))
                items.AddRange(JsonUtility.FromJson<Manifest>(File.ReadAllText(CharacterManifest)).items);
            if (File.Exists(DecorManifest))
                items.AddRange(JsonUtility.FromJson<Manifest>(File.ReadAllText(DecorManifest)).items);
            if (File.Exists(UiManifest))
                items.AddRange(JsonUtility.FromJson<Manifest>(File.ReadAllText(UiManifest)).items);
            if (File.Exists(SpellVfxManifest))
                items.AddRange(JsonUtility.FromJson<Manifest>(File.ReadAllText(SpellVfxManifest)).items);
            return items.ToArray();
        }

        static Dictionary<string, Sprite[]> ImportDraftSprites()
        {
            var result = new Dictionary<string, Sprite[]>();
            foreach (var it in LoadManifestItems())
                result[it.name] = ImportSpriteSet(it);
            return result;
        }

        static Sprite[] ImportSpriteSet(ManifestItem item)
        {
            AssetDatabase.ImportAsset(item.path, ImportAssetOptions.ForceSynchronousImport);
            var pivot = new Vector2(item.pivotX / (float)item.w, item.pivotY / (float)item.h);
            return Slice(item.path, item.w, item.h, item.frames, 1, pivot, item.border);
        }

        static Dictionary<string, Sprite> ImportExtractedProps()
        {
            var man = JsonUtility.FromJson<ExtractManifest>(File.ReadAllText(ExtractedArt + "extract_manifest_unity.json"));
            var result = new Dictionary<string, Sprite>();
            foreach (var item in man.items)
            {
                var path = ExtractedArt + item.name + ".png";
                var ti = (TextureImporter)AssetImporter.GetAtPath(path);
                ti.spriteImportMode = SpriteImportMode.Single;
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.spriteAlignment = (int)SpriteAlignment.Custom;
                s.spritePivot = new Vector2(item.pivotX / (float)item.w, item.pivotY / (float)item.h);
                ti.SetTextureSettings(s);
                ti.SaveAndReimport();
                result[item.name] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            }
            return result;
        }

        /// <summary>Slices a sheet of cols x rows frames (reading order) with one shared pivot.</summary>
        internal static Sprite[] Slice(string path, int fw, int fh, int cols, int rows, Vector2 pivot, int border = 0)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null)
                throw new KeyError("no texture importer for " + path);
            ti.spriteImportMode = SpriteImportMode.Multiple;
            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            var existing = dp.GetSpriteRects().ToDictionary(r => r.name);
            var baseName = Path.GetFileNameWithoutExtension(path);
            var rects = new List<SpriteRect>();
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                {
                    int index = r * cols + c;
                    var name = baseName + "_" + index;
                    var sr = existing.TryGetValue(name, out var e) ? e : new SpriteRect { spriteID = GUID.Generate() };
                    sr.name = name;
                    sr.rect = new Rect(c * fw, (rows - 1 - r) * fh, fw, fh);
                    sr.alignment = SpriteAlignment.Custom;
                    sr.pivot = pivot;
                    sr.border = new Vector4(border, border, border, border); // 9-slice (UI frames)
                    rects.Add(sr);
                }
            dp.SetSpriteRects(rects.ToArray());
            var ids = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            ids?.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            dp.Apply();
            ti.SaveAndReimport();
            var byName = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToDictionary(s => s.name);
            return Enumerable.Range(0, rows * cols).Select(i => byName[baseName + "_" + i]).ToArray();
        }

        // ---------------------------------------------------------------- materials, data, database
        static (Material, Material) CreateMaterials()
        {
            Directory.CreateDirectory(MaterialsRoot);
            var unlit = UpsertMaterial(MaterialsRoot + "MAT_vfx_unlit.mat", Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            var character = UpsertMaterial(MaterialsRoot + "MAT_character_flash.mat", Shader.Find("Starbind/SpriteFlash"));
            return (unlit, character);
        }

        static Material UpsertMaterial(string path, Shader shader)
        {
            if (shader == null)
                throw new KeyError("shader missing for " + path);
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.shader = shader;
            EditorUtility.SetDirty(m);
            return m;
        }

        static T Upsert<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            bool created = asset == null;
            if (created)
                asset = ScriptableObject.CreateInstance<T>();
            fill(asset);
            if (created)
                AssetDatabase.CreateAsset(asset, path);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static SpellDefinition Spell(string id, string name, string desc, SpellForm form, ElementType el, SpellModifier mod, SpellTrigger trig,
            float dmg, float cd, float mp, float speed, float range, float radius, float status, int count = 1, int pierce = 0, float duration = 0, string icon = null) =>
            Upsert<SpellDefinition>(DataRoot + "Spells/SPL_" + id + ".asset", s =>
            {
                s.id = id; s.displayName = name; s.description = desc; s.form = form; s.element = el; s.modifier = mod; s.trigger = trig;
                s.damage = dmg; s.cooldown = cd; s.mpCost = mp; s.speed = speed; s.range = range; s.radius = radius; s.statusDuration = status;
                s.count = count; s.pierce = pierce; s.duration = duration; s.icon = icon ?? "ICON_" + id; s.vfx = "";
            });

        static EnemyDefinition Enemy(string id, string name, EnemyBehaviour b, float hp, float dmg, float speed, float range, float cd, float radius, int xp,
            string sprite, float fps, float loot, float kbResist = 0, float projSpeed = 4.5f, float pref = 0, bool elite = false, bool boss = false) =>
            Upsert<EnemyDefinition>(DataRoot + "Enemies/ENM_" + id + ".asset", e =>
            {
                e.id = id; e.displayName = name; e.behaviour = b; e.hp = hp; e.damage = dmg; e.speed = speed; e.attackRange = range; e.attackCooldown = cd;
                e.radius = radius; e.xp = xp; e.sprite = sprite; e.spriteFps = fps; e.lootChance = loot; e.knockbackResist = kbResist;
                e.projectileSpeed = projSpeed; e.preferredDistance = pref; e.isElite = elite; e.isBoss = boss;
            });

        static UpgradeDefinition Card(string id, string name, string desc, UpgradeKind kind, float weight, int stacks, string requires = null, string excludes = null, string icon = null) =>
            Upsert<UpgradeDefinition>(DataRoot + "Upgrades/UPG_" + id + ".asset", u =>
            {
                u.id = id; u.displayName = name; u.description = desc; u.kind = kind; u.weight = weight; u.maxStacks = stacks;
                u.requiresSpell = requires ?? ""; u.excludesSpell = excludes ?? ""; u.icon = icon ?? "ICON_staff";
            });

        static GameDatabase CreateDatabase(Dictionary<string, Sprite[]> sets, (Material unlit, Material character) mats)
        {
            var manifestItems = LoadManifestItems();
            var spells = new List<SpellDefinition>
            {
                // GDD §5.3 whitelist + §5.4 numbers (initial balance values)
                Spell("fire_bolt", "火球術", "飛彈・火。命中附加燃燒 4 秒。", SpellForm.Missile, ElementType.Fire, SpellModifier.None, SpellTrigger.Active, 18, 0.65f, 5, 10f, 8f, 0.25f, 4f),
                Spell("split_flame", "分裂火球", "飛彈・火・分裂。命中後分裂兩枚小火球。", SpellForm.Missile, ElementType.Fire, SpellModifier.Split, SpellTrigger.Active, 15, 0.9f, 7, 9.5f, 7f, 0.25f, 3f),
                Spell("piercing_ice", "穿透冰晶", "飛彈・冰・穿透。貫穿 2 名敵人，附加冰緩 30%。", SpellForm.Missile, ElementType.Ice, SpellModifier.Pierce, SpellTrigger.Active, 12, 0.8f, 6, 12f, 9f, 0.22f, 2.5f, 1, 2),
                Spell("chain_spark", "雷弧落擊", "領域・雷。在目標點落雷，範圍 1.5。", SpellForm.Area, ElementType.Lightning, SpellModifier.None, SpellTrigger.Active, 22, 2.8f, 14, 0, 7f, 1.5f, 2.5f),
                Spell("wind_pull", "旋風牽引", "特殊控制。2 秒旋風把敵人吸向中心；遇到燃燒的敵人形成火焰龍捲。", SpellForm.Pull, ElementType.Wind, SpellModifier.None, SpellTrigger.Active, 8, 6f, 18, 0, 6f, 2.5f, 2f, 1, 0, 2f),
                Spell("frost_orbit", "冰晶環繞", "自動・環繞・冰。冰刃繞身，同一敵人 0.6 秒內只受傷一次。", SpellForm.Orbit, ElementType.Ice, SpellModifier.None, SpellTrigger.Auto, 8, 0.5f, 0, 0, 1.4f, 0.3f, 2.5f, 2),
                Spell("thunder_pulse", "自動雷脈衝", "自動・雷。每 5 秒對附近敵人落雷。", SpellForm.Pulse, ElementType.Lightning, SpellModifier.None, SpellTrigger.Auto, 12, 5f, 0, 0, 6f, 1.3f, 2f),
                Spell("homing_missile", "追蹤魔彈", "自動。每 2.5 秒射出追蹤最近敵人的魔彈。", SpellForm.Homing, ElementType.None, SpellModifier.None, SpellTrigger.Auto, 9, 2.5f, 0, 8f, 7f, 0.2f, 0f, 1, 0, 0, "ICON_homing"),
                Spell("star_fire", "星火護符", "自動・火。定期發射小型星火攻擊最近敵人。", SpellForm.Homing, ElementType.Fire, SpellModifier.None, SpellTrigger.Auto, 10, 3f, 0, 9f, 7f, 0.2f, 3f, 1, 0, 0, "ICON_starfire"),
            };
            var enemies = new List<EnemyDefinition>
            {
                // GDD §10.1 (initial test values)
                Enemy("slime", "苔蘚史萊姆", EnemyBehaviour.Chaser, 45, 10, 2.2f, 0.55f, 1.1f, 0.28f, 1, "ENM_slime_draft", 8, 0.3f),
                Enemy("spore_shroom", "毒孢菇", EnemyBehaviour.Ranged, 55, 12, 1.6f, 0.5f, 2.4f, 0.3f, 2, "ENM_spore_shroom_draft", 6, 0.4f, 0, 4.5f, 4.5f),
                Enemy("root_guard", "樹根守衛", EnemyBehaviour.Charger, 140, 20, 1.3f, 0.9f, 3.5f, 0.55f, 4, "ENM_root_guard_draft", 5, 0.5f, 0.6f),
                Enemy("wisp", "迷途幽光", EnemyBehaviour.Flanker, 35, 8, 3.6f, 0.5f, 2.6f, 0.25f, 1, "ENM_wisp_draft", 10, 0.3f),
                Enemy("spore_elite", "孢子精英", EnemyBehaviour.Elite, 350, 15, 1.4f, 1f, 2.4f, 0.7f, 15, "ENM_spore_elite_draft", 6, 1f, 0.8f, 6f, 0, true),
                Enemy("mushroom_king", "月蝕菇王", EnemyBehaviour.Boss, 2200, 16, 1.0f, 2f, 2.6f, 1.25f, 0, "ENM_mushroom_king_draft", 6, 0f, 1f, 5f, 0, false, true),
            };
            var cards = new List<UpgradeDefinition>
            {
                // GDD §8.2: 15 MVP cards
                Card("U01", "火球分裂", "火球命中後向外分裂 2 枚低傷小彈。", UpgradeKind.Active, 10, 1, "fire_bolt", null, "ICON_split_flame"),
                Card("U02", "火球穿透", "火球可額外貫穿 1 名敵人。", UpgradeKind.Active, 10, 2, "fire_bolt", null, "ICON_fire_bolt"),
                Card("U03", "冰霜附著", "冰狀態持續時間 +25%。", UpgradeKind.Element, 8, 3, "piercing_ice|frost_orbit", null, "ICON_piercing_ice"),
                Card("U04", "雷弧擴散", "碎晶雷暴的電弧跳躍 +1（上限 8）。", UpgradeKind.Element, 8, 5, "chain_spark|thunder_pulse|piercing_ice|frost_orbit", null, "ICON_chain_spark"),
                Card("U05", "旋風增幅", "旋風牽引範圍 +20%。", UpgradeKind.Active, 8, 3, "wind_pull", null, "ICON_wind_pull"),
                Card("U06", "魔彈增殖", "追蹤魔彈每次多一枚。", UpgradeKind.Auto, 9, 3, "homing_missile", null, "ICON_homing"),
                Card("U07", "冰晶環繞", "解鎖冰晶環繞；已擁有則多一把冰刃。", UpgradeKind.Auto, 10, 4, null, null, "ICON_frost_orbit"),
                Card("U08", "自動雷脈衝", "解鎖自動落雷（每 5 秒）。", UpgradeKind.Auto, 9, 1, null, "thunder_pulse", "ICON_thunder_pulse"),
                Card("U09", "星火護符", "定期發射一枚星火攻擊最近敵人。", UpgradeKind.Auto, 9, 1, null, "star_fire", "ICON_starfire"),
                Card("U10", "魔力脈動", "MP 回復 +20%。", UpgradeKind.Passive, 7, 3, null, null, "ICON_overdrive"),
                Card("U11", "敏捷步伐", "移動速度 +8%。", UpgradeKind.Passive, 7, 3, null, null, "ICON_dash"),
                Card("U12", "穩固身軀", "最大 HP +15，立即回復 15。", UpgradeKind.Passive, 7, 3, null, null, "PICK_potion_hp"),
                Card("U13", "共鳴增幅", "元素反應的共鳴獲得 +20%。", UpgradeKind.Passive, 6, 3, null, null, "ICON_overdrive"),
                Card("U14", "連鎖精通", "元素反應傷害 +15%。", UpgradeKind.Passive, 7, 3, null, null, "ICON_chain_spark"),
                Card("U15", "倒轉書頁", "飛彈到達盡頭後折返，返程傷害 60%，飛行速度 -15%。", UpgradeKind.Cursed, 3, 1, "fire_bolt|piercing_ice|split_flame", null, "ICON_piercing_ice"),
            };
            return Upsert<GameDatabase>(DatabasePath, db =>
            {
                db.sprites = manifestItems.Select(it => new SpriteSet { name = it.name, fps = it.fps, walkFrames = it.walkFrames > 0 ? it.walkFrames : 4, frames = sets[it.name] }).ToList();
                db.spells = spells;
                db.enemies = enemies;
                db.upgrades = cards;
                db.unlitMaterial = mats.unlit;
                db.characterMaterial = mats.character;
                db.sounds = Directory.Exists(SfxRoot)
                    ? Directory.GetFiles(SfxRoot, "*.wav").Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\', '/'))).Where(a => a != null).ToList()
                    : new List<AudioClip>();
            });
        }

        // ---------------------------------------------------------------- tiles
        class TileSet
        {
            public TileBase grass, path, water, rim, face, waterfall, block;
            public TileBase fGrass, fPath, fWater;
            public TileBase floor, wallTop, wallFace;
        }

        static TileSet CreateTiles()
        {
            const string village = "Assets/_Game/Art/Tilesets/Village/Draft/";
            const string forest = "Assets/_Game/Art/Tilesets/Forest/Draft/";
            const string interior = "Assets/_Game/Art/Tilesets/Interior/Draft/";
            const string tileOut = "Assets/_Game/Tiles/";
            var c = new Vector2(0.5f, 0.5f);
            var grass = Slice(village + "TILE_grass_draft.png", T, T, 8, 1, c);
            var path = Slice(village + "TILE_path_draft_256.png", T, T, 16, 16, c);
            var water = Slice(village + "TILE_water_draft_256.png", T, T, 16, 16, c);
            var cliff = Slice(village + "TILE_cliff_draft.png", T, T, 10, 1, c); // 2-tile-tall face: 4 upper, 4 lower, waterfall upper/lower
            var rim = Slice(village + "TILE_rim_draft.png", T, T, 8, 1, c);
            var fg = Slice(forest + "TILE_forest_grass_draft.png", T, T, 4, 1, c);
            var fp = Slice(forest + "TILE_forest_path_draft_256.png", T, T, 16, 16, c);
            var fw = Slice(forest + "TILE_forest_water_draft_256.png", T, T, 16, 16, c);
            var inn = Slice(interior + "TILE_interior_draft.png", T, T, 6, 1, c);

            var set = new TileSet();
            set.grass = Upsert<VariantTile>(tileOut + "Village/TL_grass_draft.asset", t => t.sprites = grass);
            set.path = Upsert<NeighborMaskTile>(tileOut + "Village/TL_path_draft.asset", t => { t.mode = NeighborMaskMode.Full8; t.sprites = path; });
            set.water = Upsert<NeighborMaskTile>(tileOut + "Village/TL_water_draft.asset", t => { t.mode = NeighborMaskMode.Full8; t.sprites = water; });
            set.face = Upsert<NeighborMaskTile>(tileOut + "Village/TL_cliff_face_draft.asset", t =>
            {
                t.mode = NeighborMaskMode.EastWestNorth;
                t.sprites = new[] { cliff[3], cliff[1], cliff[2], cliff[0], cliff[7], cliff[5], cliff[6], cliff[4] };
            });
            set.waterfall = Upsert<NeighborMaskTile>(tileOut + "Village/TL_waterfall_tall_draft.asset", t =>
            {
                t.mode = NeighborMaskMode.EastWestNorth;
                t.sprites = new[] { cliff[8], cliff[8], cliff[8], cliff[8], cliff[9], cliff[9], cliff[9], cliff[9] };
                t.alsoMatches = new[] { set.face };
            });
            ((NeighborMaskTile)set.face).alsoMatches = new[] { set.waterfall };
            set.rim = Upsert<NeighborMaskTile>(tileOut + "Village/TL_plateau_rim_draft.asset", t => { t.mode = NeighborMaskMode.NorthEastWestOpen; t.sprites = rim; });
            set.fGrass = Upsert<VariantTile>(tileOut + "Forest/TL_forest_grass_draft.asset", t => t.sprites = fg);
            set.fPath = Upsert<NeighborMaskTile>(tileOut + "Forest/TL_forest_path_draft.asset", t => { t.mode = NeighborMaskMode.Full8; t.sprites = fp; });
            set.fWater = Upsert<NeighborMaskTile>(tileOut + "Forest/TL_forest_water_draft.asset", t => { t.mode = NeighborMaskMode.Full8; t.sprites = fw; });
            set.floor = Upsert<VariantTile>(tileOut + "Interior/TL_floor_draft.asset", t => t.sprites = inn.Take(4).ToArray());
            set.wallTop = Upsert<Tile>(tileOut + "Interior/TL_wall_top_draft.asset", t => { t.sprite = inn[4]; t.colliderType = Tile.ColliderType.None; });
            set.wallFace = Upsert<Tile>(tileOut + "Interior/TL_wall_face_draft.asset", t => { t.sprite = inn[5]; t.colliderType = Tile.ColliderType.None; });
            var dbg = AssetDatabase.LoadAssetAtPath<Sprite>(DebugTileArt);
            set.block = Upsert<Tile>("Assets/_Game/Tiles/Debug/TL_collision_block.asset", t => { t.sprite = dbg; t.colliderType = Tile.ColliderType.Grid; });
            return set;
        }

        static TileSet LoadTiles()
        {
            TT L<TT>(string p) where TT : TileBase => AssetDatabase.LoadAssetAtPath<TT>("Assets/_Game/Tiles/" + p + ".asset");
            return new TileSet
            {
                grass = L<TileBase>("Village/TL_grass_draft"), path = L<TileBase>("Village/TL_path_draft"), water = L<TileBase>("Village/TL_water_draft"),
                waterfall = L<TileBase>("Village/TL_waterfall_tall_draft"), face = L<TileBase>("Village/TL_cliff_face_draft"), rim = L<TileBase>("Village/TL_plateau_rim_draft"),
                fGrass = L<TileBase>("Forest/TL_forest_grass_draft"), fPath = L<TileBase>("Forest/TL_forest_path_draft"), fWater = L<TileBase>("Forest/TL_forest_water_draft"),
                floor = L<TileBase>("Interior/TL_floor_draft"), wallTop = L<TileBase>("Interior/TL_wall_top_draft"), wallFace = L<TileBase>("Interior/TL_wall_face_draft"),
                block = L<TileBase>("Debug/TL_collision_block"),
            };
        }

        // ---------------------------------------------------------------- player
        static GameObject CreatePlayerPrefab(GameDatabase db, (Material unlit, Material character) mats)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            var root = new GameObject("Player") { layer = LayerMask.NameToLayer(StarbindLayers.Player) };
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            var feet = root.AddComponent<CapsuleCollider2D>();
            feet.direction = CapsuleDirection2D.Horizontal;
            feet.size = new Vector2(0.5f, 0.24f);
            feet.offset = new Vector2(0, 0.12f);

            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = db.Sprite(WitchSheet);
            sr.sharedMaterial = mats.character;
            sr.sortingLayerName = StarbindLayers.SortWorld;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            var anim = visual.AddComponent<CharacterAnimator>();
            anim.Configure(sr, WitchSheet);

            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(root.transform, false);
            shadow.transform.localScale = new Vector3(1.4f, 1.4f, 1);
            var ssr = shadow.AddComponent<SpriteRenderer>();
            ssr.sprite = db.Sprite("VFX_shadow");
            ssr.sharedMaterial = mats.unlit;
            ssr.sortingLayerName = StarbindLayers.SortGroundDecal;

            var promptGo = new GameObject("InteractPrompt");
            promptGo.transform.SetParent(root.transform, false);
            var prompt = promptGo.AddComponent<SpriteRenderer>();
            prompt.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PromptArt);
            prompt.sortingLayerName = StarbindLayers.SortWorldUI;
            prompt.enabled = false;

            var health = root.AddComponent<Health>();
            SetField(health, "team", (int)Team.Player);
            SetField(health, "maxHp", PlayerVitals.BaseHp);
            SetField(health, "radius", 0.25f);
            SetVec(health, "centerOffset", new Vector2(0, 0.45f));
            root.AddComponent<PlayerMotor>();
            root.AddComponent<PlayerVitals>();
            root.AddComponent<AbilityRunner>();
            root.AddComponent<AutoCastController>();
            root.AddComponent<PlayerBuild>();
            root.AddComponent<PlayerCombat>();
            var flash = root.AddComponent<HitFlash>();
            flash.Bind(sr);
            root.AddComponent<CombatFeedback>();
            var reader = root.AddComponent<PlayerInputReader>();
            Set(reader, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>(ControlsPath));
            var interactor = root.AddComponent<PlayerInteractor>();
            Set(interactor, "prompt", prompt);
            var controller = root.AddComponent<PlayerController>();
            Set(controller, "input", reader);
            Set(controller, "interactor", interactor);
            Set(flash, "target", sr);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void Set(UnityEngine.Object target, string field, UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetField(UnityEngine.Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            if (p.propertyType == SerializedPropertyType.Enum || p.propertyType == SerializedPropertyType.Integer)
                p.intValue = (int)value;
            else if (p.propertyType == SerializedPropertyType.Boolean)
                p.boolValue = value != 0;
            else
                p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetVec(UnityEngine.Object target, string field, Vector2 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetString(UnityEngine.Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------------------------------------------------------------- props
        static GameObject PropPrefab(Ctx ctx, PropDef p)
        {
            string key = p.sprite + (p.box != null && p.box.Length == 4 ? $"_b{p.box[0]}x{p.box[1]}" : "") + (p.fade ? "_f" : "");
            if (ctx.prefabs.TryGetValue(key, out var existing))
                return existing;
            Directory.CreateDirectory(PropPrefabOut);
            var go = new GameObject(p.sprite.Replace("_draft", ""));
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ctx.Sprite(p.sprite);
            sr.sortingLayerName = StarbindLayers.SortWorld;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
            go.layer = LayerMask.NameToLayer(StarbindLayers.WorldSolid);
            if (p.box != null && p.box.Length == 4)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(p.box[0], p.box[1]);
                box.offset = new Vector2(p.box[2], p.box[3]);
            }
            if (p.fade)
                go.AddComponent<OcclusionFader>();
            // Phase-0 prefab names kept for the north-block props (same sprite + same box + fade)
            string file = ctx.prefabFiles.Add(p.sprite) ? p.sprite : key.Replace('.', '_');
            ctx.prefabFiles.Add(file);
            var path = PropPrefabOut + file + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            ctx.prefabs[key] = prefab;
            return prefab;
        }

        /// <summary>Soft 2D lights on glowing props (display only): x/y offset from the pivot, colour, radius, intensity.</summary>
        static readonly Dictionary<string, (float x, float y, Color color, float radius, float intensity)[]> PropLightTable =
            new Dictionary<string, (float, float, Color, float, float)[]>
            {
                ["PROP_lantern_post_draft"] = new[] { (0.25f, 0.95f, new Color(1f, 0.8f, 0.48f), 2.6f, 0.85f) },
                ["PROP_lamp_draft"] = new[] { (0f, 1.1f, new Color(1f, 0.8f, 0.48f), 2.4f, 0.8f) },
                ["PROP_crystal_fountain_draft"] = new[] { (0f, 0.9f, new Color(0.62f, 0.5f, 1f), 3.2f, 0.9f) },
                ["PROP_mushroom_glow_draft"] = new[] { (0f, 0.3f, new Color(0.55f, 0.62f, 1f), 1.6f, 0.75f) },
                ["PROP_glow_shroom_draft"] = new[] { (0f, 0.3f, new Color(0.55f, 0.62f, 1f), 1.4f, 0.7f) },
                ["PROP_cauldron_draft"] = new[] { (0f, 0.35f, new Color(0.62f, 1f, 0.5f), 1.6f, 0.7f) },
                ["PROP_crystal_cluster_draft"] = new[] { (0f, 0.35f, new Color(0.78f, 0.55f, 1f), 1.4f, 0.7f) },
                ["PROP_candles_draft"] = new[] { (0f, 0.3f, new Color(1f, 0.78f, 0.42f), 1.3f, 0.75f) },
                ["PROP_cottage_draft"] = new[]
                {
                    // wall lantern, right window, round attic window (positions measured on reference A)
                    (-0.97f, 1.0f, new Color(1f, 0.78f, 0.42f), 1.6f, 0.75f), (1.34f, 0.84f, new Color(1f, 0.78f, 0.42f), 1.6f, 0.7f),
                    (0.28f, 2.0f, new Color(1f, 0.8f, 0.5f), 1.4f, 0.6f),
                },
            };

        static void AddPropLights(Transform prop, string sprite)
        {
            if (!PropLightTable.TryGetValue(sprite, out var lights))
                return;
            foreach (var (x, y, color, radius, intensity) in lights)
            {
                var go = new GameObject("Glow");
                go.transform.SetParent(prop, false);
                go.transform.localPosition = new Vector3(x, y, 0);
                var l = go.AddComponent<Light2D>();
                l.lightType = Light2D.LightType.Point;
                l.color = color;
                l.intensity = intensity;
                l.pointLightOuterRadius = radius;
                l.pointLightInnerRadius = radius * 0.15f;
                l.falloffIntensity = 0.7f;
            }
        }

        static SpriteRenderer Decal(Ctx ctx, Transform parent, string sprite, Vector2 pos, string layer, int order, string name = null)
        {
            var go = new GameObject(name ?? sprite.Replace("_draft", ""));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = ctx.Sprite(sprite);
            sr.sortingLayerName = layer;
            sr.sortingOrder = order;
            if (layer == StarbindLayers.SortWorld)
                sr.spriteSortPoint = SpriteSortPoint.Pivot;
            return sr;
        }

        static GameObject Trigger(Transform parent, string name, Vector2 pos, float radius, string id, float promptHeight)
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer(StarbindLayers.Interactable) };
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var c = go.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = radius;
            go.AddComponent<Interactable>().Configure(id, new Vector2(0, promptHeight));
            return go;
        }

        static BoxCollider2D Blocker(Transform parent, string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer(StarbindLayers.WorldSolid) };
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var b = go.AddComponent<BoxCollider2D>();
            b.size = size;
            return b;
        }

        // ---------------------------------------------------------------- scenes
        static void BuildScene(Ctx ctx, string jsonPath, string scenePath)
        {
            var map = JsonUtility.FromJson<MapDef>(File.ReadAllText(jsonPath));
            ParseRails(map, File.ReadAllText(jsonPath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            int W = map.width, H = map.height;
            bool forest = map.biome == "forest", interior = map.biome == "interior";

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            // village: a touch of lavender dusk so lanterns, windows and crystals read as light sources
            light.intensity = forest ? 0.82f : interior ? 1f : 0.9f;
            light.color = forest ? new Color(0.86f, 0.9f, 1f) : interior ? new Color(1f, 0.96f, 0.9f) : new Color(0.97f, 0.93f, 1f);

            var cfg = new GameObject("SceneConfig").AddComponent<SceneConfig>();
            cfg.title = map.scene.title;
            cfg.characterDim = map.scene.dim;
            cfg.safeZone = map.scene.safe;
            cfg.objective = map.scene.objective;

            // ---- tilemaps (WORLD_STRUCTURE §5)
            var grid = new GameObject("Grid").AddComponent<Grid>();
            var ground = Layer(grid, "GroundBase", 0);
            var water = Layer(grid, "Water", 10);
            var path = Layer(grid, "Path", 20);
            var cliff = Layer(grid, "Cliff", 30);
            var collision = Layer(grid, "Collision", 100);
            var collisionGo = collision.gameObject;
            collisionGo.layer = LayerMask.NameToLayer(StarbindLayers.WorldSolid);
            collisionGo.GetComponent<TilemapRenderer>().sortingLayerName = StarbindLayers.SortWorldUI;
            collisionGo.AddComponent<EditorOnlyRenderer>();
            // per-cell boxes: CompositeCollider2D produced 0 paths in 6000.6.8f1 (SPEC_CONFLICTS C-17)
            var tc = collisionGo.AddComponent<TilemapCollider2D>();
            tc.compositeOperation = Collider2D.CompositeOperation.None;

            var t = LoadTiles();
            var baseTile = interior ? t.floor : forest ? t.fGrass : t.grass;
            var cells = new List<Vector3Int>(W * H);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    cells.Add(new Vector3Int(x, y, 0));
            ground.SetTiles(cells.ToArray(), Enumerable.Repeat(baseTile, cells.Count).ToArray());
            var L = map.layers;
            if (L.water != null) Paint(L.water, H, water, c => c == '~' ? (forest ? t.fWater : t.water) : null);
            if (L.path != null) Paint(L.path, H, path, c => c == 'p' ? (forest ? t.fPath : t.path) : null);
            if (L.cliff != null) Paint(L.cliff, H, cliff, c => c == 'R' ? t.rim : c == 'F' ? t.face : c == 'W' ? t.waterfall : null);
            if (L.wall != null) Paint(L.wall, H, cliff, c => c == 'T' ? t.wallTop : c == 'F' ? t.wallFace : null);
            if (L.collision != null) Paint(L.collision, H, collision, c => c == '#' ? t.block : null);
            Debug.Log($"[GameContentBuilder] {map.id}: water {water.GetUsedTilesCount()} path {path.GetUsedTilesCount()} cliff/wall {cliff.GetUsedTilesCount()} collision {collision.GetUsedTilesCount()} (rows {L.collision?.Length})");

            // ---- props
            var world = new GameObject("World").transform;
            var props = new GameObject("Props").transform;
            props.SetParent(world, false);
            var decals = new GameObject("Decals").transform;
            foreach (var p in map.props)
            {
                if (!string.IsNullOrEmpty(p.layer))
                {
                    Decal(ctx, decals, p.sprite, new Vector2(p.x, p.y), p.layer, p.order);
                    continue;
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(PropPrefab(ctx, p), props);
                go.transform.position = new Vector3(p.x, p.y, 0);
                AddPropLights(go.transform, p.sprite);
                if (p.door != null && !string.IsNullOrEmpty(p.door.target))
                {
                    var door = Trigger(go.transform, "Door", new Vector2(p.x + p.door.dx, p.y + p.door.dy), 0.45f, "cottage_door", 1.55f);
                    door.AddComponent<DoorPortal>().Configure(p.door.target, p.door.spawn);
                }
            }

            if (!forest && !interior)
            {
                var motes = new GameObject("AmbientMotes").AddComponent<AmbientMotes>();
                motes.transform.SetParent(world, false);
                motes.Configure(ctx.Sprite("DEC_mote_draft"), ctx.mats.unlit, 26);
            }

            // ---- objects
            var objects = new GameObject("Objects").transform;
            var npcs = new GameObject("NPCs").transform;
            npcs.SetParent(world, false);
            foreach (var o in map.objects ?? Array.Empty<ObjDef>())
                BuildObject(ctx, o, objects, npcs, world);

            // ---- spawns, zones, grid
            var spawns = new GameObject("SpawnPoints").transform;
            foreach (var s in map.spawns)
            {
                var go = new GameObject(s.id);
                go.transform.SetParent(spawns, false);
                go.transform.position = new Vector3(s.x, s.y, 0);
                go.AddComponent<SpawnPoint>().Configure(s.id);
            }
            var zm = new GameObject("Zones").AddComponent<ZoneMap>();
            zm.Configure((map.zones ?? Array.Empty<ZoneDef>()).Select(z => new ZoneMap.Zone
            {
                id = z.id, displayName = z.name, rect = new Rect(z.rect[0], z.rect[1], z.rect[2], z.rect[3]), noSpawn = z.noSpawn,
            }).ToArray());
            if (L.spawn != null)
            {
                var systems = new GameObject("RunSystems");
                var sg = systems.AddComponent<SpawnGrid>();
                sg.Configure(W, H, L.spawn);
                var spawner = systems.AddComponent<EnemySpawner>();
                spawner.Grid = sg;
                systems.AddComponent<SpawnDirector>();
                systems.AddComponent<RunManager>();
            }

            // ---- player + camera
            var start = map.spawns[0];
            var player = (GameObject)PrefabUtility.InstantiatePrefab(ctx.player);
            player.transform.position = new Vector3(start.x, start.y, 0);
            SetString(player.GetComponent<PlayerController>(), "defaultSpawnId", start.id);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 270f / 2f / 32f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = interior ? new Color32(0x1b, 0x12, 0x1a, 0xff) : forest ? new Color32(0x10, 0x1c, 0x18, 0xff) : new Color32(0x1b, 0x1d, 0x2b, 0xff);
            camGo.transform.position = new Vector3(start.x, start.y, -10);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<UniversalAdditionalCameraData>();
            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 32;
            ppc.refResolutionX = 480;
            ppc.refResolutionY = 270;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.None;
            camGo.AddComponent<CameraFollow2D>().Configure(player.transform, new Rect(0, 0, W, H));

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        static void BuildObject(Ctx ctx, ObjDef o, Transform objects, Transform npcs, Transform world)
        {
            var pos = new Vector2(o.x, o.y);
            switch (o.kind)
            {
                case "npc":
                {
                    var root = new GameObject("NPC_" + o.id) { layer = LayerMask.NameToLayer(StarbindLayers.WorldSolid) };
                    root.transform.SetParent(npcs, false);
                    root.transform.position = pos;
                    var body = root.AddComponent<BoxCollider2D>();
                    body.size = new Vector2(0.5f, 0.25f);
                    body.offset = new Vector2(0, 0.12f);
                    Shadow(ctx, root.transform);
                    var vis = new GameObject("Visual");
                    vis.transform.SetParent(root.transform, false);
                    var sr = vis.AddComponent<SpriteRenderer>();
                    sr.sprite = ctx.sets[o.sheet][0];
                    sr.sharedMaterial = ctx.mats.character;
                    sr.sortingLayerName = StarbindLayers.SortWorld;
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    if (o.radius > 0)
                    {
                        // ambient villager: kinematic body so it can stroll (NpcWander) without being pushed
                        var rb = root.AddComponent<Rigidbody2D>();
                        rb.bodyType = RigidbodyType2D.Kinematic;
                        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                        root.AddComponent<NpcWander>().Configure(o.radius);
                    }
                    var anim = vis.AddComponent<CharacterAnimator>();
                    anim.Configure(sr, o.sheet);
                    vis.AddComponent<NpcFacing>().Configure(o.facing);
                    var talk = Trigger(root.transform, "Talk", pos + new Vector2(0, 0.2f), 0.8f, "npc_" + o.id, 1.75f);
                    talk.AddComponent<Talkable>().Configure(o.name, o.lines, o.after, o.quest, o.heals);
                    break;
                }
                case "dummy":
                {
                    var root = new GameObject("TrainingDummy") { layer = LayerMask.NameToLayer(StarbindLayers.WorldSolid) };
                    root.transform.SetParent(world, false);
                    root.transform.position = pos;
                    var b = root.AddComponent<BoxCollider2D>();
                    b.size = new Vector2(0.45f, 0.25f);
                    b.offset = new Vector2(0, 0.12f);
                    Shadow(ctx, root.transform);
                    var vis = new GameObject("Visual");
                    vis.transform.SetParent(root.transform, false);
                    var sr = vis.AddComponent<SpriteRenderer>();
                    sr.sprite = ctx.Sprite("PROP_training_dummy_draft");
                    sr.sharedMaterial = ctx.mats.character;
                    sr.sortingLayerName = StarbindLayers.SortWorld;
                    sr.spriteSortPoint = SpriteSortPoint.Pivot;
                    var h = root.AddComponent<Health>();
                    SetField(h, "team", (int)Team.Neutral);
                    SetField(h, "maxHp", 9999f);
                    SetField(h, "radius", 0.35f);
                    SetVec(h, "centerOffset", new Vector2(0, 0.6f));
                    SetField(h, "immortal", 1f);
                    var flash = root.AddComponent<HitFlash>();
                    flash.Bind(sr);
                    Set(flash, "target", sr);
                    root.AddComponent<TrainingDummy>();
                    break;
                }
                case "sign":
                case "sign_post":
                {
                    if (o.kind == "sign_post")
                    {
                        var p = new PropDef { sprite = o.sprite, x = o.x, y = o.y, box = new[] { 0.35f, 0.25f, 0, 0.12f } };
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(PropPrefab(ctx, p), world);
                        go.transform.position = pos;
                    }
                    var t = Trigger(objects, "Sign_" + o.name, pos + new Vector2(0, 0.1f), 0.6f, "sign", 1.3f);
                    t.AddComponent<Talkable>().Configure(o.name, o.lines);
                    break;
                }
                case "starplate":
                {
                    var p = new PropDef { sprite = "PROP_starplate_draft", x = o.x, y = o.y };
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(PropPrefab(ctx, p), world);
                    go.transform.position = pos;
                    go.GetComponent<SpriteRenderer>().sortingLayerName = StarbindLayers.SortGroundDecal;
                    var t = Trigger(objects, "StarPlate", pos + new Vector2(0, 0.4f), 0.8f, "star_plate", 1.2f);
                    t.AddComponent<LocalTeleport>().Configure(o.target);
                    break;
                }
                case "exit":
                {
                    var go = new GameObject("Exit_" + o.scene);
                    go.transform.SetParent(objects, false);
                    go.AddComponent<AreaExit>().Configure(new Rect(o.rect[0], o.rect[1], o.rect[2], o.rect[3]), o.scene, o.spawn, o.label);
                    break;
                }
                case "bridge":
                {
                    var sr = Decal(ctx, objects, o.sprite, pos, StarbindLayers.SortGround, 35, "Bridge");
                    foreach (var r in o.railsParsed ?? Array.Empty<RailDef>())
                        Blocker(sr.transform, "Rail", new Vector2(r.x, r.y), new Vector2(r.w, r.h));
                    break;
                }
                case "door":
                {
                    var t = Trigger(objects, "Door", pos, o.radius > 0 ? o.radius : 0.6f, "workshop_door", 1.2f);
                    t.AddComponent<DoorPortal>().Configure(o.scene, o.spawn);
                    break;
                }
                case "station":
                {
                    var t = Trigger(objects, "Station_" + o.station, pos, o.radius, "station_" + o.station.ToLower(), o.prompt);
                    t.AddComponent<WorkshopStation>().Configure((WorkshopStation.Kind)Enum.Parse(typeof(WorkshopStation.Kind), o.station));
                    break;
                }
                case "altar":
                {
                    var p = new PropDef { sprite = "PROP_altar_draft", x = o.x, y = o.y, box = new[] { 1.4f, 0.5f, 0, 0.3f } };
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(PropPrefab(ctx, p), world);
                    go.transform.position = pos;
                    var t = Trigger(objects, "StarAltar", pos + new Vector2(0, -0.2f), 1.2f, "star_altar", 1.9f);
                    t.AddComponent<StarAltar>();
                    break;
                }
                case "chest":
                {
                    var sr = Decal(ctx, world, "PROP_chest_draft", pos, StarbindLayers.SortWorld, 0, "Chest");
                    sr.gameObject.layer = LayerMask.NameToLayer(StarbindLayers.WorldSolid);
                    var b = sr.gameObject.AddComponent<BoxCollider2D>();
                    b.size = new Vector2(0.8f, 0.35f);
                    b.offset = new Vector2(0, 0.18f);
                    var t = Trigger(sr.transform, "Open", pos + new Vector2(0, -0.1f), 0.7f, "chest", 1.1f);
                    t.AddComponent<Chest>().Configure(sr, o.page);
                    break;
                }
                case "vine":
                {
                    var sr = Decal(ctx, world, "PROP_vine_draft", pos, StarbindLayers.SortWorld, 0, "BurnableVine");
                    int vines = Mathf.Max(1, Mathf.RoundToInt(o.block[0]));
                    for (int ix = 1; ix < vines; ix++) // extra whole vine sprites across the path, alternating sides
                        Decal(ctx, sr.transform, "PROP_vine_draft", pos + new Vector2((ix + 1) / 2 * (ix % 2 == 1 ? 1f : -1f), 0), StarbindLayers.SortWorld, 0, "Vine");
                    var root = sr.gameObject;
                    var blocker = Blocker(objects, "VineBlocker", pos + new Vector2(0, 0.3f), new Vector2(o.block[0], o.block[1]));
                    var h = root.AddComponent<Health>();
                    SetField(h, "team", (int)Team.Neutral);
                    SetField(h, "maxHp", 9999f);
                    SetField(h, "radius", 0.6f);
                    SetVec(h, "centerOffset", new Vector2(0, 0.6f));
                    SetField(h, "immortal", 1f);
                    var v = root.AddComponent<BurnableVine>();
                    v.Configure(sr, blocker);
                    Set(v, "sprite", sr);
                    Set(v, "blocker", blocker);
                    break;
                }
                case "bridge_stone":
                {
                    var bridge = Decal(ctx, objects, "PROP_bridge_stone_broken_draft", pos, StarbindLayers.SortGround, 35, "StoneBridge");
                    var gap = Blocker(objects, "BridgeGap", pos, new Vector2(o.gap[0], o.gap[1]));
                    var leverPos = new Vector2(o.lever[0], o.lever[1]);
                    var lever = Decal(ctx, world, "PROP_lever_draft", leverPos, StarbindLayers.SortWorld, 0, "BridgeLever");
                    var t = Trigger(lever.transform, "Pull", leverPos, 0.6f, "bridge_lever", 1.1f);
                    var sw = t.AddComponent<BridgeSwitch>();
                    sw.Configure(lever, bridge, gap);
                    Set(sw, "lever", lever);
                    Set(sw, "bridge", bridge);
                    Set(sw, "gap", gap);
                    break;
                }
                case "boss_arena":
                {
                    var go = new GameObject("BossArena");
                    go.transform.SetParent(objects, false);
                    var gates = new List<Collider2D>();
                    var raw = File.ReadAllText(Maps + "forest.json");
                    foreach (var g in ParseGates(raw))
                    {
                        var gate = Blocker(go.transform, "Gate", new Vector2(g[0], g[1]), new Vector2(g[2], g[3]));
                        // whole vine sprites side by side (no stretching: keeps the pixel grid)
                        int across = Mathf.Max(1, Mathf.RoundToInt(g[2])), down = Mathf.Max(1, Mathf.RoundToInt(g[3] / 1.2f));
                        for (int ix = 0; ix < across; ix++)
                            for (int iy = 0; iy < down; iy++)
                                Decal(ctx, gate.transform, "PROP_vine_draft",
                                    new Vector2(g[0] - g[2] * 0.5f + 0.5f + ix, g[1] - g[3] * 0.5f + iy * 1.2f), StarbindLayers.SortWorld, 0, "GateVines");
                        gates.Add(gate);
                    }
                    var arena = go.AddComponent<BossArena>();
                    var spawnAt = new Vector2(o.bossSpawn[0], o.bossSpawn[1]);
                    arena.Configure(new Rect(o.trigger[0], o.trigger[1], o.trigger[2], o.trigger[3]), spawnAt, gates.ToArray());
                    var so = new SerializedObject(arena);
                    so.FindProperty("trigger").rectValue = new Rect(o.trigger[0], o.trigger[1], o.trigger[2], o.trigger[3]);
                    so.FindProperty("bossSpawn").vector2Value = spawnAt;
                    var gp = so.FindProperty("gates");
                    gp.arraySize = gates.Count;
                    for (int i = 0; i < gates.Count; i++)
                        gp.GetArrayElementAtIndex(i).objectReferenceValue = gates[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                }
                default:
                    Debug.LogWarning("[GameContentBuilder] unknown object kind " + o.kind);
                    break;
            }
        }

        static void Shadow(Ctx ctx, Transform parent)
        {
            var s = new GameObject("Shadow");
            s.transform.SetParent(parent, false);
            s.transform.localScale = new Vector3(1.4f, 1.4f, 1);
            var sr = s.AddComponent<SpriteRenderer>();
            sr.sprite = ctx.Sprite("VFX_shadow_draft");
            sr.sharedMaterial = ctx.mats.unlit;
            sr.sortingLayerName = StarbindLayers.SortGroundDecal;
        }

        // JsonUtility cannot read nested arrays; these small parsers read the few that exist.
        static void ParseRails(MapDef map, string raw)
        {
            if (map.objects == null)
                return;
            int searchFrom = 0;
            foreach (var o in map.objects)
            {
                if (o.kind != "bridge")
                    continue;
                int i = raw.IndexOf("\"rails\"", searchFrom, StringComparison.Ordinal);
                if (i < 0)
                    continue;
                searchFrom = i + 1;
                o.railsParsed = ParseNested(raw, i).Select(a => new RailDef { x = a[0], y = a[1], w = a[2], h = a[3] }).ToArray();
            }
        }

        static List<float[]> ParseGates(string raw)
        {
            int i = raw.IndexOf("\"gates\"", StringComparison.Ordinal);
            return i < 0 ? new List<float[]>() : ParseNested(raw, i);
        }

        static List<float[]> ParseNested(string raw, int keyIndex)
        {
            var result = new List<float[]>();
            int start = raw.IndexOf('[', keyIndex);
            int depth = 0, i = start;
            int inner = -1;
            for (; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '[')
                {
                    depth++;
                    if (depth == 2) inner = i;
                }
                else if (c == ']')
                {
                    if (depth == 2)
                        result.Add(raw.Substring(inner + 1, i - inner - 1).Split(',').Select(s => float.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray());
                    depth--;
                    if (depth == 0) break;
                }
            }
            return result;
        }

        static Tilemap Layer(Grid grid, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid.transform, false);
            var tm = go.AddComponent<Tilemap>();
            var r = go.AddComponent<TilemapRenderer>();
            r.sortingLayerName = StarbindLayers.SortGround;
            r.sortingOrder = order;
            r.mode = TilemapRenderer.Mode.Chunk;
            return tm;
        }

        static void Paint(string[] rows, int height, Tilemap tm, Func<char, TileBase> pick)
        {
            var cells = new List<Vector3Int>();
            var list = new List<TileBase>();
            for (int r = 0; r < rows.Length; r++)
                for (int x = 0; x < rows[r].Length; x++)
                {
                    var t = pick(rows[r][x]);
                    if (t == null)
                        continue;
                    cells.Add(new Vector3Int(x, height - 1 - r, 0));
                    list.Add(t);
                }
            tm.SetTiles(cells.ToArray(), list.ToArray());
        }
    }
}
