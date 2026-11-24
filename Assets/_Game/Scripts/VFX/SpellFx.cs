using Starbind.CameraRig;
using Starbind.Combat;
using Starbind.Core;
using UnityEngine;

namespace Starbind.VFX
{
    /// <summary>"Big spell" effect recipes layered from VfxService pieces: magic circles and muzzle flashes on
    /// cast, glowing trails, blasts / frost novas / sky lightning on impact, with 2D lights, shockwaves,
    /// shake and screen flashes. Display only: never touches combat state.
    /// Budget: at most <see cref="FullImpactsPerWindow"/> full impacts per 0.1 s; the rest play a light
    /// version so 60-enemy waves stay readable and bounded.</summary>
    public static class SpellFx
    {
        public const int FullImpactsPerWindow = 5;
        /// <summary>Scorch decals stay small: their dithered fade turns into a visible checkerboard when scaled up.</summary>
        public const float MaxScorchScale = 1.1f;

        static float window;
        static int inWindow;

        public static int CastEffects { get; private set; }
        public static int ImpactEffects { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            window = 0;
            inWindow = 0;
            CastEffects = 0;
            ImpactEffects = 0;
        }

        public static Color ElementColor(ElementType el) => el switch
        {
            ElementType.Fire => new Color(1f, 0.47f, 0.18f),
            ElementType.Ice => new Color(0.35f, 0.78f, 1f),
            ElementType.Lightning => new Color(0.73f, 0.48f, 1f),
            ElementType.Wind => new Color(0.45f, 1f, 0.78f),
            ElementType.Nature => new Color(0.55f, 0.9f, 0.35f),
            ElementType.Moon => new Color(0.55f, 0.42f, 1f),
            ElementType.Star => new Color(1f, 0.88f, 0.45f),
            _ => new Color(0.76f, 0.47f, 1f),
        };

        static bool Full()
        {
            if (Time.time - window > 0.1f)
            {
                window = Time.time;
                inWindow = 0;
            }
            return ++inWindow <= FullImpactsPerWindow;
        }

        // ---------------------------------------------------------------- cast
        /// <summary>Spell leaves the staff: ground magic circle under the caster, muzzle star, burst of sparks, light.</summary>
        public static void Cast(ElementType el, Vector2 feet, Vector2 muzzle, Vector2 dir, float power = 1f)
        {
            var v = VfxService.Instance;
            var col = ElementColor(el);
            CastEffects++;
            v.Play("VFX_magic_circle", feet, 0, 0.9f + 0.3f * power, new Color(col.r, col.g, col.b, 0.9f), StarbindLayers.SortGroundDecal, 6);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            v.Play("VFX_flash", muzzle, angle, 0.32f + 0.18f * power, Color.Lerp(col, Color.white, 0.65f));
            v.Glow(muzzle, new Color(col.r, col.g, col.b, 0.55f), 2.1f * power, 0.24f);
            v.Light(muzzle, col, 1.7f * power, 0.65f, 0.18f);
            v.Embers(muzzle, Color.Lerp(col, Color.white, 0.5f), dir * 4f, 5, 0.24f, 0.075f);
        }

        // ---------------------------------------------------------------- trail
        /// <summary>Per-frame trail sparkle for a projectile moving along <paramref name="dir"/>.</summary>
        public static void Trail(ElementType el, Vector2 p, Vector2 dir, bool starFire = false)
        {
            var col = starFire ? new Color(1f, 0.81f, 0.35f) : ElementColor(el);
            var drift = el == ElementType.Fire && !starFire ? Vector2.up * 1.6f - dir * 1.8f : -dir * 1.8f;
            VfxService.Instance.Embers(p, Color.Lerp(col, Color.white, 0.35f), drift, 1, 0.27f, 0.08f);
        }

        // ---------------------------------------------------------------- impacts
        /// <summary>Projectile / strike impact. <paramref name="scale"/> ~ 1 for a normal spell, 2+ for reactions and finishers.</summary>
        public static void Impact(ElementType el, Vector2 p, float scale = 1f, bool heavy = false, bool starFire = false,
            float pulseRadius = 0f)
        {
            var v = VfxService.Instance;
            var col = ElementColor(el);
            ImpactEffects++;
            bool full = Full();
            scale = Mathf.Min(scale, 2.4f);
            if (starFire)
                col = new Color(1f, 0.82f, 0.44f);
            var core = Color.Lerp(col, Color.white, 0.72f);
            v.Play("VFX_flash", p, 0, 0.46f * scale, core);
            if (!full)
            {
                v.Sparks(p, core, 4, 2.6f);
                return;
            }
            v.Glow(p, new Color(col.r, col.g, col.b, heavy ? 0.64f : 0.52f), 2.6f * scale, 0.36f);
            if (el != ElementType.Lightning || pulseRadius > 0f)
                v.Light(p, col, Mathf.Min(2.2f * scale, 3f), heavy ? 0.85f : 0.55f, 0.25f);
            if (pulseRadius > 0f)
            {
                v.Play("VFX_spell_pulse", p, 0, pulseRadius * 0.84f);
                v.Play("VFX_shockwave", p, 0, pulseRadius, new Color(1f, 0.82f, 0.42f), StarbindLayers.SortGroundDecal, 5);
                v.Sparks(p, core, 12, 4f, 0.34f, 0.08f);
                return;
            }
            if (starFire)
            {
                v.Play("VFX_flash", p, 45f, 0.72f * scale, new Color(1f, 0.88f, 0.56f));
                v.Sparks(p, core, 9, 3.2f, 0.42f, 0.09f);
                return;
            }
            switch (el)
            {
                case ElementType.Fire:
                    v.Play("VFX_blast", p, 0, 0.75f * scale);
                    v.Play("VFX_scorch", p + new Vector2(0, -0.15f), 0, Mathf.Min(MaxScorchScale, 0.8f * scale), null, StarbindLayers.SortGroundDecal, 3);
                    v.Sparks(p, new Color(1f, 0.8f, 0.43f), 11, 4.5f * scale, 0.48f, 0.09f);
                    v.Embers(p, new Color(1f, 0.38f, 0.19f), Vector2.up * 2.4f, 6, 0.62f, 0.09f);
                    break;
                case ElementType.Ice:
                    v.Play("VFX_frost_nova", p, 0, 0.6f * scale);
                    v.Sparks(p, new Color(0.85f, 0.97f, 1f), 16, 4.5f * scale, 0.5f, 0.1f);
                    break;
                case ElementType.Lightning:
                    SkyStrike(p, scale);
                    break;
                case ElementType.Wind:
                    v.Play("VFX_wind_blades", p, 0, 0.8f * scale);
                    v.Sparks(p, col, 14, 5f * scale, 0.45f, 0.1f);
                    break;
                case ElementType.Nature:
                    v.Play("VFX_thorn", p, 0, 0.8f * scale);
                    v.Sparks(p, col, 10, 3.5f * scale, 0.45f, 0.09f);
                    break;
                case ElementType.Moon:
                    v.Play("VFX_shadow_hand", p, 0, 0.7f * scale, col);
                    v.Sparks(p, col, 10, 3.5f * scale, 0.45f, 0.09f);
                    break;
                case ElementType.Star:
                    v.Play("VFX_flash", p, 45f, 0.8f * scale, new Color(1f, 0.92f, 0.6f));
                    v.Sparks(p, col, 12, 4.5f * scale, 0.4f, 0.09f);
                    break;
                default:
                    v.Play("VFX_flash", p, 45f, 0.85f * scale, new Color(0.88f, 0.65f, 1f));
                    v.Sparks(p, core, 12, 4.5f * scale, 0.38f, 0.085f);
                    break;
            }
            if (heavy)
            {
                v.Play("VFX_shockwave", p, 0, 1.15f * scale, core, StarbindLayers.SortGroundDecal, 6);
                v.ScreenFlash(core, 0.13f, 0.14f);
                CameraFollow2D.Main?.Shake(0.09f * scale, 0.22f);
            }
            else
                CameraFollow2D.Main?.Shake(0.035f * scale, 0.1f);
        }

        /// <summary>Lightning from the sky with a branching sprite and ground crackle.</summary>
        static void SkyStrike(Vector2 p, float scale, bool sceneLight = true)
        {
            var v = VfxService.Instance;
            var col = ElementColor(ElementType.Lightning);
            v.Play("VFX_sky_bolt", p, 0, Mathf.Clamp(scale, 0.8f, 1.6f));
            v.Glow(p + Vector2.up * 0.34f, new Color(col.r, col.g, col.b, 0.85f), 3.8f * scale, 0.31f);
            v.Play("VFX_flash", p, 0, 0.72f * scale, new Color(1f, 0.96f, 0.78f));
            if (sceneLight)
                v.Light(p, col, Mathf.Min(2.1f * scale, 3f), 0.66f, 0.22f);
            for (int i = 0; i < 4; i++)
            {
                var to = p + Random.insideUnitCircle.normalized * Random.Range(0.8f, 1.6f) * scale;
                v.Arc(p, to, Color.Lerp(col, Color.white, 0.52f), 0.09f, 1.15f, 0.12f);
            }
            v.Sparks(p, new Color(1f, 0.88f, 0.53f), 11, 5f * scale, 0.35f, 0.09f);
            v.Play("VFX_scorch", p + new Vector2(0, -0.1f), 0, Mathf.Min(MaxScorchScale, 0.7f * scale), new Color(0.8f, 0.8f, 1f), StarbindLayers.SortGroundDecal, 3);
            CameraFollow2D.Main?.Shake(0.06f * scale, 0.15f);
        }

        /// <summary>Overdrive (R): ring of sky strikes, double shockwave, magic circle, white-out.</summary>
        public static void Overdrive(Vector2 feet, Vector2 center)
        {
            var v = VfxService.Instance;
            var violet = new Color(0.85f, 0.6f, 1f);
            v.Play("VFX_magic_circle", feet, 0, 2.3f, violet, StarbindLayers.SortGroundDecal, 6);
            v.Play("VFX_shockwave", feet, 0, 2.2f, new Color(violet.r, violet.g, violet.b, 0.6f), StarbindLayers.SortGroundDecal, 5);
            v.Play("VFX_shockwave", feet, 0, 3.1f, new Color(1f, 0.9f, 0.6f, 0.54f), StarbindLayers.SortGroundDecal, 6);
            v.Play("VFX_flash", center, 0, 1.6f, new Color(1f, 0.94f, 0.78f));
            v.Glow(center, new Color(violet.r, violet.g, violet.b, 0.78f), 5f, 0.46f);
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI / 2f + Mathf.PI / 4f;
                SkyStrike(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.8f) * 2.2f, 1f, false);
            }
            v.ScreenFlash(new Color(0.95f, 0.85f, 1f), 0.28f, 0.27f);
        }
    }
}
