using UnityEngine;

namespace Starbind.Combat
{
    /// <summary>Demo elements (GDD §5.2) plus Wind for the wind_pull special (§5.3 note) and the later
    /// schools unlocked by chapters 2-4: Nature (poison), Moon (curse) and Star (no mark; rebounds).
    /// Values are serialized: append only.</summary>
    public enum ElementType { None, Fire, Ice, Lightning, Wind, Nature, Moon, Star }

    /// <summary>Append only (serialized). Wildfire = Fire + Nature (the one later-school reaction).</summary>
    public enum ReactionType { None, SteamBurst, Overload, ShatterStorm, FireTornado, Wildfire }

    /// <summary>Which kind of attack produced a hit; used for resonance and MP-on-hit rules.</summary>
    public enum HitSource { Staff, StaffFinisher, ChargedBolt, ActiveSpell, AutoSpell, Reaction, Enemy, Environment }

    /// <summary>GDD §13.5. One cast = one CastId; every hit of that cast shares it (dedupe key).</summary>
    public struct CastContext
    {
        public int CastId;
        public string SourceSpellId;
        public Vector2 Origin;
        public Vector2 Direction;
        public float DamageMultiplier;
        public bool CannotTriggerReaction;
        public int ProcDepth;
        public HitSource Source;
    }

    /// <summary>GDD §13.5 damage event plus status payload.</summary>
    public struct DamageEvent
    {
        public int CastId;
        public int SourceEntityId;
        public int TargetEntityId;
        public float Amount;
        public ElementType Element;
        public bool CannotTriggerReaction;
        public HitSource Source;
        public float StatusDuration;
        public float Knockback;
        public Vector2 Direction;
        public int ProcDepth;
        public bool IsCritical;
    }

    /// <summary>Result of applying one DamageEvent.</summary>
    public struct DamageResult
    {
        public bool Applied;
        public float Dealt;
        public bool Killed;
        public ReactionType Reaction;
    }

    public static class CombatIds
    {
        static int nextCast = 1;
        static int nextEntity = 1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            nextCast = 1;
            nextEntity = 1;
        }

        public static int NewCast() => nextCast++;
        public static int NewEntity() => nextEntity++;
    }
}
