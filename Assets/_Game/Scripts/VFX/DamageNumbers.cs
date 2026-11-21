using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using UnityEngine;

namespace Starbind.VFX
{
    /// <summary>Pixel-digit damage numbers (draft digit sprites), pooled. Hits on the same target within
    /// MergeWindow add into the existing number instead of spawning more (GDD §10.2 merge rule).</summary>
    public sealed class DamageNumbers : MonoBehaviour
    {
        public const float MergeWindow = 0.15f;
        public const float Life = 0.65f;
        const int MaxDigits = 4;

        sealed class Number
        {
            public GameObject go;
            public SpriteRenderer[] digits;
            public float start;
            public float value;
            public int target;
            public Vector3 origin;
            public float scale;
        }

        readonly Stack<Number> free = new Stack<Number>();
        readonly List<Number> live = new List<Number>();
        Sprite[] glyphs;

        static DamageNumbers instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static DamageNumbers Instance
        {
            get
            {
                if (instance == null)
                    instance = new GameObject("[DamageNumbers]").AddComponent<DamageNumbers>();
                return instance;
            }
        }

        public int LiveCount => live.Count;

        void Awake()
        {
            var set = GameDatabase.Instance != null ? GameDatabase.Instance.Sprites("UI_digits") : null;
            glyphs = set?.frames;
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        public static Color ColorFor(in DamageEvent e)
        {
            if (e.Source == HitSource.Reaction) return new Color(1f, 0.55f, 1f);
            if (e.Source == HitSource.Enemy) return new Color(1f, 0.35f, 0.35f);
            switch (e.Element)
            {
                case ElementType.Fire: return new Color(1f, 0.65f, 0.25f);
                case ElementType.Ice: return new Color(0.55f, 0.9f, 1f);
                case ElementType.Lightning: return new Color(1f, 0.95f, 0.4f);
                case ElementType.Wind: return new Color(0.6f, 1f, 0.8f);
            }
            return e.Source == HitSource.Environment ? new Color(1f, 0.8f, 0.6f) : Color.white;
        }

        public void Show(Vector2 pos, float amount, Color color, int targetId, bool big)
        {
            if (glyphs == null || glyphs.Length < 10 || amount < 0.5f)
                return;
            float now = Time.time;
            foreach (var n in live)
                if (n.target == targetId && now - n.start < MergeWindow)
                {
                    n.value += amount;
                    Render(n, color);
                    return;
                }
            var num = free.Count > 0 ? free.Pop() : Create();
            num.go.SetActive(true);
            num.start = now;
            num.value = amount;
            num.target = targetId;
            num.scale = big ? 1.5f : 1f;
            num.origin = new Vector3(pos.x + Random.Range(-0.15f, 0.15f), pos.y + 0.5f, 0);
            num.go.transform.position = num.origin;
            num.go.transform.localScale = Vector3.one * num.scale;
            Render(num, color);
            live.Add(num);
        }

        Number Create()
        {
            var n = new Number { go = new GameObject("dmg") };
            n.go.transform.SetParent(transform, false);
            n.digits = new SpriteRenderer[MaxDigits];
            for (int i = 0; i < MaxDigits; i++)
            {
                var d = new GameObject("d" + i);
                d.transform.SetParent(n.go.transform, false);
                var sr = d.AddComponent<SpriteRenderer>();
                sr.sortingLayerName = StarbindLayers.SortWorldUI;
                sr.sortingOrder = 10;
                if (GameDatabase.Instance != null && GameDatabase.Instance.unlitMaterial != null)
                    sr.sharedMaterial = GameDatabase.Instance.unlitMaterial;
                n.digits[i] = sr;
            }
            return n;
        }

        void Render(Number n, Color color)
        {
            int v = Mathf.Clamp(Mathf.RoundToInt(n.value), 0, 9999);
            string s = v.ToString();
            float w = 6f / 32f;
            float x0 = -(s.Length - 1) * w * 0.5f;
            for (int i = 0; i < MaxDigits; i++)
            {
                var sr = n.digits[i];
                if (i < s.Length)
                {
                    sr.enabled = true;
                    sr.sprite = glyphs[s[i] - '0'];
                    sr.color = color;
                    sr.transform.localPosition = new Vector3(x0 + i * w, 0, 0);
                }
                else
                    sr.enabled = false;
            }
        }

        void LateUpdate()
        {
            float now = Time.time;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var n = live[i];
                float t = (now - n.start) / Life;
                if (t >= 1f)
                {
                    n.go.SetActive(false);
                    live.RemoveAt(i);
                    free.Push(n);
                    continue;
                }
                float rise = Mathf.Sqrt(t) * 0.6f;
                var p = n.origin + new Vector3(0, rise, 0);
                p.x = Mathf.Round(p.x * 32f) / 32f;
                p.y = Mathf.Round(p.y * 32f) / 32f;
                n.go.transform.position = p;
                bool visible = t < 0.75f || ((int)(now * 30) % 2 == 0);
                foreach (var d in n.digits)
                    if (d.sprite != null)
                        d.forceRenderingOff = !visible;
            }
        }
    }
}
