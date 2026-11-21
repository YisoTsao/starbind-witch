namespace Starbind.Combat
{
    /// <summary>Three-step staff combo timing (GDD §4.3): sweep 10 -> back strike 12 -> shock 18.
    /// A press during an attack is buffered; the next step must start within ComboWindow after the
    /// previous step ends, or the chain restarts. The finisher has a longer recovery.</summary>
    public sealed class StaffCombo
    {
        public static readonly float[] Damage = { 10f, 12f, 18f };
        public static readonly float[] StepDuration = { 0.22f, 0.24f, 0.32f };
        public const float ComboWindow = 0.45f;
        public const float FinisherRecovery = 0.25f;

        int nextStep;
        float busyUntil = -1f;
        float lastEnd = -10f;
        bool buffered;

        public int LastStep { get; private set; } = -1;
        public bool IsBusy(float now) => now < busyUntil;

        /// <summary>Returns the step index (0..2) started now, or -1 when the press was buffered or ignored.</summary>
        public int Press(float now)
        {
            if (IsBusy(now))
            {
                buffered = true;
                return -1;
            }
            return Start(now);
        }

        /// <summary>Call every frame; starts a buffered step as soon as the current one ends.</summary>
        public int Tick(float now)
        {
            if (buffered && !IsBusy(now))
            {
                buffered = false;
                return Start(now);
            }
            return -1;
        }

        int Start(float now)
        {
            if (now - lastEnd > ComboWindow || nextStep > 2)
                nextStep = 0;
            int step = nextStep;
            float dur = StepDuration[step] + (step == 2 ? FinisherRecovery : 0f);
            busyUntil = now + dur;
            lastEnd = now + StepDuration[step];
            nextStep = step == 2 ? 0 : step + 1;
            if (step == 2)
                lastEnd = -10f; // finisher always restarts the chain
            LastStep = step;
            return step;
        }

        public void Reset()
        {
            nextStep = 0;
            busyUntil = -1f;
            lastEnd = -10f;
            buffered = false;
            LastStep = -1;
        }
    }

    /// <summary>Hold-to-charge magic bolt (GDD §4.2): tap = melee; hold past TapThreshold starts
    /// charging; damage scales 18..34 over FullCharge seconds in three levels.</summary>
    public static class ChargeRules
    {
        public const float TapThreshold = 0.22f;
        public const float FullCharge = 1.0f;
        public const float MinDamage = 18f;
        public const float MaxDamage = 34f;

        public static bool IsCharge(float heldSeconds) => heldSeconds >= TapThreshold;

        /// <summary>0 = not charged, 1..3 = charge level.</summary>
        public static int Level(float heldSeconds)
        {
            if (heldSeconds < TapThreshold) return 0;
            float t = (heldSeconds - TapThreshold) / (FullCharge - TapThreshold);
            return t >= 1f ? 3 : t >= 0.5f ? 2 : 1;
        }

        public static float Damage(float heldSeconds)
        {
            int lvl = Level(heldSeconds);
            if (lvl == 0) return 0;
            return MinDamage + (MaxDamage - MinDamage) * (lvl - 1) / 2f;
        }
    }

    /// <summary>MP regained by staff hits (GDD §4.3): 2 MP for the first hit of each attack event on a
    /// target, capped per second so big groups cannot refill MP infinitely.</summary>
    public sealed class ManaOnHitLimiter
    {
        public const float PerHit = 2f;
        public const float CapPerSecond = 8f;
        float windowStart = -10f;
        float given;

        public float Grant(float now, float multiplier = 1f)
        {
            if (now - windowStart >= 1f)
            {
                windowStart = now;
                given = 0;
            }
            float g = System.Math.Min(PerHit * multiplier, CapPerSecond * multiplier - given);
            if (g <= 0)
                return 0;
            given += g;
            return g;
        }
    }
}
