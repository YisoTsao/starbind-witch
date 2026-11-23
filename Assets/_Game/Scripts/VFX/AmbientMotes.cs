using UnityEngine;

namespace Starbind.VFX
{
    /// <summary>Drifting firefly / magic motes around the camera view (village ambience). Display only:
    /// a fixed set of sprites created once, wrapped around the view as the camera moves, so the count
    /// never grows. Uses its own seeded System.Random, never gameplay randomness (GameRng).</summary>
    public sealed class AmbientMotes : MonoBehaviour
    {
        [SerializeField] Sprite sprite;
        [SerializeField] Material material;
        [SerializeField] int count = 26;
        [SerializeField] Color colorA = new Color(0.95f, 1f, 0.7f, 1f);
        [SerializeField] Color colorB = new Color(0.85f, 0.7f, 1f, 1f);
        [SerializeField] string sortingLayer = "Foreground";

        SpriteRenderer[] motes;
        Vector2[] offset;
        float[] phase, speed;
        Camera cam;

        public int Count => motes?.Length ?? 0;

        public void Configure(Sprite s, Material m, int n)
        {
            sprite = s;
            material = m;
            count = n;
        }

        void Start()
        {
            cam = Camera.main;
            var rng = new System.Random(7177);
            motes = new SpriteRenderer[count];
            offset = new Vector2[count];
            phase = new float[count];
            speed = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Mote");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                if (material != null)
                    sr.sharedMaterial = material;
                sr.sortingLayerName = sortingLayer;
                sr.color = rng.NextDouble() < 0.6 ? colorA : colorB;
                motes[i] = sr;
                offset[i] = new Vector2((float)rng.NextDouble(), (float)rng.NextDouble());
                phase[i] = (float)rng.NextDouble() * 10f;
                speed[i] = 0.15f + (float)rng.NextDouble() * 0.25f;
            }
        }

        void LateUpdate()
        {
            if (motes == null || cam == null)
                return;
            float h = cam.orthographicSize * 2f + 1f, w = h * cam.aspect + 1f;
            Vector2 c = cam.transform.position;
            float t = Time.time;
            for (int i = 0; i < motes.Length; i++)
            {
                float p = phase[i];
                // world-anchored drift, wrapped into the view rectangle so motes stay put as the camera pans
                var drift = new Vector2(Mathf.Sin(t * speed[i] + p) * 0.6f + t * 0.05f, Mathf.Sin(t * speed[i] * 1.3f + p * 2f) * 0.4f + t * 0.08f);
                float x = Mathf.Repeat(offset[i].x * w + drift.x - c.x, w) + c.x - w * 0.5f;
                float y = Mathf.Repeat(offset[i].y * h + drift.y - c.y, h) + c.y - h * 0.5f;
                motes[i].transform.position = new Vector3(x, y, 0);
                var col = motes[i].color;
                col.a = 0.25f + 0.75f * Mathf.Max(0f, Mathf.Sin(t * 1.7f * speed[i] * 4f + p));
                motes[i].color = col;
            }
        }
    }
}
