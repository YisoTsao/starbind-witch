using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.World
{
    /// <summary>Training dummy (GDD §3.4): neutral, immortal target for practising the staff combo and
    /// reactions; counts hits and total damage for the HUD/tutorial.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        Health health;
        HitFlash flash;
        float wobbleUntil;
        Transform visual;

        public int Hits { get; private set; }
        public float TotalDamage { get; private set; }
        public int Reactions { get; private set; }

        void Awake()
        {
            health = GetComponent<Health>();
            flash = GetComponent<HitFlash>();
            visual = transform.Find("Visual");
            health.Damaged += OnDamaged;
        }

        void OnDestroy() => health.Damaged -= OnDamaged;

        void OnDamaged(Health h, DamageEvent e, DamageResult r)
        {
            if (!r.Applied)
                return;
            Hits++;
            TotalDamage += r.Dealt;
            if (r.Reaction != ReactionType.None)
                Reactions++;
            flash?.Flash(0.08f);
            wobbleUntil = Time.time + 0.25f;
        }

        void Update()
        {
            if (visual == null)
                return;
            float k = Mathf.Max(0, wobbleUntil - Time.time) / 0.25f;
            visual.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 50f) * 8f * k);
        }
    }
}
