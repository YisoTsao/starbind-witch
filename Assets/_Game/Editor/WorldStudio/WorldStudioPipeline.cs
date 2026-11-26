using System;
using System.IO;
using Starbind.World.Studio;
using UnityEditor;
using UnityEngine;

namespace Starbind.Editor.WorldStudio
{
    /// <summary>Generate -> validate -> export JSON -> build scene, shared by the World Studio window,
    /// the Build All menu and batch mode. A region whose validation fails is not built.</summary>
    public static class WorldStudioPipeline
    {
        public const string MapsFolder = "Assets/_Game/Data/Maps/";
        public const string RegionScenes = "Assets/_Game/Scenes/Regions/";

        public static string JsonPath(RegionBlueprint bp) => MapsFolder + bp.id + ".json";
        public static string ScenePath(RegionBlueprint bp) => RegionScenes + bp.sceneName + ".unity";

        public static (WorldMapData data, WorldValidation report) Generate(RegionBlueprint bp, int seed)
        {
            var t0 = DateTime.Now;
            var data = WorldGenerator.Generate(bp, seed);
            var report = data.Validate();
            data.Log.Add($"generate + validate {(DateTime.Now - t0).TotalMilliseconds:0} ms");
            return (data, report);
        }

        /// <summary>Generates, validates, writes the map JSON and builds the scene. Returns the report.</summary>
        public static WorldValidation GenerateAndBuild(RegionBlueprint bp, int seed)
        {
            var (data, report) = Generate(bp, seed);
            foreach (var line in data.Log)
                Debug.Log("[WorldStudio] " + line);
            Debug.Log("[WorldStudio] validation: " + report);
            if (!report.Ok)
            {
                Debug.LogError($"[WorldStudio] {bp.id} seed {seed} failed validation; scene not built");
                return report;
            }
            Directory.CreateDirectory(MapsFolder);
            File.WriteAllText(JsonPath(bp), WorldMapExporter.ToJson(data, bp));
            AssetDatabase.ImportAsset(JsonPath(bp));
            GameContentBuilder.BuildRegionScene(JsonPath(bp), ScenePath(bp));
            Debug.Log($"[WorldStudio] built {ScenePath(bp)} from {JsonPath(bp)}");
            return report;
        }

        /// <summary>Every preset region (called by Starbind/Build All Game Content).</summary>
        public static void BuildAllRegions()
        {
            if (!File.Exists(MoonveilPreset.ManifestPath))
            {
                Debug.LogWarning("[WorldStudio] Moonveil art manifest missing; run tools/art/moonveil_art.py. Region skipped.");
                return;
            }
            var bp = MoonveilPreset.Apply();
            GenerateAndBuild(bp, bp.seed);
        }

        /// <summary>Batch: Unity -batchmode -executeMethod Starbind.Editor.WorldStudio.WorldStudioPipeline.BuildMoonveilBatch</summary>
        public static void BuildMoonveilBatch()
        {
            try
            {
                var bp = MoonveilPreset.Apply();
                var report = GenerateAndBuild(bp, bp.seed);
                EditorApplication.Exit(report.Ok ? 0 : 2);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
