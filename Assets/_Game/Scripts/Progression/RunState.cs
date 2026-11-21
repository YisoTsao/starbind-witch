using System;

namespace Starbind.Progression
{
    /// <summary>One run's progress (GDD §8.1): XP, level, kills and gathered materials. Discarded on
    /// death except for the materials RunManager banks into the profile.</summary>
    public sealed class RunState
    {
        public int Seed { get; }
        public int Level { get; private set; } = 1;
        public int Xp { get; private set; }
        public int Kills { get; set; }
        public int EliteKills { get; set; }
        public float Elapsed { get; set; }
        public int StarSand { get; set; }
        public int Spores { get; set; }
        public int MoonDew { get; set; }
        public int CoreShards { get; set; }
        public int Pages { get; set; }
        public int ReactionsTriggered { get; set; }
        public bool BossDefeated { get; set; }
        public int PendingLevelUps { get; set; }

        public event Action<int> LeveledUp;

        public RunState(int seed) => Seed = seed;

        public static int XpToNext(int level) => 5 + (level - 1) * 4;

        /// <summary>Adds XP; returns how many levels were gained.</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0)
                return 0;
            Xp += amount;
            int gained = 0;
            while (Xp >= XpToNext(Level))
            {
                Xp -= XpToNext(Level);
                Level++;
                gained++;
                PendingLevelUps++;
                LeveledUp?.Invoke(Level);
            }
            return gained;
        }

        public float XpFraction => Xp / (float)XpToNext(Level);
    }
}
