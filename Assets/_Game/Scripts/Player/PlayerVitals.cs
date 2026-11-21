using System;
using Starbind.Combat;
using Starbind.Core;
using UnityEngine;

namespace Starbind.Player
{
    /// <summary>HP (via Health), MP and their regeneration, plus the player's StatBlock (GDD §4.2).
    /// Hurt invulnerability 0.45 s. Stats drive max HP, MP regen and move speed.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class PlayerVitals : MonoBehaviour
    {
        public const float BaseHp = 100f;
        public const float BaseMp = 100f;
        public const float BaseMpRegen = 4f;
        public const float HurtInvulnerable = 0.45f;

        public Health Health { get; private set; }
        public StatBlock Stats { get; } = new StatBlock();
        public float Mp { get; private set; } = BaseMp;
        public float MaxMp => Stats.Get(StatId.MaxMp);
        public float MpNormalized => MaxMp > 0 ? Mp / MaxMp : 0;
        public event Action ManaDenied;

        void Awake()
        {
            Health = GetComponent<Health>();
            Stats.SetBase(StatId.MaxHp, BaseHp);
            Stats.SetBase(StatId.MaxMp, BaseMp);
            Stats.SetBase(StatId.MpRegen, BaseMpRegen);
            Stats.SetBase(StatId.MoveSpeed, 1f);
            Stats.SetBase(StatId.Damage, 1f);
            Stats.SetBase(StatId.ReactionDamage, 1f);
            Stats.SetBase(StatId.ResonanceGain, 1f);
            Stats.SetBase(StatId.IceDuration, 1f);
            Stats.SetBase(StatId.BurnDuration, 1f);
            Stats.SetBase(StatId.PullRadius, 1f);
            Stats.SetBase(StatId.CooldownRate, 1f);
            Stats.SetBase(StatId.ManaOnHit, 1f);
            Stats.SetBase(StatId.ChilledDamage, 0f);
            Stats.Changed += ApplyStats;
            Health.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            Stats.Changed -= ApplyStats;
            if (Health != null)
                Health.Damaged -= OnDamaged;
        }

        void Start() => ApplyStats();

        void ApplyStats()
        {
            float max = Stats.Get(StatId.MaxHp);
            if (Mathf.Abs(max - Health.Max) > 0.01f)
                Health.SetMax(max, false);
            Mp = Mathf.Min(Mp, MaxMp);
            if (TryGetComponent(out PlayerMotor motor))
                motor.SpeedMultiplier = Stats.Get(StatId.MoveSpeed);
        }

        void OnDamaged(Health h, DamageEvent e, DamageResult r)
        {
            if (r.Applied && !h.IsDead)
                h.InvulnerableUntil = Time.time + HurtInvulnerable;
        }

        void Update()
        {
            if (Health.IsDead)
                return;
            Mp = Mathf.Min(MaxMp, Mp + Stats.Get(StatId.MpRegen) * Time.deltaTime);
        }

        public bool TrySpendMana(float amount)
        {
            if (Mp + 0.0001f < amount)
            {
                ManaDenied?.Invoke();
                return false;
            }
            Mp -= amount;
            return true;
        }

        public void RestoreMana(float amount) => Mp = Mathf.Min(MaxMp, Mp + Mathf.Max(0, amount));

        public void FullRestore()
        {
            Health.ResetHealth();
            Health.SetMax(Stats.Get(StatId.MaxHp), true);
            Mp = MaxMp;
        }
    }
}
