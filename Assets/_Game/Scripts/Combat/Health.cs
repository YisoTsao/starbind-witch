using System;
using Starbind.Core;
using UnityEngine;

namespace Starbind.Combat
{
    /// <summary>Hit points, team, element marks and invulnerability of one combat entity. Damage only
    /// arrives through CombatWorld.Damage (the DamageResolver); this component just stores state.</summary>
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] Team team = Team.Enemy;
        [SerializeField] float maxHp = 45f;
        [SerializeField] float radius = 0.3f;
        [SerializeField] Vector2 centerOffset = new Vector2(0f, 0.35f);
        [SerializeField] bool immortal; // training dummies: never die, refill instead

        public int EntityId { get; private set; }
        public Team Team => team;
        public float Max => maxHp;
        public float Current { get; private set; }
        public float Radius => radius;
        public bool IsDead { get; private set; }
        public bool Immortal => immortal;
        /// <summary>False for scenery that only reacts to specific elements (burnable vines): auto
        /// spells never pick it as a target.</summary>
        public bool AutoTargetable { get; set; } = true;
        public StatusSet Status { get; } = new StatusSet();
        public float InvulnerableUntil { get; set; } = -1f;
        public float DamageTakenMultiplier { get; set; } = 1f;
        public float LastDamagedAt { get; private set; } = -10f;
        public float TotalDamageTaken { get; private set; }
        public Vector2 Center => (Vector2)transform.position + centerOffset;
        public float Normalized => maxHp > 0 ? Current / maxHp : 0f;

        public event Action<Health, DamageEvent, DamageResult> Damaged;
        public event Action<Health> Died;
        public event Action<Health> Changed;

        public void Configure(Team t, float max, float r, Vector2 center, bool isImmortal = false)
        {
            team = t;
            maxHp = max;
            radius = r;
            centerOffset = center;
            immortal = isImmortal;
            ResetHealth();
        }

        void Awake()
        {
            EntityId = CombatIds.NewEntity();
            if (Current <= 0 && !IsDead)
                Current = maxHp;
        }

        void OnEnable() => CombatWorld.Register(this);
        void OnDisable() => CombatWorld.Unregister(this);

        public void ResetHealth()
        {
            Current = maxHp;
            IsDead = false;
            Status.Clear();
            DamageTakenMultiplier = 1f;
            TotalDamageTaken = 0;
            Changed?.Invoke(this);
        }

        public void SetMax(float max, bool fill)
        {
            float delta = max - maxHp;
            maxHp = Mathf.Max(1f, max);
            Current = fill ? maxHp : Mathf.Clamp(Current + Mathf.Max(0, delta), 0, maxHp);
            Changed?.Invoke(this);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0)
                return;
            Current = Mathf.Min(maxHp, Current + amount);
            Changed?.Invoke(this);
        }

        /// <summary>Called by the DamageResolver only. Returns the damage actually dealt.</summary>
        internal float Take(float amount, float now)
        {
            if (IsDead || amount <= 0)
                return 0;
            float dealt = Mathf.Min(Current, amount);
            Current -= amount;
            TotalDamageTaken += amount;
            LastDamagedAt = now;
            if (Current <= 0)
            {
                if (immortal)
                    Current = maxHp; // dummy refills; still reports the full hit
                else
                {
                    Current = 0;
                    IsDead = true;
                }
            }
            Changed?.Invoke(this);
            return immortal ? amount : dealt;
        }

        internal void RaiseDamaged(in DamageEvent e, in DamageResult r) => Damaged?.Invoke(this, e, r);

        internal void RaiseDied() => Died?.Invoke(this);

        /// <summary>Kills regardless of HP (debug, scripted events).</summary>
        public void Kill()
        {
            if (IsDead || immortal)
                return;
            Current = 0;
            IsDead = true;
            Changed?.Invoke(this);
            Died?.Invoke(this);
        }
    }
}
