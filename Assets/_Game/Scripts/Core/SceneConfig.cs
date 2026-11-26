using UnityEngine;

namespace Starbind.Core
{
    /// <summary>Per-scene settings placed by the scene builder: title, ambient dim for characters,
    /// whether it is a safe zone (no enemies), the default objective text and the chapter it runs.</summary>
    public sealed class SceneConfig : MonoBehaviour
    {
        public string title = "星露村";
        [Range(0, 0.5f)] public float characterDim;
        public bool safeZone = true;
        [TextArea] public string objective = "";
        /// <summary>ChapterDefinition id for run scenes (forest, mine, ...); empty in safe scenes.</summary>
        public string chapterId = "";
        /// <summary>Optional: replaces the chapter's intro notice / push objective in this scene (a World
        /// Studio region that runs an existing chapter, e.g. Moonveil Forest running chapter 1).</summary>
        public string runIntro = "";
        public string runObjective = "";

        public static SceneConfig Current { get; private set; }

        void OnEnable()
        {
            Current = this;
            Shader.SetGlobalFloat("_SBDim", characterDim);
        }

        void OnDisable()
        {
            if (Current == this)
                Current = null;
        }
    }
}
