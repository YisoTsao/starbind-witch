using UnityEngine;

namespace Starbind.World
{
    /// <summary>Hides debug renderers (collision/marker tilemaps) in Play mode and builds.</summary>
    public sealed class EditorOnlyRenderer : MonoBehaviour
    {
        void Awake()
        {
            foreach (var r in GetComponents<Renderer>())
                r.enabled = false;
        }
    }
}
