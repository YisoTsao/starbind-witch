using System;

namespace Starbind.Combat
{
    /// <summary>Arcane Overdrive resonance (GDD §7.1). 0..100; R spends 100 for an 8 s overdrive.
    /// Kill resonance is capped per second; spell direct-hit resonance is capped per cast.</summary>
    public sealed class ResonanceMeter
    {
        public const float Max = 100f;
        public const float Duration = 8f;
        public const float KillCapPerSecond = 3f;
        public const float SpellHitCapPerCast = 2f;

        public float Value { get; private set; }
        public float GainMultiplier = 1f;
        public float ReactionGainMultiplier = 1f;
        public float ActiveUntil { get; private set; } = -1f;
        public event Action Started;
        public event Action Ended;

        float killWindowStart;
        float killGainInWindow;
        int lastCastId = -1;
        float castGain;
        bool wasActive;

        public bool IsActive(float now) => now < ActiveUntil;
        public bool CanActivate(float now) => Value >= Max && !IsActive(now);
        public float Normalized => Value / Max;

        public float Add(HitSource source, float now, int castId = 0, bool reaction = false, bool perfectDodge = false, bool kill = false)
        {
            if (IsActive(now))
                return 0;
            float gain = 0;
            if (reaction) gain = 8f * ReactionGainMultiplier;
            else if (perfectDodge) gain = 5f;
            else if (kill)
            {
                if (now - killWindowStart >= 1f)
                {
                    killWindowStart = now;
                    killGainInWindow = 0;
                }
                gain = Math.Min(1f, KillCapPerSecond - killGainInWindow);
                killGainInWindow += gain;
            }
            else if (source == HitSource.StaffFinisher) gain = 2f;
            else if (source == HitSource.ActiveSpell)
            {
                if (castId != lastCastId)
                {
                    lastCastId = castId;
                    castGain = 0;
                }
                gain = Math.Min(1f, SpellHitCapPerCast - castGain);
                castGain += gain;
            }
            gain = Math.Max(0, gain) * GainMultiplier;
            float before = Value;
            Value = Math.Min(Max, Value + gain);
            return Value - before;
        }

        public bool TryActivate(float now)
        {
            if (!CanActivate(now))
                return false;
            Value = 0;
            ActiveUntil = now + Duration;
            wasActive = true;
            Started?.Invoke();
            return true;
        }

        /// <summary>Call once per frame; raises Ended exactly once when the overdrive runs out.</summary>
        public void Tick(float now)
        {
            if (wasActive && !IsActive(now))
            {
                wasActive = false;
                Ended?.Invoke();
            }
        }

        public void ForceFill() => Value = Max;
    }
}
