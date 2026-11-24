using System.Linq;
using Starbind.Core;
using Starbind.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Starbind.Editor
{
    public static class ChapterWalkthroughBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Dev/ChapterWalkthrough.unity";

        [MenuItem("Starbind/Open Chapter Walkthrough")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static void Create()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Chapter walkthrough");
            var config = root.AddComponent<SceneConfig>();
            config.title = "章節灰盒";
            config.objective = "沿通路探索，至章節入口或終點按 F 往返";
            var walk = root.AddComponent<ChapterGraybox>();
            walk.atlas = AssetDatabase.LoadAssetAtPath<TextAsset>(ChapterExplorer.DataPath);
            walk.groundTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/Forest/TL_forest_grass_draft.asset");
            walk.wallTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/_Game/Tiles/Debug/TL_collision_block.asset");
            walk.anchorSprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Game/Tiles/Debug/DBG_spawn.png").OfType<Sprite>().First();
            walk.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/Player/Player.prefab");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
