using UnityEngine;

namespace Starbind.Core
{
    /// <summary>Per-scene settings placed by the scene builder: title, ambient dim for characters,
    /// whether it is a safe zone (no enemies) and the default objective text.</summary>
    public sealed class SceneConfig : MonoBehaviour
    {
        public string title = "星露村";
        [Range(0, 0.5f)] public float characterDim;
        public bool safeZone = true;
        [TextArea] public string objective = "";

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
