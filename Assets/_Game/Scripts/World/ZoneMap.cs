using System;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Named map regions (Forest Z01-Z05, Village areas). Current zone = first rect containing
    /// the player; used by the HUD, spawn weights and events. Plain rect checks, no physics.</summary>
    public sealed class ZoneMap : MonoBehaviour
    {
        [Serializable]
        public struct Zone
        {
            public string id;
            public string displayName;
            public Rect rect;
            public bool noSpawn;
        }

        [SerializeField] Zone[] zones = Array.Empty<Zone>();
        public Zone[] Zones => zones;
        public static ZoneMap Current { get; private set; }

        public void Configure(Zone[] z) => zones = z;

        void OnEnable() => Current = this;

        void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        public bool TryGet(Vector2 p, out Zone zone)
        {
            foreach (var z in zones)
                if (z.rect.Contains(p))
                {
                    zone = z;
                    return true;
                }
            zone = default;
            return false;
        }

        public bool TryGetById(string id, out Zone zone)
        {
            foreach (var z in zones)
                if (z.id == id)
                {
                    zone = z;
                    return true;
                }
            zone = default;
            return false;
        }
    }
}
