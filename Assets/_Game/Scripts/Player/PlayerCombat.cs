using System;
using System.Collections.Generic;
using Starbind.CameraRig;
using Starbind.Combat;
using Starbind.Core;
using Starbind.Items;
using Starbind.Spells;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Player
{
    /// <summary>Player attacks (GDD §4.1-4.4, §7): staff 3-hit combo (tap), charged magic bolt (hold and
    /// release, or right button), Q/E active spells, R Arcane Overdrive, 1/2 potions, resonance and
    /// MP-on-hit bookkeeping. Every hit is resolved by CombatWorld; this class only starts attacks.</summary>
    [RequireComponent(typeof(PlayerVitals), typeof(PlayerMotor), typeof(PlayerBuild))]
    public sealed class PlayerCombat : MonoBehaviour
    {
        public const float StaffReach = 1.15f;
        public const float FinisherReach = 1.45f;
        public const float StaffHalfAngle = 70f;
        public const float HpPotionHeal = 30f;
        public const float MpPotionRestore = 35f;
        public const float PerfectDodgeBonus = 10f;
        public const float OverdriveCooldownMul = 0.75f;
        public const float OverdriveReactionMul = 1.35f;

        PlayerVitals vitals;
        PlayerMotor motor;
        PlayerBuild build;
        AbilityRunner runner;
        Camera cam;
        readonly StaffCombo combo = new StaffCombo();
        readonly ManaOnHitLimiter manaOnHit = new ManaOnHitLimiter();
        readonly List<Health> hits = new List<Health>(16);
        CombatWorld subscribedWorld;
        SpriteFlipbook chargeFx;
        SpriteFlipbook overdriveFx;
        float pressStart = -1f;
        bool charging;
        bool perfectDodgeReady;
        int lastFinisherCast = -1;

        public ResonanceMeter Resonance { get; } = new ResonanceMeter();
        public Inventory Bag { get; set; } = new Inventory();
        public Vector2? AimOverride { get; set; }
        public bool InputEnabled { get; set; } = true;
        public bool IsCharging => charging;
        public float ChargeHeld => charging ? Time.time - pressStart : 0f;
        public int StaffStepsStarted { get; private set; }
        public float AttackLockedUntil { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.down;
        public bool OverdriveActive => Resonance.IsActive(Time.time);
        /// <summary>Moonring staff: the charged bolt returns.</summary>
        public bool BoltReturns { get; set; }
        /// <summary>Storm staff: every third effective cast adds a small arc.</summary>
        public bool StormArc { get; set; }
        public int EffectiveCasts { get; private set; }
        public int StormArcs { get; private set; }
        public event Action<string> Notice;

        void Awake()
        {
            vitals = GetComponent<PlayerVitals>();
            motor = GetComponent<PlayerMotor>();
            build = GetComponent<PlayerBuild>();
            runner = GetComponent<AbilityRunner>();
            runner.SpendMana = vitals.TrySpendMana;
            runner.SourceEntity = () => vitals.Health.EntityId;
            runner.Cast += OnSpellCast;
            Resonance.Started += OnOverdriveStarted;
            Resonance.Ended += OnOverdriveEnded;
        }

        void OnEnable()
        {
            subscribedWorld = CombatWorld.Instance;
            subscribedWorld.Bus.Hit += OnHit;
            subscribedWorld.Bus.ReactionTriggered += OnReaction;
            subscribedWorld.Bus.Killed += OnKilled;
        }

        void OnDisable()
        {
            if (subscribedWorld != null)
            {
                subscribedWorld.Bus.Hit -= OnHit;
                subscribedWorld.Bus.ReactionTriggered -= OnReaction;
                subscribedWorld.Bus.Killed -= OnKilled;
            }
            subscribedWorld = null;
            StopCharge();
        }

        // ------------------------------------------------------------------ aim
        public Vector2 Center => (Vector2)transform.position + new Vector2(0, 0.45f);

        public Vector2 AimPoint(Controls.PlayerInputReader input)
        {
            if (AimOverride.HasValue)
                return AimOverride.Value;
            if (input != null)
            {
                var stick = input.AimStick;
                if (stick.sqrMagnitude > 0.09f)
                    return Center + stick.normalized * 4f;
                if (input.PointerActive)
                {
                    if (cam == null)
                        cam = Camera.main;
                    if (cam != null)
                    {
                        var s = input.AimScreen;
                        var w = cam.ScreenToWorldPoint(new Vector3(s.x, s.y, -cam.transform.position.z));
                        return w;
                    }
                }
            }
            return Center + motor.Facing * 3f;
        }

        // ------------------------------------------------------------------ attack button
        public void AttackDown()
        {
            if (!CanAct())
                return;
            pressStart = Time.time;
        }

        public void AttackUp(Vector2 aim)
        {
            if (pressStart < 0)
                return;
            if (charging)
                ReleaseCharge(aim);
            else if (CanAct())
                Staff(aim);
            pressStart = -1f;
        }

        /// <summary>Right button: start charging immediately (GDD §4.3 alternative scheme).</summary>
        public void ChargeDown()
        {
            if (!CanAct())
                return;
            pressStart = Time.time - ChargeRules.TapThreshold;
            BeginCharge();
        }

        public void ChargeUp(Vector2 aim)
        {
            if (charging)
                ReleaseCharge(aim);
            pressStart = -1f;
        }

        bool CanAct() => InputEnabled && !vitals.Health.IsDead && !GamePause.IsPaused;

        void BeginCharge()
        {
            charging = true;
            if (chargeFx == null)
            {
                chargeFx = VfxService.Instance.Play("VFX_charge", Center, 0, 1f, null, StarbindLayers.SortVfx, 3, true, transform);
                if (chargeFx != null)
                    chargeFx.FollowOffset = new Vector2(0, 0.45f);
            }
            Sfx.Play("sfx_charge", 0.5f);
        }

        void StopCharge()
        {
            charging = false;
            if (chargeFx != null && VfxService.Instance != null)
                VfxService.Instance.Release(chargeFx);
            chargeFx = null;
        }

        // ------------------------------------------------------------------ staff
        /// <summary>Starts (or buffers) the next staff combo step toward <paramref name="aim"/>.</summary>
        public int Staff(Vector2 aim)
        {
            AimDirection = Dir(aim);
            int step = combo.Press(Time.time);
            if (step >= 0)
                Swing(step);
            return step;
        }

        Vector2 Dir(Vector2 aim)
        {
            var d = aim - Center;
            return d.sqrMagnitude > 0.0001f ? d.normalized : motor.Facing;
        }

        void Swing(int step)
        {
            StaffStepsStarted++;
            motor.Face(AimDirection);
            AttackLockedUntil = Time.time + StaffCombo.StepDuration[step];
            bool finisher = step == 2;
            float reach = finisher ? FinisherReach : StaffReach;
            var world = CombatWorld.Instance;
            var e = new DamageEvent
            {
                CastId = CombatIds.NewCast(),
                SourceEntityId = vitals.Health.EntityId,
                Amount = StaffCombo.Damage[step] * vitals.Stats.Get(StatId.Damage),
                Source = finisher ? HitSource.StaffFinisher : HitSource.Staff,
                Knockback = finisher ? 2.4f : 0.7f,
                Direction = AimDirection,
            };
            var center = Center + AimDirection * 0.25f;
            if (finisher)
                world.Query(center + AimDirection * 0.5f, reach * 0.8f, Team.Player, hits);
            else
                world.QueryArc(center, AimDirection, reach, StaffHalfAngle, Team.Player, hits);
            int landed = 0;
            foreach (var h in hits)
                if (world.Damage(e, h).Applied)
                    landed++;

            float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
            var v = VfxService.Instance;
            var fx = v.Play("VFX_slash", center + AimDirection * 0.3f, angle, finisher ? 1.25f : 1f, finisher ? new Color(1f, 0.9f, 1f) : Color.white);
            if (fx != null)
                fx.Renderer.flipY = step == 1;
            Sfx.Play(finisher ? "sfx_staff_heavy" : "sfx_staff", 0.7f);
            if (finisher)
            {
                v.Play("VFX_thunder_ring", center + AimDirection * 0.5f, 0, 1.0f, new Color(0.85f, 0.7f, 1f));
                v.Light(center + AimDirection * 0.5f, new Color(0.8f, 0.6f, 1f), 2.5f, 1.4f, 0.2f);
                if (landed > 0)
                {
                    CameraFollow2D.Main?.Shake(0.08f, 0.15f);
                    GamePause.HitStop(0.05f);
                }
            }
            else if (landed > 0)
                GamePause.HitStop(0.025f);
        }

        // ------------------------------------------------------------------ charged bolt
        void ReleaseCharge(Vector2 aim)
        {
            float held = Time.time - pressStart;
            StopCharge();
            int level = ChargeRules.Level(held);
            if (level == 0)
                return;
            FireBolt(aim, held);
        }

        /// <summary>Fires the charged magic bolt for a hold of <paramref name="heldSeconds"/>.</summary>
        public Projectile FireBolt(Vector2 aim, float heldSeconds)
        {
            AimDirection = Dir(aim);
            motor.Face(AimDirection);
            int level = Mathf.Max(1, ChargeRules.Level(heldSeconds));
            float dmg = Mathf.Max(ChargeRules.MinDamage, ChargeRules.Damage(heldSeconds));
            if (perfectDodgeReady)
            {
                dmg += PerfectDodgeBonus;
                perfectDodgeReady = false;
            }
            AttackLockedUntil = Time.time + 0.18f;
            var spec = new ProjectileSpec
            {
                Team = Team.Player,
                Hit = new DamageEvent
                {
                    CastId = CombatIds.NewCast(),
                    SourceEntityId = vitals.Health.EntityId,
                    Amount = dmg * vitals.Stats.Get(StatId.Damage),
                    Source = HitSource.ChargedBolt,
                    Knockback = 1.6f,
                },
                Position = Center + AimDirection * 0.3f,
                Direction = AimDirection,
                Speed = 11f,
                MaxDistance = 8f,
                Radius = 0.22f + 0.06f * level,
                Pierce = level - 1,
                Returns = BoltReturns,
                Sprite = "VFX_magic_bolt",
                Scale = 0.9f + 0.2f * level,
                ImpactVfx = "VFX_ice_burst",
                ImpactColor = new Color(0.85f, 0.65f, 1f),
                StopOnProps = true,
                ImpactSfx = "sfx_bolt_hit",
            };
            Sfx.Play("sfx_bolt", 0.8f);
            VfxService.Instance.Light(Center, new Color(0.75f, 0.55f, 1f), 2f, 1.2f, 0.15f);
            return Projectile.Launch(spec, transform);
        }

        // ------------------------------------------------------------------ spells / overdrive / items
        public CastResult CastSlot(int slot, Vector2 aim)
        {
            if (!CanAct())
                return CastResult.Blocked;
            var s = build.Active(slot);
            if (s == null)
                return CastResult.Blocked;
            var r = runner.TryCast(s, aim);
            if (r == CastResult.Cast)
            {
                AimDirection = Dir(aim);
                motor.Face(AimDirection);
                AttackLockedUntil = Time.time + 0.15f;
            }
            else if (r == CastResult.NoMana)
                Notice?.Invoke("魔力不足");
            return r;
        }

        public bool TryOverdrive()
        {
            if (!CanAct())
                return false;
            if (!Resonance.TryActivate(Time.time))
            {
                if (!Resonance.IsActive(Time.time))
                    Notice?.Invoke("共鳴未滿");
                return false;
            }
            return true;
        }

        void OnOverdriveStarted()
        {
            var v = VfxService.Instance;
            overdriveFx = v.Play("VFX_overdrive_aura", transform.position, 0, 1.2f, null, StarbindLayers.SortGroundDecal, 4, true, transform);
            if (overdriveFx != null)
                overdriveFx.FollowOffset = new Vector2(0, 0.1f);
            v.Play("VFX_levelup", Center, 0, 2f, new Color(1f, 0.85f, 1f));
            v.Light(Center, new Color(0.85f, 0.6f, 1f), 5f, 2.2f, 0.6f);
            v.Sparks(Center, new Color(1f, 0.9f, 0.5f), 30, 6f, 0.6f);
            CameraFollow2D.Main?.Shake(0.12f, 0.35f);
            Sfx.Play("sfx_overdrive", 1f);
            Notice?.Invoke("魔典超載！");
        }

        void OnOverdriveEnded()
        {
            if (overdriveFx != null)
                VfxService.Instance.Release(overdriveFx);
            overdriveFx = null;
            Sfx.Play("sfx_overdrive_end", 0.6f);
        }

        public bool UseItem(int slot)
        {
            if (!CanAct())
                return false;
            if (slot == 1)
            {
                if (vitals.Health.Current >= vitals.Health.Max - 0.5f || !Bag.TryRemove(Inventory.HpPotion, 1))
                    return false;
                vitals.Health.Heal(HpPotionHeal);
                VfxService.Instance.Play("VFX_levelup", Center, 0, 1f, new Color(1f, 0.5f, 0.55f));
            }
            else if (slot == 2)
            {
                if (vitals.Mp >= vitals.MaxMp - 0.5f || !Bag.TryRemove(Inventory.MpPotion, 1))
                    return false;
                vitals.RestoreMana(MpPotionRestore);
                VfxService.Instance.Play("VFX_levelup", Center, 0, 1f, new Color(0.5f, 0.7f, 1f));
            }
            else
                return false;
            Sfx.Play("sfx_potion", 0.8f);
            return true;
        }

        /// <summary>Called by enemy attacks that connected while the dash was invulnerable.</summary>
        public void NotifyPerfectDodge(bool important)
        {
            if (perfectDodgeReady && !important)
                return;
            perfectDodgeReady = true;
            if (important)
                Resonance.Add(HitSource.Enemy, Time.time, perfectDodge: true);
            VfxService.Instance.Sparks(Center, new Color(0.7f, 1f, 0.9f), 12, 4f);
            Notice?.Invoke("完美閃避");
        }

        public bool PerfectDodgeReady => perfectDodgeReady;

        void OnSpellCast(RuntimeSpell s, CastContext ctx)
        {
            if (s.Trigger != SpellTrigger.Active)
                return;
            EffectiveCasts++;
            if (!StormArc || EffectiveCasts % 3 != 0)
                return;
            // storm staff proc: one small arc chain (proc depth 1, so it cannot start reactions)
            var world = CombatWorld.Instance;
            var from = world.Nearest(Center, 6f, Team.Player, null, null, true);
            if (from == null)
                return;
            StormArcs++;
            var e = new DamageEvent
            {
                CastId = CombatIds.NewCast(), SourceEntityId = vitals.Health.EntityId, Amount = 10f * vitals.Stats.Get(StatId.Damage),
                Element = ElementType.Lightning, StatusDuration = 1.5f, Source = HitSource.AutoSpell, ProcDepth = 1,
            };
            world.Bus.RaiseArc(Center, from.Center);
            world.Damage(e, from);
            var next = world.Nearest(from.Center, 3f, Team.Player, from, null, true);
            if (next != null)
            {
                world.Bus.RaiseArc(from.Center, next.Center);
                world.Damage(e, next);
            }
            Sfx.Play("sfx_thunder", 0.4f);
        }

        // ------------------------------------------------------------------ bus listeners
        void OnHit(Health target, DamageEvent e, DamageResult r)
        {
            if (!r.Applied || e.SourceEntityId != vitals.Health.EntityId)
                return;
            float now = Time.time;
            if (e.Source == HitSource.Staff || e.Source == HitSource.StaffFinisher)
                vitals.RestoreMana(manaOnHit.Grant(now, vitals.Stats.Get(StatId.ManaOnHit)));
            if (e.Source == HitSource.StaffFinisher && e.CastId != lastFinisherCast)
            {
                lastFinisherCast = e.CastId;
                Resonance.Add(e.Source, now, e.CastId);
            }
            else if (e.Source == HitSource.ActiveSpell)
                Resonance.Add(e.Source, now, e.CastId);
        }

        void OnReaction(ReactionType r, Vector2 p, Health target) => Resonance.Add(HitSource.Reaction, Time.time, reaction: true);

        void OnKilled(Health h, DamageEvent e)
        {
            if (h.Team == Team.Enemy)
                Resonance.Add(HitSource.Reaction, Time.time, kill: true);
        }

        // ------------------------------------------------------------------ tick
        void Update()
        {
            float now = Time.time;
            Resonance.GainMultiplier = 1f;
            Resonance.ReactionGainMultiplier = vitals.Stats.Get(StatId.ResonanceGain);
            Resonance.Tick(now);
            bool od = Resonance.IsActive(now);
            runner.DamageMultiplier = vitals.Stats.Get(StatId.Damage);
            runner.CooldownMultiplier = vitals.Stats.Get(StatId.CooldownRate) * (od ? OverdriveCooldownMul : 1f);
            runner.IceDurationMultiplier = vitals.Stats.Get(StatId.IceDuration);
            runner.BurnDurationMultiplier = vitals.Stats.Get(StatId.BurnDuration);
            runner.PullRadiusMultiplier = vitals.Stats.Get(StatId.PullRadius);
            var world = CombatWorld.Instance;
            world.ReactionDamageMultiplier = vitals.Stats.Get(StatId.ReactionDamage) * (od ? OverdriveReactionMul : 1f);
            world.ChainBonus = build.ChainBonus;
            world.ChilledBonus = vitals.Stats.Get(StatId.ChilledDamage);

            int buffered = combo.Tick(now);
            if (buffered >= 0)
                Swing(buffered);

            if (pressStart >= 0 && !charging && ChargeRules.IsCharge(now - pressStart) && CanAct())
                BeginCharge();
            motor.AttackSlow = charging ? 0.55f : now < AttackLockedUntil ? 0.45f : 1f;
            if (charging && chargeFx != null)
                chargeFx.transform.localScale = Vector3.one * (0.8f + 0.25f * ChargeRules.Level(now - pressStart));
        }
    }
}
