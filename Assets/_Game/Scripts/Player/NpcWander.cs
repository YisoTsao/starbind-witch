using Starbind.Core;
using UnityEngine;

namespace Starbind.Player
{
    /// <summary>Ambient villager stroll: walks to random points within <c>radius</c> of its start, pauses,
    /// repeats. Stops while the witch is close (so she can talk) or a dialogue / menu is open, and
    /// turns back when the next step would touch a solid collider or water. Display / ambience only;
    /// uses its own seeded System.Random so it never consumes gameplay randomness (GameRng).</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class NpcWander : MonoBehaviour
    {
        [SerializeField] float radius = 2f;
        [SerializeField] float speed = 0.9f;
        [SerializeField] float stopNearPlayer = 1.8f;

        static readonly Collider2D[] hits = new Collider2D[8];
        Rigidbody2D body;
        Collider2D own;
        Vector2 home, target;
        float waitUntil;
        System.Random rng;
        ContactFilter2D filter;

        public bool Walking { get; private set; }
        public Vector2 Direction { get; private set; } = Vector2.down;
        public Vector2 Home => home;
        public float Radius => radius;

        public void Configure(float r, float s = 0.9f)
        {
            radius = r;
            speed = s;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            own = GetComponent<Collider2D>();
            home = transform.position;
            int seed = 17;
            foreach (char ch in name)
                seed = seed * 31 + ch;
            rng = new System.Random(seed);
            waitUntil = Time.time + 0.5f + (float)rng.NextDouble() * 2.5f;
            filter = new ContactFilter2D { useTriggers = false, useLayerMask = true };
            filter.SetLayerMask(LayerMask.GetMask(StarbindLayers.WorldSolid, StarbindLayers.Water, StarbindLayers.Player));
        }

        void FixedUpdate()
        {
            if (ShouldHold())
            {
                Walking = false;
                waitUntil = Mathf.Max(waitUntil, Time.time + 1f);
                return;
            }
            if (!Walking)
            {
                if (Time.time < waitUntil)
                    return;
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = radius * Mathf.Sqrt((float)rng.NextDouble());
                target = home + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                Walking = true;
            }
            var pos = body.position;
            var to = target - pos;
            if (to.magnitude < 0.06f)
            {
                Rest(1.5f, 4f);
                return;
            }
            Direction = to.normalized;
            var next = pos + Direction * Mathf.Min(speed * Time.fixedDeltaTime, to.magnitude);
            if (Blocked(next))
            {
                Rest(0.8f, 2f);
                return;
            }
            body.MovePosition(next);
        }

        void Rest(float min, float max)
        {
            Walking = false;
            waitUntil = Time.time + min + (float)rng.NextDouble() * (max - min);
        }

        bool ShouldHold()
        {
            if (GameSession.Exists && GameSession.Instance.UI != null && GameSession.Instance.UI.BlocksGameplay)
                return true;
            var p = PlayerController.Current;
            return p != null && ((Vector2)p.transform.position - body.position).sqrMagnitude < stopNearPlayer * stopNearPlayer;
        }

        bool Blocked(Vector2 next)
        {
            int n = Physics2D.OverlapCircle(next + new Vector2(0, 0.12f), 0.2f, filter, hits);
            for (int i = 0; i < n; i++)
                if (hits[i] != own)
                    return true;
            return false;
        }
    }
}
