using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starbind.CameraRig;
using Starbind.Combat;
using Starbind.Controls;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Items;
using Starbind.Player;
using Starbind.Progression;
using Starbind.Spells;
using Starbind.VFX;
using Starbind.World;
using Starbind.World.Studio;
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
        /// <summary>Chapter 1 map before the World Studio overhaul (tools/map/forest.py). No longer built or
        /// shipped: chapter 1 runs in Moonveil Forest (DECISIONS WVO-1). Kept for reference only.</summary>
        public const string LegacyForestScene = "Assets/_Game/Scenes/Dev/Legacy/Forest_v1.unity";
        /// <summary>Witch World Studio regions (docs/WORLD_GENERATOR_SPEC.md): generated map JSON -> scene.</summary>
        public const string MoonveilScene = "Assets/_Game/Scenes/Regions/MoonveilForest.unity";
        const string ChapterArtManifest = ArtRoot + "chapter_manifest.json"; // tools/art/chapter_art.py (chapters 2-5)

        /// <summary>Chapter scenes after the forest (map json id, scene path), STORY_WORLD_QUESTS §3.6.</summary>
        public static readonly (string map, string scene)[] ChapterScenes =
        {
            ("mine", "Assets/_Game/Scenes/Mine.unity"),
            ("lake", "Assets/_Game/Scenes/Lake.unity"),
            ("observatory", "Assets/_Game/Scenes/Observatory.unity"),
            ("citadel", "Assets/_Game/Scenes/Citadel.unity"),
        };

        public static string[] AllScenes()
        {
            var list = new List<string> { VillageScene, WorkshopScene, MoonveilScene };
            list.AddRange(ChapterScenes.Select(c => c.scene));
            return list.ToArray();
        }
        const int T = 32;

        // ---------------------------------------------------------------- JSON shapes
        [Serializable] class Manifest { public ManifestItem[] items; }
        [Serializable] class ManifestItem { public string name; public string path; public int w, h, frames, pivotX, pivotY, walkFrames, border; public float fps; }
        [Serializable] class ExtractManifest { public ExtractItem[] items; }
        [Serializable] class ExtractItem { public string name; public int w, h, pivotX, pivotY; }
        [Serializable] class MapDef
        {
            public string id, biome;
            /// <summary>World Studio regions: BiomeDefinition asset that supplies tiles and look.</summary>
            public string biomeAsset;
            public int width, height;
            public Layers layers;
            public PropDef[] props;
            public ObjDef[] objects;
            public SpawnDef[] spawns;
            public ZoneDef[] zones;
            public SceneDef scene;
        }
        [Serializable] class Layers { public string[] water, path, cliff, collision, wall, spawn, solid, dark, detail; }
        [Serializable] class PropDef { public string sprite; public float x, y; public bool fade; public float[] box; public DoorDef door; public string layer; public int order; public float[] light; }
        [Serializable] class DoorDef { public float dx, dy; public string target, spawn; }
        [Serializable] class SpawnDef { public string id; public float x, y; }
        [Serializable] class ZoneDef { public string id, name; public float[] rect; public bool noSpawn; }
        [Serializable] class SceneDef { public string title, objective, chapter, intro, runObjective; public float dim; public bool safe; }
        [Serializable] class StageDef { public string flag, quest; public string[] lines; }
        [Serializable] class MirrorDef { public int x, y, kind; }
        [Serializable]
        class ObjDef
        {
            public string kind, id, sheet, name, facing, quest, sprite, scene, spawn, label, target, station;
            public string gate, circuit, reward, requires, school, notice, openSprite, litSprite, leverSprite, leverOnSprite;
            public float x, y, radius, prompt, w, h;
            public int wave, count, dirX, dirY, tx, ty;
            public string[] lines, after, locked, enemies, frames;
            public StageDef[] stages;
            public MirrorDef[] mirrors;
            public bool heals, page;
            public float[] rect, block, gap, lever, trigger;
            public float[] bossSpawn, stop, span;
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
            foreach (var (map, scene) in ChapterScenes)
                if (File.Exists(Maps + map + ".json"))
                    BuildScene(ctx, Maps + map + ".json", scene);
            WorldStudio.WorldStudioPipeline.BuildAllRegions(); // Moonveil Forest: art import, preset, generate, scene
            EditorBuildSettings.scenes = AllScenes().Where(File.Exists).Select(p => new EditorBuildSettingsScene(p, true)).ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[GameContentBuilder] built " + string.Join(", ", EditorBuildSettings.scenes.Select(x => Path.GetFileNameWithoutExtension(x.path))));
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

        /// <summary>Builds one map JSON into a scene with the assets already in the project (no re-import),
        /// and adds the scene to the build settings. Used by Witch World Studio.</summary>
        internal static void BuildRegionScene(string jsonPath, string scenePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            BuildScene(LoadContext(), jsonPath, scenePath);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(sc => sc.path == scenePath))
            {
                list.Add(new EditorBuildSettingsScene(scenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Context from the existing database, extracted props, materials and player prefab.</summary>
        static Ctx LoadContext()
        {
            var db = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (db == null)
                throw new KeyError("GameDatabase missing: run Starbind/Build All Game Content first");
            var sets = new Dictionary<string, Sprite[]>();
            foreach (var set in db.sprites)
                if (set != null && !string.IsNullOrEmpty(set.name))
                    sets[set.name] = set.frames;
            var extracted = new Dictionary<string, Sprite>();
            var manPath = ExtractedArt + "extract_manifest_unity.json";
            if (File.Exists(manPath))
                foreach (var item in JsonUtility.FromJson<ExtractManifest>(File.ReadAllText(manPath)).items)
                {
                    var sp = AssetDatabase.LoadAssetAtPath<Sprite>(ExtractedArt + item.name + ".png");
                    if (sp != null)
                        extracted[item.name] = sp;
                }
            var mats = (AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "MAT_vfx_unlit.mat"),
                        AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "MAT_character_flash.mat"));
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return new Ctx { db = db, sets = sets, extracted = extracted, mats = mats, player = player };
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
                scenes = AllScenes().Where(File.Exists).ToArray(),
                locationPathName = outPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None,
            });
            Debug.Log($"[GameContentBuilder] build {report.summary.result}: {report.summary.totalSize / 1048576f:0.0} MB, {report.summary.totalErrors} errors, {report.summary.totalTime}");
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>Batch entry: WebGL player for phone and desktop browsers (no Android/iOS module on
        /// this machine). Gzip with decompression fallback so any static web server can host it;
        /// landscape default orientation. Path from -buildOut or Builds/webgl.</summary>
        [MenuItem("Starbind/Build WebGL (mobile browser)")]
        public static void BuildWebGLPlayer() => BuildWebGL(false);

        public static void BuildWebGLPlayerBatch() => BuildWebGL(true);

        static void BuildWebGL(bool exit)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-buildOut");
            string outPath = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/webgl";
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = AllScenes().Where(File.Exists).ToArray(),
                locationPathName = outPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            Debug.Log($"[GameContentBuilder] WebGL build {report.summary.result}: {report.summary.totalSize / 1048576f:0.0} MB, {report.summary.totalErrors} errors, {report.summary.totalTime}");
            if (exit)
                EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }

        class Ctx
        {
            public GameDatabase db;
            public Dictionary<string, Sprite[]> sets;
            public Dictionary<string, Sprite> extracted;
            public (Material unlit, Material character) mats;
            public GameObject player;
            public string rawJson;
            public readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
            public readonly HashSet<string> prefabFiles = new HashSet<string>();

            public bool HasSprite(string name) => extracted.ContainsKey(name) || (sets.TryGetValue(name, out var f) && f.Length > 0);

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
            if (File.Exists(ChapterArtManifest))
                items.AddRange(JsonUtility.FromJson<Manifest>(File.ReadAllText(ChapterArtManifest)).items);
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

        internal static T Upsert<T>(string path, Action<T> fill) where T : ScriptableObject
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

        /// <summary>Enemy plus the chapter-2+ fields: loot material, shot sprite, summons, shot element.</summary>
        static EnemyDefinition EnemyX(string id, string name, EnemyBehaviour b, float hp, float dmg, float speed, float range, float cd, float radius, int xp,
            float fps, float loot, string material, string shot = "", string summon = "", string summon2 = "", float kbResist = 0, float projSpeed = 4.5f,
            float pref = 0, bool elite = false, bool boss = false, ElementType element = ElementType.None)
        {
            var e = Enemy(id, name, b, hp, dmg, speed, range, cd, radius, xp, "ENM_" + id + "_draft", fps, loot, kbResist, projSpeed, pref, elite, boss);
            e.lootMaterial = material;
            e.projectileSprite = shot;
            e.summonId = summon;
            e.summonId2 = summon2;
            e.element = element;
            EditorUtility.SetDirty(e);
            return e;
        }

        static ChapterDefinition Chapter(string id, int order, string scene, string title, string boss, string bossName, string arena, string elite,
            string victory, string requires, string intro, string push, string[] rewards, string rewardNote, float hpScale, bool finale,
            params (string zone, string[] ids, float[] w)[] zones) =>
            Upsert<ChapterDefinition>(DataRoot + "Chapters/CH_" + id + ".asset", c =>
            {
                c.id = id; c.order = order; c.scene = scene; c.title = title; c.bossId = boss; c.bossName = bossName; c.arenaName = arena;
                c.eliteId = elite; c.victoryFlag = victory; c.requiresFlag = requires; c.introNotice = intro; c.pushObjective = push;
                c.bossObjective = "擊敗" + bossName; c.rewardSpells = rewards; c.rewardNote = rewardNote; c.enemyHpScale = hpScale; c.finale = finale;
                c.entrySpawn = "spawn_from_village";
                c.returnSpawn = id == "forest" ? "spawn_from_forest" : "spawn_from_starplate";
                c.zones = zones.Select(z => new ZoneMix { zone = z.zone, enemies = z.ids, weights = z.w }).ToList();
            });

        static QuestDefinition Quest(int order, string id, string chapter, QuestKind kind, string title, string desc, string giver, string flag, string requires) =>
            Upsert<QuestDefinition>(DataRoot + "Quests/Q_" + id + ".asset", q =>
            {
                q.order = order; q.id = id; q.chapter = chapter; q.kind = kind; q.title = title; q.description = desc; q.giver = giver;
                q.flag = flag; q.requires = requires ?? "";
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
                // chapter rewards (STORY_WORLD_QUESTS §2.3): beam form + nature (ch.2), moon + echo (ch.3), star (ch.4)
                Spell("prism_beam", "稜光射線", "光束・雷。瞬間貫穿直線上最多 8 名敵人，附加感電。", SpellForm.Beam, ElementType.Lightning, SpellModifier.None, SpellTrigger.Active, 16, 1.6f, 10, 0, 7f, 0.35f, 2.5f),
                Spell("thorn_bloom", "荊棘毒花", "自動・自然。每 4 秒在最近敵人腳下開出毒花，附加中毒 5 秒。", SpellForm.Pulse, ElementType.Nature, SpellModifier.None, SpellTrigger.Auto, 8, 4f, 0, 0, 6.5f, 1.6f, 5f),
                Spell("shadow_hand", "暗影之手", "領域・月影。目標點伸出暗影之手，附加詛咒（受傷 +15%）。", SpellForm.Area, ElementType.Moon, SpellModifier.None, SpellTrigger.Active, 20, 3.2f, 12, 0, 7f, 1.8f, 4f),
                Spell("moon_echo", "月影迴響", "飛彈・月影・迴響。0.3 秒後再射出 60% 威力的回聲。", SpellForm.Missile, ElementType.Moon, SpellModifier.Echo, SpellTrigger.Active, 14, 0.9f, 7, 10f, 8f, 0.24f, 3f),
                Spell("star_shot", "星彈", "飛彈・星痕。命中後反彈至附近敵人 2 次（每次 80%）。", SpellForm.Missile, ElementType.Star, SpellModifier.None, SpellTrigger.Active, 15, 0.75f, 6, 11f, 8f, 0.24f, 0f),
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
                // ch.2 star core mine
                EnemyX("crystal_mite", "晶蟎", EnemyBehaviour.Bomber, 30, 14, 2.8f, 0.5f, 1f, 0.24f, 1, 10, 0.25f, Inventory.StarSand),
                EnemyX("ore_bat", "礦洞蝙蝠", EnemyBehaviour.Flanker, 32, 9, 3.8f, 0.5f, 2.4f, 0.24f, 1, 10, 0.3f, Inventory.StarSand),
                EnemyX("gem_spitter", "晶刺蟲", EnemyBehaviour.Ranged, 55, 13, 1.5f, 0.5f, 2.3f, 0.3f, 2, 6, 0.4f, Inventory.StarSand, "VFX_crystal_shard", projSpeed: 5f, pref: 4.5f),
                EnemyX("rock_golemling", "石像兵", EnemyBehaviour.Charger, 150, 21, 1.3f, 0.9f, 3.4f, 0.55f, 4, 5, 0.5f, Inventory.CoreShard, kbResist: 0.6f),
                EnemyX("crystal_elite", "晶甲精英", EnemyBehaviour.Elite, 400, 16, 1.4f, 1f, 2.4f, 0.7f, 15, 6, 1f, Inventory.StarSand, "VFX_crystal_shard", "crystal_mite", kbResist: 0.8f, projSpeed: 6f, elite: true),
                EnemyX("crystal_golem", "晶核魔像", EnemyBehaviour.GolemBoss, 2600, 18, 0.9f, 2f, 2.6f, 1.3f, 0, 6, 0, Inventory.CoreShard, "VFX_crystal_shard", "crystal_mite", "gem_spitter", 1f, 5f, boss: true),
                // ch.3 starlight lake
                EnemyX("lake_slime", "湖水史萊姆", EnemyBehaviour.Chaser, 50, 11, 2.3f, 0.55f, 1.1f, 0.28f, 1, 8, 0.3f, Inventory.MoonDew),
                EnemyX("moon_moth", "月光飛蛾", EnemyBehaviour.Flanker, 36, 10, 3.6f, 0.5f, 2.4f, 0.25f, 1, 10, 0.35f, Inventory.MoonDew),
                EnemyX("mire_frog", "沼澤蛙", EnemyBehaviour.Ranged, 60, 13, 1.6f, 0.5f, 2.2f, 0.3f, 2, 6, 0.4f, Inventory.MoonDew, "VFX_water_drop", projSpeed: 5f, pref: 4.5f),
                EnemyX("reflection_knight", "倒影騎士", EnemyBehaviour.Charger, 165, 22, 1.35f, 0.9f, 3.3f, 0.55f, 4, 5, 0.5f, Inventory.StarSand, kbResist: 0.6f),
                EnemyX("shadow_clone", "湖影分身", EnemyBehaviour.Flanker, 60, 10, 3.4f, 0.5f, 2.2f, 0.3f, 1, 8, 0.1f, Inventory.MoonDew),
                EnemyX("shadow_elite", "湖影精英", EnemyBehaviour.Elite, 430, 17, 1.4f, 1f, 2.3f, 0.7f, 15, 6, 1f, Inventory.MoonDew, "VFX_shadow_bolt", "lake_slime", kbResist: 0.8f, projSpeed: 6f, elite: true),
                EnemyX("lake_shadow", "湖影", EnemyBehaviour.ShadowBoss, 2800, 18, 1.2f, 2f, 2.5f, 1.0f, 0, 6, 0, Inventory.MoonDew, "VFX_shadow_bolt", "shadow_clone", "", 1f, 5f, boss: true),
                // ch.4 lost observatory
                EnemyX("snow_slime", "雪球史萊姆", EnemyBehaviour.Chaser, 56, 12, 2.2f, 0.55f, 1.1f, 0.3f, 1, 8, 0.3f, Inventory.StarSand),
                EnemyX("frost_wisp", "霜靈", EnemyBehaviour.Flanker, 40, 11, 3.6f, 0.5f, 2.4f, 0.25f, 1, 10, 0.35f, Inventory.MoonDew),
                EnemyX("lens_turret", "透鏡砲台", EnemyBehaviour.Ranged, 80, 14, 0.7f, 0.5f, 2.6f, 0.35f, 2, 4, 0.45f, Inventory.CoreShard, "VFX_beam_seg", projSpeed: 6f, pref: 5.5f, kbResist: 0.9f),
                EnemyX("clockwork_hound", "發條獵犬", EnemyBehaviour.Charger, 170, 23, 1.6f, 0.9f, 3f, 0.5f, 4, 8, 0.5f, Inventory.CoreShard, kbResist: 0.5f),
                EnemyX("gear_elite", "齒輪精英", EnemyBehaviour.Elite, 460, 18, 1.4f, 1f, 2.3f, 0.7f, 15, 6, 1f, Inventory.CoreShard, "VFX_star_shot", "snow_slime", kbResist: 0.8f, projSpeed: 6f, elite: true),
                EnemyX("star_guardian", "觀星儀守護者", EnemyBehaviour.GuardianBoss, 3000, 19, 0.8f, 2f, 2.5f, 1.3f, 0, 6, 0, Inventory.CoreShard, "VFX_star_shot", "lens_turret", "", 1f, 5f, boss: true),
                // finale: black moon citadel
                EnemyX("shade_slime", "影史萊姆", EnemyBehaviour.Chaser, 60, 13, 2.4f, 0.55f, 1.05f, 0.28f, 1, 8, 0.3f, Inventory.MoonDew),
                EnemyX("void_spark", "虛空火花", EnemyBehaviour.Bomber, 38, 16, 3f, 0.5f, 1f, 0.24f, 1, 10, 0.25f, Inventory.StarSand),
                EnemyX("dark_acolyte", "黑月信徒", EnemyBehaviour.Ranged, 70, 15, 1.6f, 0.5f, 2.1f, 0.3f, 2, 6, 0.4f, Inventory.MoonDew, "VFX_shadow_bolt", projSpeed: 5.5f, pref: 4.5f),
                EnemyX("eclipse_knight", "月蝕騎士", EnemyBehaviour.Charger, 190, 25, 1.4f, 0.9f, 3.2f, 0.55f, 4, 5, 0.5f, Inventory.CoreShard, kbResist: 0.6f),
                EnemyX("thorn_wisp", "荊棘幽光", EnemyBehaviour.Flanker, 44, 12, 3.6f, 0.5f, 2.3f, 0.25f, 1, 10, 0.3f, Inventory.Spore),
                EnemyX("moon_elite", "黑月精英", EnemyBehaviour.Elite, 520, 19, 1.4f, 1f, 2.2f, 0.7f, 15, 6, 1f, Inventory.CoreShard, "VFX_shadow_bolt", "shade_slime", kbResist: 0.8f, projSpeed: 6f, elite: true),
                EnemyX("twisted_grimoire", "扭曲魔典", EnemyBehaviour.GrimoireBoss, 3600, 20, 1.0f, 2f, 2.4f, 1.2f, 0, 6, 0, Inventory.CoreShard, "VFX_shadow_bolt", "thorn_wisp", "void_spark", 1f, 5f, boss: true),
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
                Card("U16", "稜光聚焦", "稜光射線寬度 +0.15、射程 +1。", UpgradeKind.Active, 9, 3, "prism_beam", null, "ICON_prism_beam"),
                Card("U17", "荊棘蔓延", "荊棘毒花範圍 +25%。", UpgradeKind.Auto, 9, 3, "thorn_bloom", null, "ICON_thorn_bloom"),
                Card("U18", "深影詛咒", "暗影之手範圍 +20%、傷害 +15%。", UpgradeKind.Active, 9, 3, "shadow_hand", null, "ICON_shadow_hand"),
                Card("U19", "重重迴響", "月影迴響多一次回聲（上限 2）。", UpgradeKind.Active, 8, 1, "moon_echo", null, "ICON_moon_echo"),
                Card("U20", "星界連鎖", "星彈反彈次數 +1。", UpgradeKind.Active, 9, 2, "star_shot", null, "ICON_star_shot"),
            };
            var chapters = new List<ChapterDefinition>
            {
                // chapter 1 runs in Moonveil Forest (World Studio region; replaces the old 64x64 forest, DECISIONS WVO-1)
                Chapter("forest", 1, "MoonveilForest", "紫幻森林", "mushroom_king", "月蝕菇王", "首領空地", "spore_elite", "defeat_mushroom_king", "",
                    "紫幻森林　森林入口", "調查森林深處的異常魔力", new string[0], "", 1f, false),
                Chapter("mine", 2, "Mine", "星核礦坑", "crystal_golem", "晶核魔像", "核心礦室", "crystal_elite", "defeat_crystal_golem", "defeat_mushroom_king",
                    "星核礦坑　Z01 礦口", "穿過礦坑，前往核心礦室", new[] { "prism_beam", "thorn_bloom" }, "（光束形態・自然學派）", 1.3f, false,
                    ("Z01", new[] { "ore_bat", "crystal_mite" }, new[] { 6f, 4f }),
                    ("Z02", new[] { "ore_bat", "crystal_mite", "gem_spitter" }, new[] { 4f, 4f, 4f }),
                    ("Z03", new[] { "crystal_mite", "gem_spitter", "rock_golemling", "ore_bat" }, new[] { 4f, 4f, 2f, 3f }),
                    ("Z04", new[] { "gem_spitter", "rock_golemling", "ore_bat", "crystal_mite" }, new[] { 3f, 3f, 3f, 3f }),
                    ("Z06", new[] { "ore_bat", "crystal_mite" }, new[] { 5f, 3f })),
                Chapter("lake", 3, "Lake", "星輝湖", "lake_shadow", "湖影", "湖心", "shadow_elite", "defeat_lake_shadow", "defeat_crystal_golem",
                    "星輝湖　Z01 湖畔營地", "點亮導電塔，渡過浮島前往湖心", new[] { "shadow_hand", "moon_echo" }, "（月影學派・迴響修飾）", 1.6f, false,
                    ("Z01", new[] { "lake_slime", "moon_moth" }, new[] { 7f, 3f }),
                    ("Z02", new[] { "lake_slime", "moon_moth", "mire_frog" }, new[] { 4f, 3f, 4f }),
                    ("Z03", new[] { "moon_moth", "mire_frog", "reflection_knight", "lake_slime" }, new[] { 3f, 4f, 2f, 3f }),
                    ("Z04", new[] { "lake_slime", "mire_frog", "moon_moth", "reflection_knight" }, new[] { 3f, 3f, 3f, 2f }),
                    ("Z06", new[] { "lake_slime", "mire_frog" }, new[] { 4f, 4f })),
                Chapter("observatory", 4, "Observatory", "失落觀星台", "star_guardian", "觀星儀守護者", "塔頂", "gear_elite", "defeat_star_guardian", "defeat_lake_shadow",
                    "失落觀星台　Z01 山腳", "沿雪道上行，調整折射鏡，登上塔頂", new[] { "star_shot" }, "（星痕學派；研究桌新增「失控超載」）", 1.9f, false,
                    ("Z01", new[] { "snow_slime", "frost_wisp" }, new[] { 7f, 3f }),
                    ("Z02", new[] { "snow_slime", "frost_wisp", "lens_turret" }, new[] { 4f, 4f, 2f }),
                    ("Z03", new[] { "frost_wisp", "lens_turret", "clockwork_hound", "snow_slime" }, new[] { 3f, 3f, 2f, 3f }),
                    ("Z04", new[] { "clockwork_hound", "lens_turret", "frost_wisp", "snow_slime" }, new[] { 3f, 3f, 3f, 3f }),
                    ("Z06", new[] { "snow_slime", "frost_wisp" }, new[] { 4f, 4f })),
                Chapter("citadel", 5, "Citadel", "黑月城塞", "twisted_grimoire", "扭曲魔典", "中央大廳", "moon_elite", "defeat_twisted_grimoire", "defeat_star_guardian",
                    "黑月城塞　Z01 城門", "完成三座古塔的試煉，進入中央大廳", new string[0], "", 2.2f, true,
                    ("Z01", new[] { "shade_slime", "void_spark" }, new[] { 6f, 3f }),
                    ("Z02", new[] { "shade_slime", "thorn_wisp", "dark_acolyte" }, new[] { 4f, 4f, 3f }),
                    ("Z03", new[] { "dark_acolyte", "void_spark", "eclipse_knight", "shade_slime" }, new[] { 4f, 3f, 2f, 3f }),
                    ("Z04", new[] { "eclipse_knight", "dark_acolyte", "void_spark", "shade_slime" }, new[] { 3f, 3f, 3f, 3f }),
                    ("Z06", new[] { "shade_slime", "dark_acolyte" }, new[] { 4f, 4f })),
            };
            int qo = 0;
            var M = QuestKind.Main;
            var S = QuestKind.Side;
            var quests = new List<QuestDefinition>
            {
                // existing chapter-1 flags (STORY_WORLD_QUESTS §4.1) keep their ids so old saves keep progress
                Quest(qo++, "M1-01", "forest", M, "拜訪艾拉", "在村莊廣場找艾拉，聽她說明魔典的異常。", "艾拉", "talked_ella", null),
                Quest(qo++, "M1-02", "forest", M, "魔典台的初次書頁", "回魔女小屋，在魔典台把法術放進槽位。", "艾拉", "configure_first_spell", "talked_ella"),
                Quest(qo++, "M1-03", "forest", M, "擊敗月蝕菇王", "從村莊南路進入紫幻森林，登上東北懸崖的首領空地，擊敗月蝕菇王。", "艾拉", "defeat_mushroom_king", "configure_first_spell"),
                Quest(qo++, "M1-04", "forest", M, "研究殘頁", "帶著材料回研究桌，完成一次研究。", "艾拉", "research_after_victory", "defeat_mushroom_king"),
                Quest(qo++, "S1-01", "forest", S, "騎士的訓練", "到訓練場和凱恩談談近戰的訣竅。", "凱恩", "talked_kain", null),
                // ch.2
                Quest(qo++, "M2-01", "mine", M, "研讀殘頁星圖", "菇王的殘頁畫著礦坑的星圖。去找艾拉。", "艾拉", "talked_ella_mine", "defeat_mushroom_king"),
                Quest(qo++, "M2-02", "mine", M, "凱恩帶路", "凱恩知道礦坑的入口。在傳送星盤選擇「星核礦坑」。", "凱恩", "talked_kain_mine", "talked_ella_mine"),
                Quest(qo++, "S2-01", "mine", S, "受困的礦工", "礦坑西側的死巷裡有礦工被困住了。", "礦工杜克", "rescue_miner", "defeat_mushroom_king"),
                Quest(qo++, "M2-03", "mine", M, "擊敗晶核魔像", "用礦車炸開落石，點亮導電水晶，擊敗核心礦室的晶核魔像。小心：雷擊會替它充能。", "凱恩", "defeat_crystal_golem", "defeat_mushroom_king"),
                // ch.3
                Quest(qo++, "M3-01", "lake", M, "湖畔營地調查", "在湖畔營地和艾拉會合。", "艾拉", "talked_ella_lake", "defeat_crystal_golem"),
                Quest(qo++, "S3-01", "lake", S, "湖岸殘頁", "東浮島岸邊漂來一頁發光的紙。", "", "lake_page", "defeat_crystal_golem"),
                Quest(qo++, "M3-02", "lake", M, "擊敗湖影", "點亮導電塔架起光橋，渡過浮島，擊敗湖心的湖影。它會模仿你的構築。", "艾拉", "defeat_lake_shadow", "defeat_crystal_golem"),
                // ch.4
                Quest(qo++, "M4-01", "observatory", M, "國王的委託", "王子帶來國王的委託：調查失落觀星台。", "王子", "talked_prince_observatory", "defeat_lake_shadow"),
                Quest(qo++, "S4-01", "observatory", S, "舊觀測紀錄", "雪道旁的小屋裡留著第一位星痕魔女的紀錄。", "", "observatory_records", "defeat_lake_shadow"),
                Quest(qo++, "M4-02", "observatory", M, "擊敗觀星儀守護者", "轉動折射鏡把星光導向門扉，登上塔頂擊敗觀星儀守護者。", "王子", "defeat_star_guardian", "defeat_lake_shadow"),
                // finale
                Quest(qo++, "M5-01", "citadel", M, "月蝕降臨", "黑月升起。和艾拉談過後，從傳送星盤前往黑月城塞。", "艾拉", "talked_ella_citadel", "defeat_star_guardian"),
                Quest(qo++, "S5-01", "citadel", S, "古塔側室", "東塔旁的側室藏著第一位魔女的遺言。", "", "citadel_side_room", "defeat_star_guardian"),
                Quest(qo++, "M5-02", "citadel", M, "擊敗扭曲魔典", "完成自然、月影、星痕三座古塔的試煉，進入中央大廳擊敗扭曲魔典。", "艾拉", "defeat_twisted_grimoire", "defeat_star_guardian"),
                Quest(qo++, "M5-03", "citadel", M, "魔典的抉擇", "決定魔典的命運。", "", "story_complete", "defeat_twisted_grimoire"),
            };
            return Upsert<GameDatabase>(DatabasePath, db =>
            {
                db.sprites = manifestItems.Select(it => new SpriteSet { name = it.name, fps = it.fps, walkFrames = it.walkFrames > 0 ? it.walkFrames : 4, frames = sets[it.name] }).ToList();
                db.spells = spells;
                db.enemies = enemies;
                db.upgrades = cards;
                db.chapters = chapters;
                db.quests = quests;
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
            public readonly Dictionary<string, BiomeTiles> biomes = new Dictionary<string, BiomeTiles>();
        }

        /// <summary>Chapter 2-5 biome tiles (tools/art/chapter_art.py): 4 ground variants, path and water
        /// 256-mask autotiles, 4 solid (impassable rock / ruin top) variants.</summary>
        class BiomeTiles { public TileBase ground, path, water, solid; }

        static readonly string[] ChapterBiomes = { "mine", "lake", "observatory", "citadel" };

        static string BiomeFolder(string b) => char.ToUpperInvariant(b[0]) + b.Substring(1);

        /// <summary>Light, ambience and backdrop per biome: (global light colour, intensity, camera colour, character dim).</summary>
        static (Color light, float intensity, Color32 back) BiomeLook(string biome) => biome switch
        {
            "forest" => (new Color(0.86f, 0.9f, 1f), 0.82f, new Color32(0x10, 0x1c, 0x18, 0xff)),
            "interior" => (new Color(1f, 0.96f, 0.9f), 1f, new Color32(0x1b, 0x12, 0x1a, 0xff)),
            "mine" => (new Color(0.78f, 0.78f, 1f), 0.72f, new Color32(0x14, 0x10, 0x22, 0xff)),
            "lake" => (new Color(0.8f, 0.9f, 1f), 0.8f, new Color32(0x0e, 0x18, 0x2c, 0xff)),
            "observatory" => (new Color(0.95f, 0.97f, 1f), 0.95f, new Color32(0x22, 0x26, 0x36, 0xff)),
            "citadel" => (new Color(0.82f, 0.7f, 1f), 0.7f, new Color32(0x12, 0x0a, 0x1c, 0xff)),
            _ => (new Color(0.97f, 0.93f, 1f), 0.9f, new Color32(0x1b, 0x1d, 0x2b, 0xff)),
        };

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
            foreach (var b in ChapterBiomes)
            {
                string dir = $"Assets/_Game/Art/Tilesets/{BiomeFolder(b)}/Draft/";
                if (!File.Exists(dir + $"TILE_{b}_ground_draft.png"))
                    continue;
                var g = Slice(dir + $"TILE_{b}_ground_draft.png", T, T, 4, 1, c);
                var pa = Slice(dir + $"TILE_{b}_path_draft_256.png", T, T, 16, 16, c);
                var wa = Slice(dir + $"TILE_{b}_water_draft_256.png", T, T, 16, 16, c);
                var so = Slice(dir + $"TILE_{b}_solid_draft.png", T, T, 4, 1, c);
                string o = tileOut + BiomeFolder(b) + "/";
                set.biomes[b] = new BiomeTiles
                {
                    ground = Upsert<VariantTile>(o + $"TL_{b}_ground_draft.asset", t => t.sprites = g),
                    path = Upsert<NeighborMaskTile>(o + $"TL_{b}_path_draft.asset", t => { t.mode = NeighborMaskMode.Full8; t.sprites = pa; }),
                    water = Upsert<NeighborMaskTile>(o + $"TL_{b}_water_draft.asset", t => { t.mode = NeighborMaskMode.Full8; t.sprites = wa; }),
                    solid = Upsert<VariantTile>(o + $"TL_{b}_solid_draft.asset", t => t.sprites = so),
                };
            }
            var dbg = AssetDatabase.LoadAssetAtPath<Sprite>(DebugTileArt);
            set.block = Upsert<Tile>("Assets/_Game/Tiles/Debug/TL_collision_block.asset", t => { t.sprite = dbg; t.colliderType = Tile.ColliderType.Grid; });
            return set;
        }

        static TileSet LoadTiles()
        {
            TT L<TT>(string p) where TT : TileBase => AssetDatabase.LoadAssetAtPath<TT>("Assets/_Game/Tiles/" + p + ".asset");
            var set = new TileSet
            {
                grass = L<TileBase>("Village/TL_grass_draft"), path = L<TileBase>("Village/TL_path_draft"), water = L<TileBase>("Village/TL_water_draft"),
                waterfall = L<TileBase>("Village/TL_waterfall_tall_draft"), face = L<TileBase>("Village/TL_cliff_face_draft"), rim = L<TileBase>("Village/TL_plateau_rim_draft"),
                fGrass = L<TileBase>("Forest/TL_forest_grass_draft"), fPath = L<TileBase>("Forest/TL_forest_path_draft"), fWater = L<TileBase>("Forest/TL_forest_water_draft"),
                floor = L<TileBase>("Interior/TL_floor_draft"), wallTop = L<TileBase>("Interior/TL_wall_top_draft"), wallFace = L<TileBase>("Interior/TL_wall_face_draft"),
                block = L<TileBase>("Debug/TL_collision_block"),
            };
            foreach (var b in ChapterBiomes)
            {
                string o = BiomeFolder(b) + $"/TL_{b}_";
                var ground = L<TileBase>(o + "ground_draft");
                if (ground != null)
                    set.biomes[b] = new BiomeTiles { ground = ground, path = L<TileBase>(o + "path_draft"), water = L<TileBase>(o + "water_draft"), solid = L<TileBase>(o + "solid_draft") };
            }
            return set;
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
            AddContactShadow(ctx, go.transform, p, sr.sprite);
            // Phase-0 prefab names kept for the north-block props (same sprite + same box + fade)
            bool first = ctx.prefabFiles.Add(p.sprite);
            string file = first ? p.sprite : key.Replace('.', '_');
            if (!first && !ctx.prefabFiles.Add(file))
                file += "_plain"; // the sprite's first variant (e.g. a village lamp with a collider) keeps the plain name
            ctx.prefabFiles.Add(file);
            var path = PropPrefabOut + file + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            ctx.prefabs[key] = prefab;
            return prefab;
        }

        /// <summary>World Studio props that stand on the ground (trees near clearings, rocks, ruins, camp
        /// pieces: the ones with a collider or occlusion fade) get a flat contact shadow on GroundDecal,
        /// sized from the collider or sprite width (MISSING_ASSETS §2 接地陰影). Deep-forest trees have
        /// neither, so the dense canopy adds no extra renderers.</summary>
        static void AddContactShadow(Ctx ctx, Transform prop, PropDef p, Sprite sprite)
        {
            if (sprite == null || !p.sprite.StartsWith("PROP_mv_") || p.sprite.Contains("bridge") || p.sprite.Contains("shadow"))
                return;
            bool hasBox = p.box != null && p.box.Length == 4;
            if (!hasBox && !p.fade)
                return;
            float want = Mathf.Max(hasBox ? p.box[0] * 32f * 1.25f : 0f, sprite.rect.width * 0.42f);
            string name = want < 32 ? "PROP_mv_shadow_s" : want < 52 ? "PROP_mv_shadow_m" : want < 80 ? "PROP_mv_shadow_l" : "PROP_mv_shadow_xl";
            if (!ctx.HasSprite(name))
                return;
            var shadowSprite = ctx.Sprite(name);
            var go = new GameObject("Shadow");
            go.transform.SetParent(prop, false);
            go.transform.localPosition = new Vector3(hasBox ? p.box[2] : 0f, 0.06f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = shadowSprite;
            sr.sortingLayerName = StarbindLayers.SortGroundDecal;
            sr.sortingOrder = 60;
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
                AddLight(prop, new Vector2(x, y), color, radius, intensity);
        }

        static void AddLight(Transform prop, Vector2 offset, Color color, float radius, float intensity)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(prop, false);
            go.transform.localPosition = new Vector3(offset.x, offset.y, 0);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.falloffIntensity = 0.7f;
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

        static void SetStrings(UnityEngine.Object target, string field, string[] values)
        {
            var so = new SerializedObject(target);
            var p = so.FindProperty(field);
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).stringValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
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
            ctx.rawJson = File.ReadAllText(jsonPath);
            var look = BiomeLook(map.biome);
            var sb = string.IsNullOrEmpty(map.biomeAsset) ? null : AssetDatabase.LoadAssetAtPath<BiomeDefinition>(map.biomeAsset);
            if (!string.IsNullOrEmpty(map.biomeAsset) && sb == null)
                throw new KeyError("biome asset missing: " + map.biomeAsset);
            if (sb != null)
                look = (sb.globalLight, sb.globalIntensity, (Color32)sb.background);

            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            // village: a touch of lavender dusk so lanterns, windows and crystals read as light sources
            light.intensity = look.intensity;
            light.color = look.light;

            var cfg = new GameObject("SceneConfig").AddComponent<SceneConfig>();
            cfg.title = map.scene.title;
            cfg.characterDim = map.scene.dim;
            cfg.safeZone = map.scene.safe;
            cfg.objective = map.scene.objective;
            cfg.chapterId = map.scene.chapter ?? "";
            cfg.runIntro = map.scene.intro ?? "";
            cfg.runObjective = map.scene.runObjective ?? "";

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
            t.biomes.TryGetValue(map.biome ?? "", out var bt);
            var baseTile = sb != null ? sb.ground : bt != null ? bt.ground : interior ? t.floor : forest ? t.fGrass : t.grass;
            var cells = new List<Vector3Int>(W * H);
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    cells.Add(new Vector3Int(x, y, 0));
            ground.SetTiles(cells.ToArray(), Enumerable.Repeat(baseTile, cells.Count).ToArray());
            var L = map.layers;
            if (sb != null)
            {
                // World Studio layers (docs/WORLD_GENERATOR_SPEC.md §5): dark floor 5, water 10, path 20, paver 22, detail 25, cliff 30
                var dark = Layer(grid, "GroundDark", 5);
                var paver = Layer(grid, "Paver", 22);
                var detail = Layer(grid, "Detail", 25);
                if (L.dark != null) Paint(L.dark, H, dark, c => c == 'd' ? sb.darkGround : null);
                if (L.water != null) Paint(L.water, H, water, c => c == '~' ? sb.water : null);
                if (L.path != null) Paint(L.path, H, path, c => c == 'p' ? sb.path : null);
                if (L.path != null) Paint(L.path, H, paver, c => c == 'v' ? sb.paver : null);
                if (L.detail != null) Paint(L.detail, H, detail, c => c >= '1' && c <= '9' && c - '1' < sb.details.Length ? sb.details[c - '1'] : null);
                if (L.cliff != null) Paint(L.cliff, H, cliff, c => c == 'R' ? sb.cliffRim : c == 'F' ? sb.cliffFace : c == 'W' ? sb.cliffWaterfall : c == 'S' ? sb.cliffStairs : null);
                L = new Layers { collision = L.collision, spawn = L.spawn };
            }
            if (L.water != null) Paint(L.water, H, water, c => c == '~' ? (bt != null ? bt.water : forest ? t.fWater : t.water) : null);
            if (L.path != null) Paint(L.path, H, path, c => c == 'p' ? (bt != null ? bt.path : forest ? t.fPath : t.path) : null);
            if (L.solid != null && bt != null) Paint(L.solid, H, cliff, c => c == 'S' ? bt.solid : null);
            if (L.cliff != null) Paint(L.cliff, H, cliff, c => c == 'R' ? t.rim : c == 'F' ? t.face : c == 'W' ? t.waterfall : null);
            if (L.wall != null) Paint(L.wall, H, cliff, c => c == 'T' ? t.wallTop : c == 'F' ? t.wallFace : null);
            if (L.collision != null) Paint(L.collision, H, collision, c => c == '#' ? t.block : null);
            Debug.Log($"[GameContentBuilder] {map.id}: water {water.GetUsedTilesCount()} path {path.GetUsedTilesCount()} cliff/wall {cliff.GetUsedTilesCount()} collision {collision.GetUsedTilesCount()} (rows {L.collision?.Length})");

            // ---- props
            var world = new GameObject("World").transform;
            var props = new GameObject("Props").transform;
            props.SetParent(world, false);
            var decals = new GameObject("Decals").transform;
            var chunks = new Dictionary<Vector2Int, Transform>();
            foreach (var p in map.props)
            {
                if (!string.IsNullOrEmpty(p.layer))
                {
                    Decal(ctx, decals, p.sprite, new Vector2(p.x, p.y), p.layer, p.order);
                    continue;
                }
                var parent = props;
                if (sb != null)
                {
                    // 16x16-tile chunks keep the hierarchy navigable on large generated maps
                    var key = new Vector2Int(Mathf.FloorToInt(p.x / 16f), Mathf.FloorToInt(p.y / 16f));
                    if (!chunks.TryGetValue(key, out parent))
                    {
                        parent = new GameObject($"Chunk_{key.x}_{key.y}").transform;
                        parent.SetParent(props, false);
                        chunks[key] = parent;
                    }
                }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(PropPrefab(ctx, p), parent);
                go.transform.position = new Vector3(p.x, p.y, 0);
                AddPropLights(go.transform, p.sprite);
                if (p.light != null && p.light.Length == 7)
                    AddLight(go.transform, new Vector2(p.light[5], p.light[6]), new Color(p.light[0], p.light[1], p.light[2]), p.light[3], p.light[4]);
                if (p.door != null && !string.IsNullOrEmpty(p.door.target))
                {
                    var door = Trigger(go.transform, "Door", new Vector2(p.x + p.door.dx, p.y + p.door.dy), 0.45f, "cottage_door", 1.55f);
                    door.AddComponent<DoorPortal>().Configure(p.door.target, p.door.spawn);
                }
            }

            if (sb != null)
            {
                if (sb.ambientMotes > 0)
                {
                    var motes = new GameObject("AmbientMotes").AddComponent<AmbientMotes>();
                    motes.transform.SetParent(world, false);
                    motes.Configure(ctx.Sprite("DEC_mote_draft"), ctx.mats.unlit, sb.ambientMotes);
                }
            }
            else if (!forest && !interior && map.biome != "mine")
            {
                var motes = new GameObject("AmbientMotes").AddComponent<AmbientMotes>();
                motes.transform.SetParent(world, false);
                motes.Configure(ctx.Sprite(map.biome == "observatory" ? "DEC_snowflake_draft" : "DEC_mote_draft"), ctx.mats.unlit, map.biome == "observatory" ? 40 : 26);
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
            cam.backgroundColor = look.back;
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
                    var tk = talk.AddComponent<Talkable>();
                    tk.Configure(o.name, o.lines, o.after, o.quest, o.heals);
                    tk.ConfigureStages(Stages(o), o.reward, o.requires, o.locked);
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
                    t.AddComponent<RegionSelector>().Configure(o.target);
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
                    var p = new PropDef { sprite = string.IsNullOrEmpty(o.sprite) ? "PROP_altar_draft" : o.sprite, x = o.x, y = o.y, box = new[] { 1.4f, 0.5f, 0, 0.3f } };
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(PropPrefab(ctx, p), world);
                    go.transform.position = pos;
                    var t = Trigger(objects, "StarAltar", pos + new Vector2(0, -0.2f), 1.2f, "star_altar", 1.9f);
                    var altar = t.AddComponent<StarAltar>();
                    if (o.wave > 0)
                        SetField(altar, "waveSize", o.wave);
                    if (!string.IsNullOrEmpty(o.notice))
                        SetString(altar, "notice", o.notice);
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
                    string vineSprite = string.IsNullOrEmpty(o.sprite) ? "PROP_vine_draft" : o.sprite;
                    var sr = Decal(ctx, world, vineSprite, pos, StarbindLayers.SortWorld, 0, "BurnableVine");
                    int vines = Mathf.Max(1, Mathf.RoundToInt(o.block[0]));
                    for (int ix = 1; ix < vines; ix++) // extra whole vine sprites across the path, alternating sides
                        Decal(ctx, sr.transform, vineSprite, pos + new Vector2((ix + 1) / 2 * (ix % 2 == 1 ? 1f : -1f), 0), StarbindLayers.SortWorld, 0, "Vine");
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
                    if (!string.IsNullOrEmpty(o.openSprite))
                        SetString(v, "burntSprite", o.openSprite);
                    if (o.frames != null && o.frames.Length > 0)
                        SetStrings(v, "burnFrames", o.frames);
                    break;
                }
                case "bridge_stone":
                {
                    var bridge = Decal(ctx, objects, string.IsNullOrEmpty(o.sprite) ? "PROP_bridge_stone_broken_draft" : o.sprite, pos, StarbindLayers.SortGround, 35, "StoneBridge");
                    // World Studio bridges: the gap covers the bridge cells (rect), the sprite stands at its pivot
                    var gapCentre = o.rect != null && o.rect.Length == 4 ? new Vector2(o.rect[0] + o.rect[2] * 0.5f, o.rect[1] + o.rect[3] * 0.5f) : pos;
                    var gap = Blocker(objects, "BridgeGap", gapCentre, new Vector2(o.gap[0], o.gap[1]));
                    var leverPos = new Vector2(o.lever[0], o.lever[1]);
                    var lever = Decal(ctx, world, string.IsNullOrEmpty(o.leverSprite) ? "PROP_lever_draft" : o.leverSprite, leverPos, StarbindLayers.SortWorld, 0, "BridgeLever");
                    var t = Trigger(lever.transform, "Pull", leverPos, 0.6f, "bridge_lever", 1.1f);
                    var sw = t.AddComponent<BridgeSwitch>();
                    sw.Configure(lever, bridge, gap, string.IsNullOrEmpty(o.openSprite) ? "PROP_bridge_stone_draft" : o.openSprite);
                    Set(sw, "lever", lever);
                    Set(sw, "bridge", bridge);
                    Set(sw, "gap", gap);
                    SetString(sw, "repairedSprite", string.IsNullOrEmpty(o.openSprite) ? "PROP_bridge_stone_draft" : o.openSprite);
                    if (!string.IsNullOrEmpty(o.leverOnSprite))
                        SetString(sw, "leverOnSprite", o.leverOnSprite);
                    break;
                }
                case "boss_arena":
                {
                    var go = new GameObject("BossArena");
                    go.transform.SetParent(objects, false);
                    var gates = new List<Collider2D>();
                    foreach (var g in ParseGates(ctx.rawJson))
                    {
                        var gate = Blocker(go.transform, "Gate", new Vector2(g[0], g[1]), new Vector2(g[2], g[3]));
                        // whole vine sprites side by side (no stretching: keeps the pixel grid)
                        int across = Mathf.Max(1, Mathf.RoundToInt(g[2])), down = Mathf.Max(1, Mathf.RoundToInt(g[3] / 1.2f));
                        for (int ix = 0; ix < across; ix++)
                            for (int iy = 0; iy < down; iy++)
                                Decal(ctx, gate.transform, string.IsNullOrEmpty(o.sprite) ? "PROP_vine_draft" : o.sprite,
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
                case "gate":
                {
                    var block = Blocker(objects, "Gate_" + o.gate, pos, new Vector2(o.w, o.h));
                    var sprites = new List<SpriteRenderer>();
                    if (o.span != null && o.span.Length == 4)
                    {
                        // visuals along a line (light bridges): ground decals, blocker only at the bridge head
                        int n = Mathf.Max(1, o.count);
                        var a = new Vector2(o.span[0], o.span[1]);
                        var b = new Vector2(o.span[2], o.span[3]);
                        for (int i = 0; i < n; i++)
                            sprites.Add(Decal(ctx, block.transform, o.sprite, Vector2.Lerp(a, b, n == 1 ? 0.5f : i / (float)(n - 1)), StarbindLayers.SortGround, 36, "GateVisual"));
                    }
                    else
                    {
                        int across = Mathf.Max(1, o.count > 0 ? o.count : Mathf.RoundToInt(Mathf.Max(o.w, o.h)));
                        bool vertical = o.h > o.w;
                        for (int i = 0; i < across; i++)
                        {
                            // sprites stand on their bottom pivot: spread along the long side of the blocker
                            var at = vertical
                                ? new Vector2(pos.x, pos.y - o.h * 0.5f + i * (o.h / across))
                                : new Vector2(pos.x - o.w * 0.5f + (i + 0.5f) * (o.w / across), pos.y - o.h * 0.5f);
                            sprites.Add(Decal(ctx, block.transform, o.sprite, at, StarbindLayers.SortWorld, 0, "GateVisual"));
                        }
                    }
                    block.gameObject.AddComponent<PowerGate>().Configure(o.gate, block, sprites.ToArray(), o.openSprite, o.notice);
                    break;
                }
                case "crystal":
                {
                    var sr = Decal(ctx, world, string.IsNullOrEmpty(o.sprite) ? "PROP_crystal_node_draft" : o.sprite, pos, StarbindLayers.SortWorld, 0, "Crystal_" + o.circuit);
                    var go = sr.gameObject;
                    go.layer = LayerMask.NameToLayer(StarbindLayers.WorldSolid);
                    var b = go.AddComponent<BoxCollider2D>();
                    b.size = new Vector2(0.6f, 0.35f);
                    b.offset = new Vector2(0, 0.18f);
                    var h = go.AddComponent<Health>();
                    SetField(h, "team", (int)Team.Neutral);
                    SetField(h, "maxHp", 9999f);
                    SetField(h, "radius", 0.5f);
                    SetVec(h, "centerOffset", new Vector2(0, 0.6f));
                    SetField(h, "immortal", 1f);
                    Trigger(go.transform, "Charge", pos + new Vector2(0, -0.1f), 0.75f, "crystal", 1.5f);
                    go.AddComponent<ConductiveCrystal>().Configure(o.circuit, sr, string.IsNullOrEmpty(o.litSprite) ? "PROP_crystal_node_lit_draft" : o.litSprite);
                    break;
                }
                case "cart":
                {
                    var stop = new Vector2(o.stop[0], o.stop[1]);
                    var dir = (stop - pos).normalized;
                    float len = Vector2.Distance(pos, stop);
                    for (float d = 0; d <= len + 0.01f; d += 1f)
                        Decal(ctx, objects, Mathf.Abs(dir.x) > Mathf.Abs(dir.y) ? "PROP_rail_h_draft" : "PROP_rail_v_draft", pos + dir * d, StarbindLayers.SortGround, 34, "Rail");
                    var cart = Decal(ctx, world, "PROP_minecart_draft", pos, StarbindLayers.SortWorld, 0, "MineCart");
                    var t = Trigger(objects, "PushCart", pos + new Vector2(0, -0.2f), 0.8f, "minecart", 1.4f);
                    t.AddComponent<MineCart>().Configure(cart.transform, stop, o.gate);
                    break;
                }
                case "beam":
                {
                    Decal(ctx, world, "PROP_beam_emitter_draft", new Vector2(o.x + 0.5f, o.y + 0.1f), StarbindLayers.SortWorld, 0, "BeamEmitter");
                    Decal(ctx, world, "PROP_beam_receiver_draft", new Vector2(o.tx + 0.5f, o.ty + 0.1f), StarbindLayers.SortWorld, 0, "BeamReceiver");
                    var mirrors = new List<BeamMirror>();
                    foreach (var m in o.mirrors ?? Array.Empty<MirrorDef>())
                    {
                        var mp = new Vector2(m.x + 0.5f, m.y + 0.2f);
                        var sr = Decal(ctx, world, "PROP_mirror_draft", mp, StarbindLayers.SortWorld, 0, "Mirror");
                        var trig = Trigger(sr.transform, "Turn", mp, 0.7f, "mirror", 1.4f);
                        var bm = trig.AddComponent<BeamMirror>();
                        bm.Configure(new Vector2Int(m.x, m.y), m.kind, sr);
                        mirrors.Add(bm);
                    }
                    var go = new GameObject("BeamPuzzle_" + o.gate);
                    go.transform.SetParent(objects, false);
                    go.AddComponent<BeamPuzzle>().Configure(new Vector2Int((int)o.x, (int)o.y), new Vector2Int(o.dirX, o.dirY), new Vector2Int(o.tx, o.ty), o.gate, mirrors.ToArray());
                    break;
                }
                case "trial":
                {
                    var p = new PropDef { sprite = string.IsNullOrEmpty(o.sprite) ? "PROP_trial_seal_draft" : o.sprite, x = o.x, y = o.y, box = new[] { 1.2f, 0.45f, 0, 0.25f } };
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(PropPrefab(ctx, p), world);
                    go.transform.position = pos;
                    var t = Trigger(objects, "Trial_" + o.school, pos + new Vector2(0, -0.3f), 1.1f, "trial_seal", 2f);
                    t.AddComponent<TrialSeal>().Configure(o.gate, o.school, o.enemies, o.wave > 0 ? o.wave : 18);
                    break;
                }
                case "note":
                {
                    if (!string.IsNullOrEmpty(o.sprite))
                    {
                        var p = new PropDef { sprite = o.sprite, x = o.x, y = o.y, box = new[] { 0.6f, 0.3f, 0, 0.15f } };
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(PropPrefab(ctx, p), world);
                        go.transform.position = pos;
                    }
                    var t = Trigger(objects, "Note_" + o.id, pos + new Vector2(0, 0.1f), 0.7f, "note", 1.3f);
                    var tk = t.AddComponent<Talkable>();
                    tk.Configure(o.name, o.lines, null, o.quest);
                    tk.ConfigureStages(Stages(o), o.reward, o.requires, o.locked);
                    break;
                }
                default:
                    Debug.LogWarning("[GameContentBuilder] unknown object kind " + o.kind);
                    break;
            }
        }

        static StageLines[] Stages(ObjDef o) =>
            (o.stages ?? Array.Empty<StageDef>()).Select(st => new StageLines { flag = st.flag, quest = st.quest, lines = st.lines }).ToArray();

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
