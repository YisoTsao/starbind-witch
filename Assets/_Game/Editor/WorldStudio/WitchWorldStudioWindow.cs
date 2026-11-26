using System.Linq;
using Starbind.World.Studio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Starbind.Editor.WorldStudio
{
    /// <summary>Tools / Witch World / World Studio: pick a region blueprint and seed, preview the generated
    /// layers (no scene changes), validate, then build the scene. Same seed = same map.</summary>
    public sealed class WitchWorldStudioWindow : EditorWindow
    {
        RegionBlueprint blueprint;
        int seed;
        Texture2D preview;
        WorldValidation report;
        string log = "";
        Vector2 scroll;
        int zoom = 3;

        [MenuItem("Tools/Witch World/World Studio")]
        public static void Open() => GetWindow<WitchWorldStudioWindow>("World Studio");

        [MenuItem("Tools/Witch World/Rebuild Moonveil Forest")]
        public static void RebuildMoonveil()
        {
            var bp = MoonveilPreset.Apply();
            WorldStudioPipeline.GenerateAndBuild(bp, bp.seed);
        }

        void OnEnable()
        {
            if (blueprint == null)
                blueprint = AssetDatabase.LoadAssetAtPath<RegionBlueprint>(MoonveilPreset.BlueprintPath);
            if (blueprint != null && seed == 0)
                seed = blueprint.seed;
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Witch World Studio", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Blueprint = macro layout (POIs, roads, water, cliffs). Biome = tiles, props, rules. " +
                                    "Generate previews without touching scenes; Build writes Data/Maps/<id>.json and Scenes/Regions/<scene>.unity.", MessageType.None);
            var bp = (RegionBlueprint)EditorGUILayout.ObjectField("Region Blueprint", blueprint, typeof(RegionBlueprint), false);
            if (bp != blueprint)
            {
                blueprint = bp;
                if (bp != null)
                    seed = bp.seed;
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                seed = EditorGUILayout.IntField("Seed", seed);
                if (GUILayout.Button("Blueprint seed", GUILayout.Width(110)) && blueprint != null)
                    seed = blueprint.seed;
                if (GUILayout.Button("Random", GUILayout.Width(70)))
                    seed = Random.Range(1, int.MaxValue);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create / Update Moonveil Preset"))
                {
                    blueprint = MoonveilPreset.Apply();
                    seed = blueprint.seed;
                }
                GUI.enabled = blueprint != null;
                if (GUILayout.Button("Generate Preview"))
                    RunPreview();
                if (GUILayout.Button("Build Scene"))
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        report = WorldStudioPipeline.GenerateAndBuild(blueprint, seed);
                        log = report.ToString();
                    }
                }
                if (GUILayout.Button("Open Scene") && System.IO.File.Exists(WorldStudioPipeline.ScenePath(blueprint)))
                    EditorSceneManager.OpenScene(WorldStudioPipeline.ScenePath(blueprint));
                GUI.enabled = true;
            }
            zoom = EditorGUILayout.IntSlider("Preview zoom", zoom, 1, 6);
            if (report != null)
                EditorGUILayout.HelpBox(log, report.Ok ? MessageType.Info : MessageType.Error);
            if (preview != null)
            {
                scroll = EditorGUILayout.BeginScrollView(scroll);
                var r = GUILayoutUtility.GetRect(preview.width * zoom, preview.height * zoom, GUILayout.ExpandWidth(false));
                GUI.DrawTexture(r, preview, ScaleMode.ScaleToFit, false);
                EditorGUILayout.EndScrollView();
            }
        }

        void RunPreview()
        {
            var (data, rep) = WorldStudioPipeline.Generate(blueprint, seed);
            report = rep;
            log = rep + "\n" + string.Join("\n", data.Log.TakeLast(6));
            preview = Render(data, blueprint.biome);
        }

        /// <summary>1 px per cell layer preview: forest, grass, dark floor, road, paver, water, bridge,
        /// cliff, props (dots) and POI centres.</summary>
        public static Texture2D Render(WorldMapData d, BiomeDefinition biome)
        {
            var px = new Color32[d.Width * d.Height];
            for (int y = 0; y < d.Height; y++)
                for (int x = 0; x < d.Width; x++)
                {
                    int k = d.Index(x, y);
                    Color32 c = d.Open[k] == 0 ? new Color32(0x3a, 0x22, 0x58, 255) : d.Ground[k] != 0 ? new Color32(0x3e, 0x52, 0x3c, 255) : new Color32(0x6a, 0x86, 0x4a, 255);
                    if (d.Surface[k] == SurfaceCell.Path) c = new Color32(0xb4, 0x82, 0x6c, 255);
                    if (d.Surface[k] == SurfaceCell.Paver) c = new Color32(0xa8, 0x98, 0xb8, 255);
                    if (d.Water[k] != 0) c = new Color32(0x2c, 0x3c, 0x9a, 255);
                    if (d.Bridge[k] != 0) c = new Color32(0x9a, 0x6a, 0x44, 255);
                    if (d.Cliff[k] == CliffCell.Face) c = new Color32(0x5a, 0x4a, 0x6e, 255);
                    if (d.Cliff[k] == CliffCell.Waterfall) c = new Color32(0x9a, 0xb0, 0xff, 255);
                    px[k] = c;
                }
            foreach (var p in d.Props)
            {
                var c = WorldMapData.Cell(p.position);
                if (d.In(c.x, c.y))
                    px[d.Index(c.x, c.y)] = p.source == "wall" ? new Color32(0x8e, 0x4c, 0xc8, 255) : new Color32(0xf0, 0xd0, 0x80, 255);
            }
            foreach (var poi in d.Pois)
            {
                var c = WorldMapData.Cell(poi.center);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                        if (d.In(c.x + dx, c.y + dy))
                            px[d.Index(c.x + dx, c.y + dy)] = new Color32(0xff, 0x60, 0x60, 255);
            }
            var tex = new Texture2D(d.Width, d.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
