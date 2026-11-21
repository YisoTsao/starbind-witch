using UnityEditor;

namespace Starbind.Editor
{
    /// <summary>Phase-0 entry point kept for the documented menu path; the village is now one of the
    /// three scenes built by GameContentBuilder (48x40 village including the Phase-0 north block).</summary>
    public static class VillageSceneBuilder
    {
        public const string ScenePath = GameContentBuilder.VillageScene;

        [MenuItem("Starbind/Build Village Scene (draft art)")]
        public static void Build() => GameContentBuilder.BuildAll();

        public static void BuildBatch() => GameContentBuilder.BuildAllBatch();
    }
}
