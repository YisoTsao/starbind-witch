using System;
using UnityEngine;

namespace Starbind.World.Studio
{
    /// <summary>Reusable point-of-interest template: clearing shape, ground, fixed props and gameplay objects.</summary>
    [CreateAssetMenu(menuName = "Starbind/World Studio/POI")]
    public sealed class PoiDefinition : ScriptableObject
    {
        public string id;
        public PoiKind kind;
        public string displayName;
        [Tooltip("Clearing radii in tiles (x, y); the boundary is distorted by noise.")]
        public Vector2 clearing = new Vector2(8, 6);
        [Range(0, 0.5f)] public float boundaryNoise = 0.22f;
        public GroundStyle ground = GroundStyle.Grass;
        [Tooltip("Paver disc radius around the centre (0 = none).")]
        public float paverRadius;
        [Tooltip("No enemy spawning inside the clearing (entrance, camp, boss arena).")]
        public bool safe;
        public PoiStamp[] stamps = Array.Empty<PoiStamp>();
        public PoiObject[] objects = Array.Empty<PoiObject>();
        [Tooltip("Scatter props are kept this far (tiles) from the centre so combat stays readable.")]
        public float clearCore = 3f;
        [Tooltip("Roads that end here stop painting dirt this far (tiles) from the centre: the plaza stays free for stamps.")]
        public float roadStop;
    }
}
