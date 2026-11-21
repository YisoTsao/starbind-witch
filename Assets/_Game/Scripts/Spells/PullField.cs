using System.Collections.Generic;
using Starbind.Combat;
using Starbind.Core;
using Starbind.VFX;
using UnityEngine;

namespace Starbind.Spells
{
    /// <summary>wind_pull (GDD §5.4): 2 s vortex that drags enemies to its centre and ticks Wind damage.
    /// Wind on a burning target produces the Fire Tornado reaction through the normal resolver.</summary>
    public sealed class PullField : MonoBehaviour
    {
        DamageEvent tick;
        float radius;
        float endAt;
        float nextTick;
        SpriteFlipbook visual;
        readonly List<Health> inside = new List<Health>(32);

        public static PullField Open(Vector2 p, float radius, float duration, DamageEvent e)
        {
            var f = SpellPools.Instance.Pulls.Get();
            if (f == null)
                return null;
            f.transform.position = p;
            f.tick = e;
            f.radius = radius;
            f.endAt = Time.time + duration;
            f.nextTick = Time.time;
            f.visual = VfxService.Instance.Play("VFX_vortex", p, 0, radius / 0.75f, new Color(1, 1, 1, 0.9f), StarbindLayers.SortGroundDecal, 5, true);
            Sfx.Play("sfx_wind", 0.8f);
            return f;
        }

        void Update()
        {
            float now = Time.time;
            Vector2 c = transform.position;
            var world = CombatWorld.Instance;
            world.Query(c, radius, Team.Player, inside);
            foreach (var h in inside)
            {
                var to = c - h.Center;
                if (to.sqrMagnitude > 0.05f && h.TryGetComponent(out IKnockbackReceiver kb))
                    kb.Pull(to.normalized * Mathf.Lerp(1.5f, 4.5f, to.magnitude / radius));
            }
            if (now >= nextTick)
            {
                nextTick = now + 0.5f;
                tick.CastId = CombatIds.NewCast();
                world.DamageArea(c, radius, tick, Team.Player);
            }
            if (now >= endAt)
            {
                VfxService.Instance.Release(visual);
                visual = null;
                SpellPools.Instance.Pulls.Release(this);
            }
        }
    }
}
