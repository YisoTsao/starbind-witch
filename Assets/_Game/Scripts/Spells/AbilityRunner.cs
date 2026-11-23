using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Spells
{
    public enum CastResult { Cast, OnCooldown, NoMana, NoTarget, Blocked }

    /// <summary>Shared cast pipeline for active (Q/E) and auto spells (GDD §6, §13.4): cooldown, MP,
    /// CastContext, then the spell form (projectile / strike / orbit / pull / homing). Auto casts pay no
    /// MP. Damage always goes through CombatWorld; this class never computes HP changes itself.</summary>
    public sealed class AbilityRunner : MonoBehaviour
    {
        [SerializeField] Transform castOrigin;

        public float DamageMultiplier { get; set; } = 1f;
        public float CooldownMultiplier { get; set; } = 1f;
        public float IceDurationMultiplier { get; set; } = 1f;
        public float BurnDurationMultiplier { get; set; } = 1f;
        public float PullRadiusMultiplier { get; set; } = 1f;
        public Func<float, bool> SpendMana { get; set; } = _ => true;
        public Func<int> SourceEntity { get; set; } = () => 0;
        public OrbitRing Orbit { get; private set; }

        public event Action<RuntimeSpell, CastContext> Cast;
        public int CastsThisFrame { get; private set; }
        int frame;

        Vector2 Origin => castOrigin != null ? (Vector2)castOrigin.position : (Vector2)transform.position + new Vector2(0, 0.45f);

        void Awake()
        {
            Orbit = GetComponentInChildren<OrbitRing>();
            if (Orbit == null)
            {
                var go = new GameObject("OrbitRing");
                go.transform.SetParent(transform, false);
                Orbit = go.AddComponent<OrbitRing>();
            }
        }

        public float EffectiveCooldown(RuntimeSpell s) => s.Cooldown * CooldownMultiplier;

        public float CooldownRemaining(RuntimeSpell s) => s == null ? 0 : Mathf.Max(0, s.ReadyAt - Time.time);

        public float CooldownFraction(RuntimeSpell s)
        {
            if (s == null)
                return 0;
            float cd = EffectiveCooldown(s);
            return cd <= 0 ? 0 : Mathf.Clamp01(CooldownRemaining(s) / cd);
        }

        public bool IsReady(RuntimeSpell s) => s != null && Time.time >= s.ReadyAt;

        /// <summary>Casts <paramref name="s"/> toward <paramref name="aimPoint"/>. For Pulse/Homing forms
        /// <paramref name="target"/> is used when given.</summary>
        public CastResult TryCast(RuntimeSpell s, Vector2 aimPoint, Health target = null)
        {
            if (s == null)
                return CastResult.Blocked;
            if (Time.frameCount != frame)
            {
                frame = Time.frameCount;
                CastsThisFrame = 0;
            }
            if (!IsReady(s))
                return CastResult.OnCooldown;
            bool auto = s.Trigger == SpellTrigger.Auto;
            if (!auto && s.MpCost > 0 && !SpendMana(s.MpCost))
                return CastResult.NoMana;

            var origin = Origin;
            var dir = aimPoint - origin;
            if (dir.sqrMagnitude < 0.0001f)
                dir = Vector2.right;
            dir.Normalize();
            var ctx = new CastContext
            {
                CastId = CombatIds.NewCast(),
                SourceSpellId = s.Id,
                Origin = origin,
                Direction = dir,
                DamageMultiplier = DamageMultiplier,
                Source = auto ? HitSource.AutoSpell : HitSource.ActiveSpell,
            };
            s.ReadyAt = Time.time + EffectiveCooldown(s);
            s.CastCount++;
            CastsThisFrame++;
            Execute(s, ctx, aimPoint, target);
            Cast?.Invoke(s, ctx);
            if (CombatWorld.Exists)
                CombatWorld.Instance.Bus.RaiseSpellCast(s.Id, ctx);
            return CastResult.Cast;
        }

        float StatusMul(RuntimeSpell s) => s.Element == ElementType.Ice ? IceDurationMultiplier : s.Element == ElementType.Fire ? BurnDurationMultiplier : 1f;

        DamageEvent HitFor(RuntimeSpell s, in CastContext ctx) => new DamageEvent
        {
            CastId = ctx.CastId,
            SourceEntityId = SourceEntity(),
            Amount = s.Damage * ctx.DamageMultiplier,
            Element = s.Element,
            StatusDuration = s.StatusDuration * StatusMul(s),
            Source = ctx.Source,
            Direction = ctx.Direction,
            Knockback = 0.8f,
        };

        void Execute(RuntimeSpell s, CastContext ctx, Vector2 aimPoint, Health target)
        {
            var v = VfxService.Instance;
            switch (s.Form)
            {
                case SpellForm.Missile:
                {
                    bool ice = s.Element == ElementType.Ice;
                    for (int i = 0; i < s.Count; i++)
                    {
                        float spread = (i - (s.Count - 1) * 0.5f) * 12f;
                        var spec = new ProjectileSpec
                        {
                            Team = Team.Player,
                            Hit = HitFor(s, ctx),
                            Position = ctx.Origin,
                            Direction = Quaternion.Euler(0, 0, spread) * ctx.Direction,
                            Speed = s.Speed,
                            MaxDistance = s.Range,
                            Radius = s.Radius,
                            Pierce = s.Pierce,
                            Split = s.Split,
                            Returns = s.Returns,
                            Sprite = ice ? "VFX_ice_shard" : "VFX_fireball",
                            Rotate = true,
                            ImpactVfx = ice ? "VFX_ice_burst" : "VFX_explosion",
                            ImpactColor = ice ? new Color(0.7f, 0.95f, 1f) : new Color(1f, 0.6f, 0.2f),
                            Scale = 1f,
                            StopOnProps = true,
                            ImpactSfx = ice ? "sfx_ice_hit" : "sfx_fire_hit",
                            Fx = 1f,
                        };
                        Projectile.Launch(spec, transform);
                    }
                    SpellFx.Cast(s.Element, transform.position, ctx.Origin + ctx.Direction * 0.35f, ctx.Direction, s.Count > 1 ? 1.3f : 1f);
                    Sfx.Play(ice ? "sfx_ice_cast" : "sfx_fire_cast", 0.7f);
                    break;
                }
                case SpellForm.Area:
                {
                    var p = ctx.Origin + Vector2.ClampMagnitude(aimPoint - ctx.Origin, s.Range);
                    v.Play("VFX_telegraph_circle", p, 0, s.Radius * 2f / 2f, new Color(1f, 0.95f, 0.5f, 0.6f), StarbindLayers.SortGroundDecal, 2);
                    v.Play("VFX_magic_circle", p, 0, s.Radius * 1.12f, new Color(0.81f, 0.59f, 1f, 0.86f), StarbindLayers.SortGroundDecal, 3);
                    SpellFx.Cast(s.Element, transform.position, ctx.Origin + ctx.Direction * 0.35f, ctx.Direction);
                    var e = HitFor(s, ctx);
                    e.Knockback = 1.2f;
                    AreaStrike.Schedule(p, 0.18f, s.Radius, e, Team.Player, "VFX_lightning", new Color(1f, 0.95f, 0.5f), "sfx_thunder", fx: 1.5f);
                    break;
                }
                case SpellForm.Pulse:
                {
                    var p = target != null ? target.Center : aimPoint;
                    var e = HitFor(s, ctx);
                    AreaStrike.Schedule(p, 0.05f, s.Radius, e, Team.Player, "VFX_lightning", new Color(1f, 0.95f, 0.5f), "sfx_thunder", fx: 1f, groundPulse: true);
                    v.Bolt(ctx.Origin, p, new Color(0.72f, 0.49f, 1f), 0.14f, 2, 3.5f);
                    break;
                }
                case SpellForm.Orbit:
                    Orbit.Configure(s, SourceEntity(), ctx.DamageMultiplier, s.StatusDuration * IceDurationMultiplier);
                    break;
                case SpellForm.Pull:
                {
                    var p = ctx.Origin + Vector2.ClampMagnitude(aimPoint - ctx.Origin, s.Range);
                    var e = HitFor(s, ctx);
                    e.Knockback = 0;
                    SpellFx.Cast(s.Element, transform.position, ctx.Origin + ctx.Direction * 0.35f, ctx.Direction);
                    PullField.Open(p, s.Radius * PullRadiusMultiplier, s.Duration, e);
                    break;
                }
                case SpellForm.Homing:
                {
                    bool star = s.Element == ElementType.Fire;
                    var world = CombatWorld.Instance;
                    for (int i = 0; i < s.Count; i++)
                    {
                        var t = i == 0 && target != null ? target : world.Nearest(ctx.Origin, s.Range, Team.Player, null, null, true);
                        float a = (i - (s.Count - 1) * 0.5f) * 40f + 90f;
                        var spec = new ProjectileSpec
                        {
                            Team = Team.Player,
                            Hit = HitFor(s, ctx),
                            Position = ctx.Origin,
                            Direction = Quaternion.Euler(0, 0, a) * Vector2.right,
                            Speed = s.Speed,
                            MaxDistance = s.Range * 1.6f,
                            Radius = s.Radius,
                            Homing = t,
                            TurnRate = 540f,
                            Sprite = star ? "VFX_starfire" : "VFX_homing",
                            ImpactVfx = "VFX_hit",
                            ImpactColor = star ? new Color(1f, 0.9f, 0.4f) : new Color(0.8f, 0.6f, 1f),
                            Scale = 1f,
                            Fx = 0.8f,
                        };
                        spec.Hit.CastId = CombatIds.NewCast(); // each missile is its own hit event
                        Projectile.Launch(spec, transform);
                    }
                    var col = star ? new Color(1f, 0.82f, 0.43f) : SpellFx.ElementColor(s.Element);
                    v.Play("VFX_flash", ctx.Origin, star ? 0f : 45f, 0.54f, Color.Lerp(col, Color.white, 0.55f));
                    v.Glow(ctx.Origin, new Color(col.r, col.g, col.b, 0.45f), 2.15f, 0.2f);
                    Sfx.Play("sfx_homing", 0.4f);
                    break;
                }
            }
        }
    }
}
