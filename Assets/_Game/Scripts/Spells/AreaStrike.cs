using System;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Spells
{
    /// <summary>Delayed circular strike at a point (chain_spark, thunder_pulse, charged-bolt burst).
    /// The VFX plays on impact; damage is one DamageArea call with the cast's CastId.</summary>
    public sealed class AreaStrike : MonoBehaviour
    {
        DamageEvent hit;
        float radius;
        float at;
        Team team;
        string vfx;
        Color lightColor;
        string sfx;
        int maxTargets;
        bool armed;
        Action<int> onHit;

        public static AreaStrike Schedule(Vector2 p, float delay, float radius, DamageEvent e, Team team, string vfx, Color light, string sfx = null, int maxTargets = 64, Action<int> onHit = null)
        {
            var s = SpellPools.Instance.Strikes.Get();
            if (s == null)
                return null;
            s.transform.position = p;
            s.hit = e;
            s.radius = radius;
            s.at = Time.time + delay;
            s.team = team;
            s.vfx = vfx;
            s.lightColor = light;
            s.sfx = sfx;
            s.maxTargets = maxTargets;
            s.armed = true;
            s.onHit = onHit;
            return s;
        }

        void Update()
        {
            if (!armed || Time.time < at)
                return;
            armed = false;
            Vector2 p = transform.position;
            var v = VfxService.Instance;
            if (!string.IsNullOrEmpty(vfx))
                v.Play(vfx, vfx == "VFX_lightning" ? p + new Vector2(0, 0.85f) : p, 0, vfx == "VFX_lightning" ? 1f : radius / 0.75f);
            if (lightColor.a > 0)
                v.Light(p, lightColor, radius * 2.2f, 1.6f, 0.25f);
            v.Sparks(p, lightColor.a > 0 ? lightColor : Color.white, 10, 4f);
            if (!string.IsNullOrEmpty(sfx))
                Sfx.Play(sfx, 0.8f);
            int n = CombatWorld.Instance.DamageArea(p, radius, hit, team, maxTargets);
            onHit?.Invoke(n);
            SpellPools.Instance.Strikes.Release(this);
        }
    }
}
