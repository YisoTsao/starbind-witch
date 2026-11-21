using Starbind.CameraRig;
using Starbind.Combat;
using Starbind.Core;
using UnityEngine;

namespace Starbind.VFX
{
    /// <summary>Turns combat events into feedback (GDD §4.4, §12.1): damage numbers, hit sparks, status
    /// puffs, reaction effects (A-tier), arcs, light flashes, shake and sound. Listens to the scene's
    /// CombatEventBus only; never changes combat state.</summary>
    public sealed class CombatFeedback : MonoBehaviour
    {
        CombatWorld world;
        int reactionsThisSecond;
        float reactionWindow;

        void OnEnable()
        {
            world = CombatWorld.Instance;
            world.Bus.Hit += OnHit;
            world.Bus.ReactionTriggered += OnReaction;
            world.Bus.ElementApplied += OnElement;
            world.Bus.Arc += OnArc;
        }

        void OnDisable()
        {
            if (world == null)
                return;
            world.Bus.Hit -= OnHit;
            world.Bus.ReactionTriggered -= OnReaction;
            world.Bus.ElementApplied -= OnElement;
            world.Bus.Arc -= OnArc;
            world = null;
        }

        void OnHit(Health target, DamageEvent e, DamageResult r)
        {
            if (!r.Applied)
                return;
            var p = target.Center;
            bool big = e.Source == HitSource.Reaction || e.Source == HitSource.StaffFinisher || e.Amount >= 30f;
            if (target.DamageTakenMultiplier > 0 || target.Team == Team.Player)
                DamageNumbers.Instance.Show(p + new Vector2(0, target.Radius), e.Amount, DamageNumbers.ColorFor(e), target.EntityId, big);
            if (e.Source == HitSource.Environment)
                return;
            var v = VfxService.Instance;
            if (target.Team != Team.Player && e.Source != HitSource.Reaction)
            {
                v.Play("VFX_hit", p + Random.insideUnitCircle * 0.15f, 0, 1f, DamageNumbers.ColorFor(e));
                Sfx.Play("sfx_hit", 0.35f, 0.15f);
            }
        }

        void OnElement(ElementType el, Health h)
        {
            var v = VfxService.Instance;
            switch (el)
            {
                case ElementType.Ice: v.Play("VFX_chill", h.Center + new Vector2(0, 0.2f), 0, 1.2f); break;
                case ElementType.Lightning: v.Play("VFX_shock", h.Center + new Vector2(0, 0.2f), 0, 1.2f); break;
            }
        }

        void OnArc(Vector2 a, Vector2 b)
        {
            VfxService.Instance.Arc(a, b, new Color(0.85f, 0.95f, 1f));
            VfxService.Instance.Arc(a, b, new Color(1f, 0.95f, 0.5f), 0.08f);
        }

        void OnReaction(ReactionType r, Vector2 p, Health target)
        {
            var v = VfxService.Instance;
            if (Time.time - reactionWindow > 1f)
            {
                reactionWindow = Time.time;
                reactionsThisSecond = 0;
            }
            // A-tier effects are merged when many reactions fire together (GDD §12.1)
            bool full = ++reactionsThisSecond <= 6;
            switch (r)
            {
                case ReactionType.SteamBurst:
                    v.Play("VFX_steam", p, 0, full ? 1.4f : 1f);
                    if (full) v.Play("VFX_ice_burst", p, 0, 1.6f, new Color(1f, 0.85f, 0.7f));
                    v.Light(p, new Color(0.9f, 0.95f, 1f), 3.5f, 1.2f, 0.35f);
                    Sfx.Play("sfx_steam", 0.8f);
                    break;
                case ReactionType.Overload:
                    v.Play("VFX_overload", p, 0, full ? 1.4f : 1f);
                    if (full) v.Sparks(p, new Color(1f, 0.8f, 0.3f), 24, 6f, 0.5f);
                    v.Light(p, new Color(1f, 0.7f, 0.3f), 4.5f, 2f, 0.35f);
                    if (full) CameraFollow2D.Main?.Shake(0.1f, 0.2f);
                    Sfx.Play("sfx_overload", 0.9f);
                    break;
                case ReactionType.ShatterStorm:
                    v.Play("VFX_shatter", p, 0, full ? 1.4f : 1f);
                    v.Light(p, new Color(0.6f, 0.9f, 1f), 4f, 1.8f, 0.3f);
                    Sfx.Play("sfx_shatter", 0.9f);
                    break;
                case ReactionType.FireTornado:
                    v.Light(p, new Color(1f, 0.55f, 0.2f), 4f, 1.8f, 0.5f);
                    break;
            }
            DamageNumbers.Instance.Show(p + new Vector2(0, 0.9f), 0, Color.white, -1, true);
        }
    }
}
