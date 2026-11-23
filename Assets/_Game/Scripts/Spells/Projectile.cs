using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Starbind.Spells
{
    /// <summary>Launch parameters for a pooled projectile (player missiles, charged bolt, enemy spores).</summary>
    public struct ProjectileSpec
    {
        public Team Team;
        public DamageEvent Hit;
        public Vector2 Position;
        public Vector2 Direction;
        public float Speed;
        public float MaxDistance;
        public float Radius;
        public int Pierce;
        public int Split;
        public bool Returns;
        public Health Homing;
        public float TurnRate;
        public string Sprite;
        public float Scale;
        public string ImpactVfx;
        public Color ImpactColor;
        public bool Rotate;
        public bool StopOnProps;
        public string ImpactSfx;
        /// <summary>Spell-effect power (0 = plain sprite, e.g. enemy spores). &gt; 0 adds a glowing trail,
        /// a following light, trail embers and a SpellFx impact; &gt;= 1.5 makes the impact heavy.</summary>
        public float Fx;
    }

    /// <summary>Moves in a straight (or homing) line, resolves hits through CombatWorld, handles
    /// pierce / split / return (U01, U02, U15). Purely pooled: Release() instead of Destroy.</summary>
    public sealed class Projectile : MonoBehaviour
    {
        ProjectileSpec spec;
        float traveled;
        int pierceLeft;
        bool returning;
        SpriteFlipbook flip;
        TrailRenderer trail;
        Light2D glow;
        SpriteFlipbook bloom;
        float nextTrail;
        Transform returnTo;
        readonly List<Health> hits = new List<Health>(8);
        static readonly List<Collider2D> worldHits = new List<Collider2D>(4);
        static ContactFilter2D worldFilter;
        static bool filterReady;

        public ProjectileSpec Spec => spec;
        public bool Alive { get; private set; }

        public static Projectile Launch(ProjectileSpec s, Transform owner = null)
        {
            var p = SpellPools.Instance.Spawn();
            if (p == null)
                return null;
            p.Begin(s, owner);
            return p;
        }

        void Begin(ProjectileSpec s, Transform owner)
        {
            spec = s;
            if (spec.Direction.sqrMagnitude < 0.0001f)
                spec.Direction = Vector2.right;
            spec.Direction.Normalize();
            if (spec.Scale <= 0) spec.Scale = 1f;
            if (spec.Radius <= 0) spec.Radius = 0.2f;
            traveled = 0;
            pierceLeft = spec.Pierce;
            returning = false;
            returnTo = owner;
            Alive = true;
            nextTrail = Time.time;
            transform.position = s.Position;
            transform.localScale = new Vector3(spec.Scale, spec.Scale, 1);
            flip = flip != null ? flip : GetComponent<SpriteFlipbook>();
            var set = GameDatabase.Instance != null ? GameDatabase.Instance.Sprites(spec.Sprite) : null;
            flip.Renderer.color = Color.white;
            flip.Renderer.sortingOrder = 5;
            flip.Play(set, true);
            Orient();
            SetupFx();
            if (!filterReady)
            {
                worldFilter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
                worldFilter.SetLayerMask(LayerMask.GetMask(StarbindLayers.WorldSolid));
                filterReady = true;
            }
        }

        ElementType FxElement => spec.Hit.Element;
        bool StarFire => spec.Sprite == "VFX_starfire";

        void SetupFx()
        {
            if (spec.Fx <= 0)
            {
                if (trail != null)
                    trail.emitting = false;
                return;
            }
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                var db = GameDatabase.Instance;
                if (db != null && db.unlitMaterial != null)
                    trail.sharedMaterial = db.unlitMaterial;
                trail.sortingLayerName = StarbindLayers.SortVfx;
                trail.sortingOrder = 4;
                trail.numCapVertices = 0;
                trail.minVertexDistance = 0.08f;
            }
            var col = StarFire ? new Color(1f, 0.8f, 0.34f) : SpellFx.ElementColor(FxElement);
            trail.Clear();
            trail.time = StarFire || spec.Homing != null ? 0.24f : 0.11f + 0.045f * spec.Fx;
            trail.widthMultiplier = (FxElement == ElementType.Fire && !StarFire ? 0.16f : 0.1f) * spec.Scale;
            trail.widthCurve = AnimationCurve.Linear(0, 1, 1, 0);
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.Lerp(col, Color.white, 0.6f), 0), new GradientColorKey(col, 0.35f), new GradientColorKey(col * 0.6f, 1) },
                alphaKeys = new[] { new GradientAlphaKey(0.88f, 0), new GradientAlphaKey(0.32f, 0.4f), new GradientAlphaKey(0, 1) },
            };
            trail.emitting = true;
            var v = VfxService.Instance;
            bloom = v.Glow(transform.position, new Color(col.r, col.g, col.b, 0.48f), 2.05f * spec.Scale, target: transform);
            glow = v.AttachLight(transform, col, 1.15f + 0.45f * spec.Fx, 0.78f);
        }

        void Orient()
        {
            if (spec.Rotate)
            {
                float a = Mathf.Atan2(spec.Direction.y, spec.Direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0, 0, Mathf.Round(a / 22.5f) * 22.5f); // 16 directions
                flip.Renderer.flipX = false;
            }
            else
            {
                transform.rotation = Quaternion.identity;
                flip.Renderer.flipX = spec.Direction.x < 0;
            }
        }

        void Update()
        {
            if (!Alive)
                return;
            float dt = Time.deltaTime;
            if (spec.Homing != null && !returning)
            {
                if (spec.Homing.IsDead || !spec.Homing.isActiveAndEnabled)
                    spec.Homing = CombatWorld.Instance.Nearest(transform.position, 6f, spec.Team, null, null, true);
                if (spec.Homing != null)
                {
                    var want = (spec.Homing.Center - (Vector2)transform.position).normalized;
                    spec.Direction = Vector3.RotateTowards(spec.Direction, want, spec.TurnRate * Mathf.Deg2Rad * dt, 0f);
                    Orient();
                }
            }
            if (returning && returnTo != null)
            {
                spec.Direction = ((Vector2)returnTo.position + Vector2.up * 0.4f - (Vector2)transform.position).normalized;
                Orient();
            }
            float step = spec.Speed * dt;
            transform.position += (Vector3)(spec.Direction * step);
            traveled += step;
            if (spec.Fx > 0 && Time.time >= nextTrail)
            {
                nextTrail = Time.time + 0.037f;
                SpellFx.Trail(FxElement, transform.position, spec.Direction, StarFire);
            }

            if (CheckHits())
                return;
            if (spec.StopOnProps && HitsProp())
            {
                End(true);
                return;
            }
            if (traveled >= spec.MaxDistance)
            {
                if (spec.Returns && !returning && returnTo != null)
                {
                    returning = true;
                    traveled = 0;
                    spec.Hit.Amount *= 0.6f;          // U15: return leg deals 60%
                    spec.Hit.CastId = CombatIds.NewCast(); // the return leg may hit targets again
                    pierceLeft = 99;
                    spec.MaxDistance = spec.MaxDistance + 2f;
                }
                else
                    End(spec.Team == Team.Player);
            }
            else if (returning && returnTo != null && ((Vector2)returnTo.position - (Vector2)transform.position).sqrMagnitude < 0.3f)
                End(false);
        }

        bool CheckHits()
        {
            var world = CombatWorld.Instance;
            world.Query(transform.position, spec.Radius, spec.Team, hits, 8);
            foreach (var h in hits)
            {
                var e = spec.Hit;
                e.Direction = spec.Direction;
                var r = world.Damage(e, h);
                if (!r.Applied)
                    continue;
                if (!string.IsNullOrEmpty(spec.ImpactSfx))
                    Sfx.Play(spec.ImpactSfx, 0.6f);
                if (spec.Fx > 0 && pierceLeft > 0)
                    SpellFx.Impact(FxElement, h.Center, 0.6f * spec.Scale); // each pierced target gets its own burst
                if (spec.Split > 0)
                {
                    if (spec.Fx > 0)
                        SpellFx.Impact(FxElement, transform.position, 0.8f * spec.Scale);
                    SplitInto(spec.Split);
                    spec.Split = 0;
                }
                if (pierceLeft-- <= 0)
                {
                    End(true);
                    return true;
                }
            }
            return false;
        }

        void SplitInto(int n)
        {
            for (int i = 0; i < n; i++)
            {
                float ang = (i - (n - 1) * 0.5f) * 50f + 180f * 0f;
                var child = spec;
                child.Split = 0;
                child.Pierce = 0;
                child.Returns = false;
                child.Scale = spec.Scale * 0.7f;
                child.Hit.Amount = spec.Hit.Amount * 0.4f;
                child.Direction = Quaternion.Euler(0, 0, ang + (n == 2 ? 0 : 0)) * spec.Direction;
                child.Position = transform.position;
                child.MaxDistance = 3.5f;
                Launch(child, null);
            }
        }

        bool HitsProp()
        {
            int n = Physics2D.OverlapPoint(transform.position, worldFilter, worldHits);
            for (int i = 0; i < n; i++)
            {
                var c = worldHits[i];
                // tilemap = water/cliff cells (projectiles fly over them); bodies with Health are hit by CheckHits instead
                if (c is UnityEngine.Tilemaps.TilemapCollider2D || c.GetComponentInParent<Health>() != null)
                    continue;
                return true;
            }
            return false;
        }

        public void End(bool impact)
        {
            if (!Alive)
                return;
            Alive = false;
            if (bloom != null)
            {
                VfxService.Instance.Release(bloom);
                bloom = null;
            }
            if (glow != null)
            {
                VfxService.Instance.DetachLight(glow);
                glow = null;
            }
            if (impact && spec.Fx > 0)
                SpellFx.Impact(FxElement, transform.position, spec.Scale * Mathf.Min(spec.Fx, 2f), spec.Fx >= 1.5f, StarFire);
            else if (impact && !string.IsNullOrEmpty(spec.ImpactVfx))
            {
                VfxService.Instance.Play(spec.ImpactVfx, transform.position, 0f, spec.Scale);
                VfxService.Instance.Sparks(transform.position, spec.ImpactColor.a > 0 ? spec.ImpactColor : Color.white, 5, 3f);
            }
            SpellPools.Instance.Projectiles.Release(this);
        }
    }
}
