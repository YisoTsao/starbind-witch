using System;
using System.Collections.Generic;
using Starbind.Combat;

namespace Starbind.Spells
{
    /// <summary>Per-run mutable copy of a SpellDefinition. Upgrades change these numbers; the asset
    /// itself is never written (CLAUDE.md: runtime state must not write back into assets).</summary>
    public sealed class RuntimeSpell
    {
        public readonly SpellDefinition Def;
        public string Id => Def.id;
        public SpellForm Form => Def.form;
        public ElementType Element => Def.element;
        public SpellTrigger Trigger => Def.trigger;

        public float Damage;
        public float Cooldown;
        public float MpCost;
        public float Speed;
        public float Range;
        public float Radius;
        public float StatusDuration;
        public float Duration;
        public int Count;
        public int Pierce;
        public int Split;
        public bool Returns;
        /// <summary>Weaker recasts after the cast (Echo modifier: 1, U19 adds one).</summary>
        public int Echo;
        /// <summary>Times a missile jumps to the next enemy after a hit (Star element: 2, U20 adds one).</summary>
        public int Bounce;
        public float ReadyAt;
        public int CastCount;

        internal RuntimeSpell(SpellDefinition def)
        {
            Def = def;
            Damage = def.damage;
            Cooldown = def.cooldown;
            MpCost = def.mpCost;
            Speed = def.speed;
            Range = def.range;
            Radius = def.radius;
            StatusDuration = def.statusDuration;
            Duration = def.duration;
            Count = Math.Max(1, def.count);
            Pierce = def.pierce;
            Split = def.modifier == SpellModifier.Split ? 2 : 0;
            Echo = def.modifier == SpellModifier.Echo ? 1 : 0;
            Bounce = def.element == ElementType.Star && def.form == SpellForm.Missile ? 2 : 0;
        }
    }

    /// <summary>Builds RuntimeSpells from page combinations, only for whitelisted Form x Element x
    /// Modifier triples (GDD §5.3: 6 whitelisted combos + wind_pull special + magic missiles, then one
    /// combo per later-school spell unlocked by chapters 2-4).</summary>
    public static class SpellFactory
    {
        static readonly HashSet<(SpellForm, ElementType, SpellModifier)> whitelist = new HashSet<(SpellForm, ElementType, SpellModifier)>
        {
            (SpellForm.Missile, ElementType.Fire, SpellModifier.None),       // fire_bolt
            (SpellForm.Orbit, ElementType.Ice, SpellModifier.None),          // frost_orbit
            (SpellForm.Area, ElementType.Lightning, SpellModifier.None),     // chain_spark
            (SpellForm.Missile, ElementType.Fire, SpellModifier.Split),      // split_flame
            (SpellForm.Missile, ElementType.Ice, SpellModifier.Pierce),      // piercing_ice
            (SpellForm.Pulse, ElementType.Lightning, SpellModifier.None),    // thunder_pulse
            (SpellForm.Pull, ElementType.Wind, SpellModifier.None),          // wind_pull (special control spell)
            (SpellForm.Homing, ElementType.None, SpellModifier.None),        // homing_missile
            (SpellForm.Homing, ElementType.Fire, SpellModifier.None),        // star_fire charm
            (SpellForm.Beam, ElementType.Lightning, SpellModifier.None),     // prism_beam (ch.2)
            (SpellForm.Pulse, ElementType.Nature, SpellModifier.None),       // thorn_bloom (ch.2)
            (SpellForm.Area, ElementType.Moon, SpellModifier.None),          // shadow_hand (ch.3)
            (SpellForm.Missile, ElementType.Moon, SpellModifier.Echo),       // moon_echo (ch.3)
            (SpellForm.Missile, ElementType.Star, SpellModifier.None),       // star_shot (ch.4)
        };

        public static bool IsAllowed(SpellForm form, ElementType element, SpellModifier modifier) => whitelist.Contains((form, element, modifier));

        public static bool IsAllowed(SpellDefinition def) => def != null && IsAllowed(def.form, def.element, def.modifier);

        public static RuntimeSpell Create(SpellDefinition def)
        {
            if (!IsAllowed(def))
                throw new InvalidOperationException($"spell combination not whitelisted: {def?.id} ({def?.form}/{def?.element}/{def?.modifier})");
            return new RuntimeSpell(def);
        }
    }
}
