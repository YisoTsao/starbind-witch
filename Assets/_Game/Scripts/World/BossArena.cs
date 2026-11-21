using Starbind.Combat;
using Starbind.Core;
using Starbind.Enemies;
using Starbind.Progression;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Boss arena (Z05): entering it starts the Eclipse Mushroom King fight once.</summary>
    public sealed class BossArena : MonoBehaviour
    {
        [SerializeField] Rect trigger;
        [SerializeField] Vector2 bossSpawn;
        [SerializeField] Collider2D[] gates;
        public bool Started { get; private set; }
        public Rect Trigger => trigger;
        public Vector2 BossSpawn => bossSpawn;

        public void Configure(Rect r, Vector2 spawn, Collider2D[] g)
        {
            trigger = r;
            bossSpawn = spawn;
            gates = g;
            SetGates(false);
        }

        void Start() => SetGates(false);

        void SetGates(bool closed)
        {
            if (gates == null) return;
            foreach (var g in gates)
                if (g != null)
                {
                    g.enabled = closed;
                    foreach (var sr in g.GetComponentsInChildren<SpriteRenderer>(true))
                        sr.enabled = closed;
                }
        }

        void Update()
        {
            var p = Player.PlayerController.Current;
            if (Started || p == null || RunManager.Current == null)
                return;
            if (trigger.Contains(p.transform.position))
                Begin();
        }

        public void Begin()
        {
            if (Started)
                return;
            Started = true;
            SetGates(true);
            RunManager.Current.StartBoss(bossSpawn, () => SetGates(false));
        }
    }
}
