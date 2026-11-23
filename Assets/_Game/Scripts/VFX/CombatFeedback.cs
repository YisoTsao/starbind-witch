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
            VfxService.Instance.Bolt(a, b, new Color(0.48f, 0.65f, 1f), 0.13f, 1, 3f);
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
                    v.Play("VFX_steam", p, 0, full ? 1.8f : 1f);
                    if (full)
                    {
                        v.Glow(p, new Color(0.74f, 0.87f, 1f, 0.48f), 4.2f, 0.38f);
                        v.Play("VFX_shockwave", p, 0, 1.4f, new Color(0.85f, 0.94f, 1f, 0.66f), StarbindLayers.SortGroundDecal, 5);
                        v.Sparks(p, new Color(0.83f, 0.97f, 1f), 12, 4.2f, 0.42f, 0.085f);
                    }
                    if (full)
                        v.Light(p, new Color(0.82f, 0.9f, 1f), 2.6f, 0.48f, 0.26f);
                    if (full) CameraFollow2D.Main?.Shake(0.08f, 0.2f);
                    Sfx.Play("sfx_steam", 0.8f);
                    break;
                case ReactionType.Overload:
                    v.Play("VFX_overload", p, 0, full ? 1.6f : 1f);
                    if (full)
                    {
                        v.Play("VFX_blast", p, 0, 1.15f);
                        v.Glow(p, new Color(1f, 0.44f, 0.19f, 0.84f), 4.9f, 0.38f);
                        v.Bolt(p, p + new Vector2(-0.95f, 0.74f), new Color(0.81f, 0.45f, 1f), 0.18f, 1, 4f);
                        v.Bolt(p, p + new Vector2(1.1f, 0.35f), new Color(0.81f, 0.45f, 1f), 0.15f, 1, 3f);
                        v.Sparks(p, new Color(1f, 0.8f, 0.3f), 20, 6f, 0.52f, 0.1f);
                        v.ScreenFlash(new Color(1f, 0.85f, 0.6f), 0.17f, 0.16f);
                        CameraFollow2D.Main?.Shake(0.16f, 0.3f);
                    }
                    if (full)
                        v.Light(p, new Color(1f, 0.65f, 0.3f), 3f, 0.74f, 0.29f);
                    Sfx.Play("sfx_overload", 0.9f);
                    break;
                case ReactionType.ShatterStorm:
                    v.Play("VFX_shatter", p, 0, full ? 1.8f : 1f);
                    if (full)
                    {
                        v.Glow(p, new Color(0.4f, 0.78f, 1f, 0.68f), 4.6f, 0.36f);
                        for (int i = 0; i < 3; i++)
                        {
                            float a = i * Mathf.PI * 2f / 3f + Random.value * 0.5f;
                            v.Bolt(p, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.1f, 1.75f), new Color(0.73f, 0.53f, 1f), 0.16f, 1, 3f);
                        }
                        v.ScreenFlash(new Color(0.75f, 0.92f, 1f), 0.1f, 0.14f);
                        CameraFollow2D.Main?.Shake(0.1f, 0.22f);
                    }
                    if (full)
                        v.Light(p, new Color(0.6f, 0.9f, 1f), 2.8f, 0.7f, 0.29f);
                    Sfx.Play("sfx_shatter", 0.9f);
                    break;
                case ReactionType.FireTornado:
                    if (full)
                    {
                        v.Play("VFX_blast", p, 0, 1.1f);
                        v.Play("VFX_shockwave", p, 0, 1.8f, new Color(1f, 0.6f, 0.25f));
                        v.Play("VFX_magic_circle", p, 0, 1.8f, new Color(1f, 0.55f, 0.2f, 0.9f), StarbindLayers.SortGroundDecal, 6);
                    }
                    if (full)
                        v.Light(p, new Color(1f, 0.55f, 0.2f), 2.65f, 0.62f, 0.33f);
                    break;
            }
            DamageNumbers.Instance.Show(p + new Vector2(0, 0.9f), 0, Color.white, -1, true);
        }
    }
}
