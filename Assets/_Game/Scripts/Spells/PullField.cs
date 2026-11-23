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
        SpriteFlipbook visual, funnel, bloom;
        UnityEngine.Rendering.Universal.Light2D glow;
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
            var v = VfxService.Instance;
            f.visual = v.Play("VFX_vortex", p, 0, radius * (32f / 42f), null, StarbindLayers.SortGroundDecal, 5, true);
            f.funnel = v.Play("VFX_wind_funnel", p, 0, radius * 0.68f, null, StarbindLayers.SortVfx, 2, true);
            f.bloom = v.Glow(p + Vector2.up * 0.45f, new Color(0.34f, 1f, 0.71f, 0.35f), radius * 2.5f, target: f.transform);
            f.glow = v.AttachLight(f.transform, SpellFx.ElementColor(ElementType.Wind), radius * 1.25f, 0.46f);
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
                VfxService.Instance.Play("VFX_shockwave", c, 0, radius, new Color(0.7f, 1f, 0.85f, 0.46f), StarbindLayers.SortGroundDecal, 6);
            }
            if (now >= endAt)
            {
                var v = VfxService.Instance;
                v.Play("VFX_wind_blades", c, 0, radius, new Color(0.82f, 1f, 0.95f, 0.62f));
                v.Glow(c, new Color(0.55f, 1f, 0.8f, 0.44f), radius * 2.2f);
                v.Sparks(c, new Color(0.7f, 1f, 0.85f), 12, 4.6f, 0.42f, 0.085f);
                SpellPools.Instance.Pulls.Release(this);
            }
        }

        void OnDisable()
        {
            if (visual == null && funnel == null && bloom == null && glow == null)
                return;
            var v = VfxService.Instance;
            v.Release(visual);
            v.Release(funnel);
            v.Release(bloom);
            v.DetachLight(glow);
            visual = funnel = bloom = null;
            glow = null;
        }
    }
}
