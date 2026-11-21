using System;
using System.Collections.Generic;

namespace Starbind.Save
{
    /// <summary>Permanent progress (GDD §8.4, §14.1). Only ids and numbers: no Unity object references.</summary>
    [Serializable]
    public sealed class ProfileData
    {
        public const int CurrentVersion = 1;

        public int saveVersion = CurrentVersion;
        public int researchPoints;
        public int starSand;
        public int moonDew;
        public int spores;
        public int coreShards;
        public int pages;
        public List<string> unlockedSpells = new List<string> { "fire_bolt", "wind_pull", "piercing_ice", "homing_missile" };
        public string activeQ = "fire_bolt";
        public string activeE = "wind_pull";
        public List<string> autoSpells = new List<string> { "homing_missile" };
        public string equippedStaff = "sprout_staff";
        public string equippedCharm = "ember_charm";
        public int researchHp;
        public int researchMp;
        public int researchPotion;
        public List<string> completedQuests = new List<string>();
        public int runs;
        public int victories;
        public int deaths;
        public float bestClearTime;
        public float musicVolume = 0.8f;
        public float sfxVolume = 0.9f;

        public bool IsUnlocked(string spellId) => unlockedSpells.Contains(spellId);

        public void Unlock(string spellId)
        {
            if (!string.IsNullOrEmpty(spellId) && !unlockedSpells.Contains(spellId))
                unlockedSpells.Add(spellId);
        }

        public bool HasQuest(string id) => completedQuests.Contains(id);

        public void CompleteQuest(string id)
        {
            if (!completedQuests.Contains(id))
                completedQuests.Add(id);
        }
    }
}
