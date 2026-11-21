using System;
using Starbind.Core;
using UnityEngine;

namespace Starbind.Enemies
{
    /// <summary>Enemy attack warning on the ground (GDD §4.4, §10.3). Always on the Telegraph sorting
    /// layer, which renders above player VFX. Calls back when the warning ends; the callback does the
    /// damage. Pooled.</summary>
    public sealed class Telegraph : MonoBehaviour
    {
        static ComponentPool<Telegraph> pool;
        static Transform root;

        SpriteRenderer area;
        SpriteRenderer fill;
        float start, end;
        Action done;
        bool circle;
        Vector2 size;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            pool = null;
            root = null;
        }

        static ComponentPool<Telegraph> Pool
        {
            get
            {
                if (pool == null || root == null)
                {
                    root = new GameObject("[Telegraphs]").transform;
                    pool = new ComponentPool<Telegraph>(Create, 64);
                }
                return pool;
            }
        }

        public static int Active => pool != null && root != null ? pool.ActiveCount : 0;

        static Telegraph Create()
        {
            var go = new GameObject("telegraph");
            go.transform.SetParent(root, false);
            var t = go.AddComponent<Telegraph>();
            t.area = NewRenderer(go.transform, "area", 0);
            t.fill = NewRenderer(go.transform, "fill", 1);
            go.SetActive(false);
            return t;
        }

        static SpriteRenderer NewRenderer(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = StarbindLayers.SortTelegraph;
            sr.sortingOrder = order;
            var db = GameDatabase.Instance;
            if (db != null && db.unlitMaterial != null)
                sr.sharedMaterial = db.unlitMaterial;
            return sr;
        }

        public static Telegraph Circle(Vector2 center, float radius, float duration, Action onDone)
        {
            var t = Pool.Get();
            if (t == null)
            {
                onDone?.Invoke();
                return null;
            }
            var db = GameDatabase.Instance;
            var sprite = db != null ? db.Sprite("VFX_telegraph_circle") : null;
            t.area.sprite = t.fill.sprite = sprite;
            t.circle = true;
            t.size = new Vector2(radius, radius);
            t.Begin(center, 0f, duration, onDone);
            return t;
        }

        /// <summary>Rectangle from <paramref name="from"/> along <paramref name="dir"/>.</summary>
        public static Telegraph Line(Vector2 from, Vector2 dir, float length, float width, float duration, Action onDone)
        {
            var t = Pool.Get();
            if (t == null)
            {
                onDone?.Invoke();
                return null;
            }
            var db = GameDatabase.Instance;
            var sprite = db != null ? db.Sprite("VFX_telegraph_rect") : null;
            t.area.sprite = t.fill.sprite = sprite;
            t.circle = false;
            t.size = new Vector2(length, width);
            dir = dir.sqrMagnitude > 0 ? dir.normalized : Vector2.right;
            float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            t.Begin(from + dir * length * 0.5f, ang, duration, onDone);
            return t;
        }

        void Begin(Vector2 center, float angle, float duration, Action onDone)
        {
            transform.position = center;
            transform.rotation = Quaternion.Euler(0, 0, angle);
            start = Time.time;
            end = Time.time + duration;
            done = onDone;
            // circle sprite is 64 px = 2 units across; rect sprite is 32 px = 1 unit
            area.transform.localScale = circle ? new Vector3(size.x, size.y, 1) : new Vector3(size.x, size.y, 1);
            area.color = new Color(1, 1, 1, 0.9f);
            fill.color = new Color(1f, 0.55f, 0.35f, 0.55f);
            fill.transform.localScale = Vector3.zero;
            Update();
        }

        /// <summary>Removes the warning without running its callback (attacker died or was interrupted).</summary>
        public void Cancel()
        {
            done = null;
            Pool.Release(this);
        }

        void Update()
        {
            float k = Mathf.Clamp01((Time.time - start) / Mathf.Max(0.01f, end - start));
            if (circle)
                fill.transform.localScale = new Vector3(size.x * k, size.y * k, 1);
            else
                fill.transform.localScale = new Vector3(size.x * k, size.y, 1);
            if (circle == false)
                fill.transform.localPosition = new Vector3(-size.x * 0.5f * (1 - k), 0, 0);
            else
                fill.transform.localPosition = Vector3.zero;
            float blink = (int)(Time.time * 12) % 2 == 0 ? 1f : 0.75f;
            area.color = new Color(1, 1, 1, 0.9f * blink);
            if (Time.time >= end)
            {
                var cb = done;
                done = null;
                Pool.Release(this);
                cb?.Invoke();
            }
        }
    }
}
