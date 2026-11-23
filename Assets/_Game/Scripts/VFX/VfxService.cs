using System.Collections.Generic;
using Starbind.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Starbind.VFX
{
    /// <summary>Scene-scoped VFX player (GDD §12.2): pooled sprite flipbooks, pooled 2D light flashes,
    /// one shared particle system for sparks (Emit, no per-effect systems), pooled lightning arcs, lights that
    /// follow projectiles and a full-screen flash. Display only: nothing here touches HP. Caps keep the frame
    /// bounded under 60 enemies.</summary>
    public sealed class VfxService : MonoBehaviour
    {
        public const int MaxFlipbooks = 160;
        public const int MaxLights = 32;
        public const int MaxAttachedLights = 16;
        /// <summary>Accessibility: scales every full-screen flash (0 disables them).</summary>
        public static float ScreenFlashStrength = 1f;

        static VfxService instance;

        readonly Dictionary<string, ComponentPool<SpriteFlipbook>> pools = new Dictionary<string, ComponentPool<SpriteFlipbook>>();
        readonly Dictionary<SpriteFlipbook, string> owner = new Dictionary<SpriteFlipbook, string>();
        readonly Dictionary<SpriteFlipbook, Transform> follow = new Dictionary<SpriteFlipbook, Transform>();
        readonly List<SpriteFlipbook> followKeys = new List<SpriteFlipbook>();
        ComponentPool<Light2D> lights;
        readonly List<(Light2D light, float start, float end, float intensity)> liveLights = new List<(Light2D, float, float, float)>();
        ComponentPool<LineRenderer> arcs;
        readonly List<(LineRenderer line, float end)> liveArcs = new List<(LineRenderer, float)>();
        ComponentPool<Light2D> attachedLights;
        readonly Dictionary<Light2D, Transform> attached = new Dictionary<Light2D, Transform>();
        readonly List<Light2D> attachedKeys = new List<Light2D>();
        Image screenFlash;
        float flashStart, flashEnd, flashAlpha;
        ParticleSystem sparks;
        Material glowMaterial;
        int liveFlipbooks;
        GameDatabase db;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => instance = null;

        public static VfxService Instance
        {
            get
            {
                if (instance == null)
                    instance = new GameObject("[VfxService]").AddComponent<VfxService>();
                return instance;
            }
        }

        public int LiveFlipbooks => liveFlipbooks;
        public int LiveLights => liveLights.Count;
        public int LiveArcs => liveArcs.Count;
        public int AttachedLights => attached.Count;
        public float ScreenFlashAlpha => screenFlash != null && screenFlash.enabled ? screenFlash.color.a : 0f;
        /// <summary>Counts every screen flash requested (tests and the benchmark read it).</summary>
        public int ScreenFlashes { get; private set; }

        void Awake()
        {
            db = GameDatabase.Instance;
            lights = new ComponentPool<Light2D>(CreateLight, MaxLights);
            attachedLights = new ComponentPool<Light2D>(CreateLight, MaxAttachedLights);
            arcs = new ComponentPool<LineRenderer>(CreateArc, 96);
            CreateSparks();
        }

        void OnDestroy()
        {
            if (glowMaterial != null)
                Destroy(glowMaterial);
            if (instance == this)
                instance = null;
        }

        // ---------------------------------------------------------------- flipbooks
        public SpriteFlipbook Play(string name, Vector2 pos, float angleDeg = 0f, float scale = 1f, Color? tint = null,
            string sortingLayer = StarbindLayers.SortVfx, int order = 0, bool loop = false, Transform followTarget = null, float fps = 0f)
        {
            if (db == null || liveFlipbooks >= MaxFlipbooks)
                return null;
            var set = db.Sprites(name);
            if (set == null)
                return null;
            if (!pools.TryGetValue(name, out var pool))
            {
                var key = name;
                pool = new ComponentPool<SpriteFlipbook>(() => CreateFlipbook(key));
                pools[name] = pool;
            }
            var fb = pool.Get();
            if (fb == null)
                return null;
            owner[fb] = name;
            liveFlipbooks++;
            var tr = fb.transform;
            tr.position = new Vector3(pos.x, pos.y, 0);
            tr.rotation = Quaternion.Euler(0, 0, angleDeg);
            tr.localScale = new Vector3(scale, scale, 1);
            var sr = fb.Renderer;
            sr.color = tint ?? Color.white;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            sr.flipX = false;
            fb.FollowOffset = Vector2.zero;
            fb.Play(set, loop, fps);
            if (followTarget != null)
                follow[fb] = followTarget;
            return fb;
        }

        /// <summary>Local additive light around the sprite, independent of scene lighting.</summary>
        public SpriteFlipbook Glow(Vector2 pos, Color color, float diameter, float duration = 0.24f, Transform target = null,
            string sortingLayer = StarbindLayers.SortVfx)
        {
            if (glowMaterial == null)
            {
                var shader = Resources.Load<Shader>("SpellGlow");
                if (shader == null)
                    return null;
                glowMaterial = new Material(shader);
            }
            var glow = Play("VFX_spell_glow", pos, 0f, diameter, color, sortingLayer, -4,
                target != null, target, 1f / Mathf.Max(0.01f, duration));
            if (glow != null)
            {
                glow.Renderer.sharedMaterial = glowMaterial;
                glow.FadeOut = true;
                if (target != null)
                    glow.FollowOffset = pos - (Vector2)target.position;
            }
            return glow;
        }

        public void Release(SpriteFlipbook fb)
        {
            if (fb == null || !owner.TryGetValue(fb, out var name))
                return;
            owner.Remove(fb);
            follow.Remove(fb);
            liveFlipbooks--;
            pools[name].Release(fb);
        }

        SpriteFlipbook CreateFlipbook(string name)
        {
            var go = new GameObject("vfx_" + name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (db.unlitMaterial != null)
                sr.sharedMaterial = db.unlitMaterial;
            var fb = go.AddComponent<SpriteFlipbook>();
            fb.Finished += Release;
            go.SetActive(false);
            return fb;
        }

        // ---------------------------------------------------------------- lights
        public void Light(Vector2 pos, Color color, float radius, float intensity, float duration)
        {
            var l = lights.Get();
            if (l == null)
                return;
            l.transform.position = new Vector3(pos.x, pos.y, 0);
            l.color = color;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.2f;
            l.intensity = intensity;
            liveLights.Add((l, Time.time, Time.time + duration, intensity));
        }

        /// <summary>A light that follows <paramref name="target"/> until DetachLight (projectile glow).
        /// Returns null when the attached-light budget is used up.</summary>
        public Light2D AttachLight(Transform target, Color color, float radius, float intensity)
        {
            var l = attachedLights.Get();
            if (l == null)
                return null;
            l.transform.position = target.position;
            l.color = color;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.15f;
            l.intensity = intensity;
            attached[l] = target;
            return l;
        }

        public void DetachLight(Light2D l)
        {
            if (l == null || !attached.Remove(l))
                return;
            attachedLights.Release(l);
        }

        Light2D CreateLight()
        {
            var go = new GameObject("vfx_light");
            go.transform.SetParent(transform, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.falloffIntensity = 0.7f;
            go.SetActive(false);
            return l;
        }

        // ---------------------------------------------------------------- arcs
        public void Arc(Vector2 a, Vector2 b, Color color, float duration = 0.14f, float widthPx = 2f, float jitter = 0.22f)
            => DrawArc(a, b, color, duration, widthPx, jitter, 20);

        LineRenderer DrawArc(Vector2 a, Vector2 b, Color color, float duration, float widthPx, float jitter, int order,
            LineRenderer path = null)
        {
            var line = arcs.Get();
            if (line == null)
                return null;
            int segments = Mathf.Clamp(Mathf.CeilToInt((b - a).magnitude / 0.35f), 4, 14);
            line.positionCount = segments + 1;
            var perp = Vector2.Perpendicular((b - a).normalized);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                var p = Vector2.Lerp(a, b, t);
                if (path != null)
                    p = path.GetPosition(i);
                else if (i > 0 && i < segments)
                    p += perp * Random.Range(-jitter, jitter);
                const float ppu = 32f;
                line.SetPosition(i, new Vector3(Mathf.Round(p.x * ppu) / ppu, Mathf.Round(p.y * ppu) / ppu, 0));
            }
            line.widthMultiplier = widthPx / 32f;
            line.sortingOrder = order;
            line.startColor = line.endColor = color;
            liveArcs.Add((line, Time.time + duration));
            return line;
        }

        /// <summary>Thick lightning: a wide coloured glow, a white core and a few short forks.</summary>
        public void Bolt(Vector2 a, Vector2 b, Color glow, float duration = 0.18f, int forks = 2, float widthPx = 6f)
        {
            float len = (b - a).magnitude;
            var path = DrawArc(a, b, new Color(glow.r, glow.g, glow.b, 0.72f), duration, widthPx, 0.22f, 19);
            if (path == null)
                return;
            DrawArc(a, b, Color.Lerp(glow, Color.white, 0.88f), duration * 0.72f,
                Mathf.Max(1f, widthPx * 0.36f), 0f, 21, path);
            for (int i = 0; i < forks; i++)
            {
                var s = (Vector2)path.GetPosition(Random.Range(1, path.positionCount - 1));
                var dir = Quaternion.Euler(0, 0, Random.Range(-60f, 60f)) * (b - a).normalized;
                DrawArc(s, s + (Vector2)dir * len * Random.Range(0.2f, 0.35f), glow, duration * 0.57f, 1f, 0.09f, 20);
            }
        }

        LineRenderer CreateArc()
        {
            var go = new GameObject("vfx_arc");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = 2f / 32f;
            line.numCapVertices = 0;
            if (db != null && db.unlitMaterial != null)
                line.sharedMaterial = db.unlitMaterial;
            line.sortingLayerName = StarbindLayers.SortVfx;
            line.sortingOrder = 20;
            line.textureMode = LineTextureMode.Stretch;
            go.SetActive(false);
            return line;
        }

        // ---------------------------------------------------------------- screen flash
        /// <summary>Full-screen additive-looking flash (white-out on big blasts). Stronger requests override
        /// weaker ones; scaled by ScreenFlashStrength and capped at 0.55 alpha.</summary>
        public void ScreenFlash(Color color, float alpha, float duration)
        {
            alpha = Mathf.Min(0.55f, alpha * ScreenFlashStrength);
            if (alpha <= 0.01f)
                return;
            ScreenFlashes++;
            float now = Time.unscaledTime;
            float current = screenFlash != null && now < flashEnd ? flashAlpha * (1f - (now - flashStart) / Mathf.Max(0.01f, flashEnd - flashStart)) : 0f;
            if (current > alpha)
                return;
            if (screenFlash == null)
            {
                var go = new GameObject("vfx_screen_flash");
                go.transform.SetParent(transform, false);
                var canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 90; // under GameUI (100)
                var img = new GameObject("Flash", typeof(RectTransform)).AddComponent<Image>();
                img.transform.SetParent(go.transform, false);
                var rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                img.raycastTarget = false;
                screenFlash = img;
            }
            screenFlash.enabled = true;
            screenFlash.color = new Color(color.r, color.g, color.b, alpha);
            flashStart = now;
            flashEnd = now + duration;
            flashAlpha = alpha;
        }

        // ---------------------------------------------------------------- particles
        void CreateSparks()
        {
            var go = new GameObject("vfx_sparks");
            go.transform.SetParent(transform, false);
            sparks = go.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1500;
            main.startSpeed = 0;
            main.startLifetime = 0.4f;
            main.gravityModifier = 0;
            var emission = sparks.emission;
            emission.enabled = false;
            var shape = sparks.shape;
            shape.enabled = false;
            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, 0.2f));
            var limit = sparks.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.dampen = 0.12f;
            limit.limit = 0.5f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sortingLayerName = StarbindLayers.SortVfx;
            r.sortingOrder = 30;
            var spark = db != null ? db.Sprite("VFX_spark") : null;
            if (spark != null)
            {
                var tsa = sparks.textureSheetAnimation;
                tsa.enabled = true;
                tsa.mode = ParticleSystemAnimationMode.Sprites;
                tsa.AddSprite(spark);
            }
            if (db != null && db.unlitMaterial != null)
                r.sharedMaterial = db.unlitMaterial;
            sparks.Play();
        }

        public void Sparks(Vector2 pos, Color color, int count, float speed, float life = 0.4f, float size = 0.12f)
        {
            if (sparks == null || sparks.particleCount > 1200)
                return;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f; // cosmetic
                float s = speed * (0.4f + Random.value * 0.6f);
                ep.position = new Vector3(pos.x, pos.y, 0);
                ep.velocity = new Vector3(Mathf.Cos(a) * s, Mathf.Sin(a) * s, 0);
                ep.startColor = color;
                ep.startLifetime = life * (0.6f + Random.value * 0.4f);
                ep.startSize = size;
                sparks.Emit(ep, 1);
            }
        }

        /// <summary>Trail embers: a few short-lived sparks drifting along <paramref name="drift"/>.</summary>
        public void Embers(Vector2 pos, Color color, Vector2 drift, int count = 1, float life = 0.3f, float size = 0.09f)
        {
            if (sparks == null || sparks.particleCount > 1100)
                return;
            var ep = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var j = Random.insideUnitCircle * 0.08f; // cosmetic
                ep.position = new Vector3(pos.x + j.x, pos.y + j.y, 0);
                ep.velocity = (Vector3)(drift + Random.insideUnitCircle * 0.6f);
                ep.startColor = color;
                ep.startLifetime = life * (0.6f + Random.value * 0.4f);
                ep.startSize = size;
                sparks.Emit(ep, 1);
            }
        }

        // ---------------------------------------------------------------- tick
        void LateUpdate()
        {
            float now = Time.time;
            for (int i = liveLights.Count - 1; i >= 0; i--)
            {
                var (l, start, end, intensity) = liveLights[i];
                if (now >= end)
                {
                    lights.Release(l);
                    liveLights.RemoveAt(i);
                }
                else
                    l.intensity = intensity * (1f - (now - start) / Mathf.Max(0.01f, end - start));
            }
            if (screenFlash != null && screenFlash.enabled)
            {
                float u = (Time.unscaledTime - flashStart) / Mathf.Max(0.01f, flashEnd - flashStart);
                if (u >= 1f)
                    screenFlash.enabled = false;
                else
                {
                    var col = screenFlash.color;
                    col.a = flashAlpha * (1f - u) * (1f - u);
                    screenFlash.color = col;
                }
            }
            if (attached.Count > 0)
            {
                attachedKeys.Clear();
                attachedKeys.AddRange(attached.Keys);
                foreach (var l in attachedKeys)
                {
                    var t = attached[l];
                    if (t == null || !t.gameObject.activeInHierarchy)
                        DetachLight(l);
                    else
                        l.transform.position = new Vector3(t.position.x, t.position.y, 0);
                }
            }
            for (int i = liveArcs.Count - 1; i >= 0; i--)
                if (now >= liveArcs[i].end)
                {
                    arcs.Release(liveArcs[i].line);
                    liveArcs.RemoveAt(i);
                }
            if (follow.Count > 0)
            {
                followKeys.Clear();
                followKeys.AddRange(follow.Keys);
                foreach (var fb in followKeys)
                {
                    var t = follow[fb];
                    if (t == null || !t.gameObject.activeInHierarchy)
                    {
                        if (fb.Loop)
                            Release(fb);
                        else
                            follow.Remove(fb);
                        continue;
                    }
                    var p = t.position;
                    fb.transform.position = new Vector3(p.x, p.y, 0) + (Vector3)fb.FollowOffset;
                }
            }
        }
    }
}
