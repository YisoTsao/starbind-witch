using Starbind.Core;
using UnityEditor;
using UnityEngine;

namespace Starbind.Editor
{
    /// <summary>Idempotent project settings for Phase 0 (TECH_ARCHITECTURE.md §2, §4, §5).</summary>
    public static class ProjectConfigurator
    {
        const string Renderer2DPath = "Assets/_Game/Settings/Renderer2D.asset";

        static readonly (int index, string name)[] PhysicsLayers =
        {
            (6, StarbindLayers.Player), (7, StarbindLayers.Enemy), (8, StarbindLayers.WorldSolid),
            (9, "Hitbox"), (10, "Hurtbox"), (11, "Projectile"), (12, StarbindLayers.Interactable),
            (13, StarbindLayers.Trigger),
        };

        [MenuItem("Starbind/Configure Project Settings")]
        public static void Apply()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            EnsureSortingLayers(tagManager);
            EnsurePhysicsLayers(tagManager);
            tagManager.ApplyModifiedPropertiesWithoutUndo();

            Physics2D.gravity = Vector2.zero;
            ConfigureRenderer2D();

            PlayerSettings.productName = "Starbind Witch";
            PlayerSettings.companyName = "Starbind";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.runInBackground = false;
            AssetDatabase.SaveAssets();
        }

        static void EnsureSortingLayers(SerializedObject tagManager)
        {
            var layers = tagManager.FindProperty("m_SortingLayers");
            foreach (var name in StarbindLayers.SortingOrder)
            {
                bool found = false;
                for (int i = 0; i < layers.arraySize; i++)
                    found |= layers.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue == name;
                if (found)
                    continue;
                layers.InsertArrayElementAtIndex(layers.arraySize);
                var e = layers.GetArrayElementAtIndex(layers.arraySize - 1);
                e.FindPropertyRelative("name").stringValue = name;
                e.FindPropertyRelative("uniqueID").uintValue = StableId(name);
            }
        }

        static void EnsurePhysicsLayers(SerializedObject tagManager)
        {
            var layers = tagManager.FindProperty("layers");
            foreach (var (index, name) in PhysicsLayers)
            {
                var p = layers.GetArrayElementAtIndex(index);
                if (string.IsNullOrEmpty(p.stringValue))
                    p.stringValue = name;
                else if (p.stringValue != name)
                    Debug.LogWarning($"[ProjectConfigurator] layer {index} is '{p.stringValue}', expected '{name}'");
            }
        }

        static void ConfigureRenderer2D()
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(Renderer2DPath);
            if (asset == null)
            {
                Debug.LogError("[ProjectConfigurator] missing " + Renderer2DPath);
                return;
            }
            var so = new SerializedObject(asset);
            so.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
            so.FindProperty("m_TransparencySortAxis").vector3Value = new Vector3(0, 1, 0);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        /// <summary>FNV-1a; string.GetHashCode is not stable across processes.</summary>
        static uint StableId(string s)
        {
            uint h = 2166136261;
            foreach (char c in s)
                h = (h ^ c) * 16777619;
            return h == 0 ? 1u : h;
        }
    }
}
