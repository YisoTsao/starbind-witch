using System.Collections.Generic;
using Starbind.Core;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Starbind.VFX
{
    /// <summary>Scene-scoped VFX player (GDD §12.2): pooled sprite flipbooks, pooled 2D light flashes,
    /// one shared particle system for sparks (Emit, no per-effect systems), pooled lightning arcs.
    /// Display only: nothing here touches HP. Caps keep the frame bounded under 60 enemies.</summary>
    public sealed class VfxService : MonoBehaviour
    {
        public const int MaxFlipbooks = 160;
        public const int MaxLights = 12;

        static VfxService instance;

        readonly Dictionary<string, ComponentPool<SpriteFlipbook>> pools = new Dictionary<string, ComponentPool<SpriteFlipbook>>();
        readonly Dictionary<SpriteFlipbook, string> owner = new Dictionary<SpriteFlipbook, string>();
        readonly Dictionary<SpriteFlipbook, Transform> follow = new Dictionary<SpriteFlipbook, Transform>();
        readonly List<SpriteFlipbook> followKeys = new List<SpriteFlipbook>();
        ComponentPool<Light2D> lights;
        readonly List<(Light2D light, float start, float end, float intensity)> liveLights = new List<(Light2D, float, float, float)>();
        ComponentPool<LineRenderer> arcs;
        readonly List<(LineRenderer line, float end)> liveArcs = new List<(LineRenderer, float)>();
        ParticleSystem sparks;
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

        void Awake()
        {
            db = GameDatabase.Instance;
            lights = new ComponentPool<Light2D>(CreateLight, MaxLights);
            arcs = new ComponentPool<LineRenderer>(CreateArc, 48);
            CreateSparks();
        }

        void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        // ---------------------------------------------------------------- flipbooks
        public SpriteFlipbook Play(string name, Vector2 pos, float angleDeg = 0f, float scale = 1f, Color? tint = null,
            string sortingLayer = StarbindLayers.SortVfx, int order = 0, bool loop = false, Transform followTarget = null, float fps = 0f)
        {
            if (db == null || (!loop && liveFlipbooks >= MaxFlipbooks))
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
            fb.Play(set, loop, fps);
            if (followTarget != null)
                follow[fb] = followTarget;
            return fb;
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
        public void Arc(Vector2 a, Vector2 b, Color color, float duration = 0.14f)
        {
            var line = arcs.Get();
            if (line == null)
                return;
            const int segments = 7;
            line.positionCount = segments + 1;
            var perp = Vector2.Perpendicular((b - a).normalized);
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                var p = Vector2.Lerp(a, b, t);
                if (i > 0 && i < segments)
                    p += perp * Random.Range(-0.22f, 0.22f); // cosmetic jitter only
                line.SetPosition(i, new Vector3(p.x, p.y, 0));
            }
            line.startColor = line.endColor = color;
            liveArcs.Add((line, Time.time + duration));
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
