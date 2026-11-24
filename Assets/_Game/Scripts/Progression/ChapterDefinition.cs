using System;
using System.Collections.Generic;
using UnityEngine;

namespace Starbind.Progression
{
    /// <summary>Weighted enemy mix for one zone id of a chapter map (GDD §10.2).</summary>
    [Serializable]
    public sealed class ZoneMix
    {
        public string zone;
        public string[] enemies;
        public float[] weights;
    }

    /// <summary>Static data for one chapter run (STORY_WORLD_QUESTS §2.3, CHAPTER_PLAN): the scene, its boss
    /// and elite, zone enemy mixes, unlock and victory flags and first-clear rewards. RunManager and
    /// SpawnDirector read it; nothing here is written at runtime.</summary>
    [CreateAssetMenu(menuName = "Starbind/Chapter Definition")]
    public sealed class ChapterDefinition : ScriptableObject
    {
        public string id;
        public int order;
        public string scene;
        public string title;
        public string bossId;
        public string bossName;
        public string arenaName;
        public string eliteId;
        /// <summary>Quest flag set on victory; also the unlock flag of the next chapter.</summary>
        public string victoryFlag;
        /// <summary>Flag needed before the star plate offers this chapter ("" = always open).</summary>
        public string requiresFlag;
        public string entrySpawn = "spawn_from_village";
        /// <summary>Village spawn used after a victory (the forest returns by the south road).</summary>
        public string returnSpawn = "spawn_from_starplate";
        public string introNotice;
        public string pushObjective;
        public string bossObjective;
        /// <summary>Spells unlocked the first time the chapter is cleared.</summary>
        public string[] rewardSpells = Array.Empty<string>();
        public string rewardNote;
        /// <summary>Health multiplier for every non-boss enemy spawned in this chapter.</summary>
        public float enemyHpScale = 1f;
        /// <summary>The finale: victory opens the ending choice instead of the plain results screen.</summary>
        public bool finale;
        public List<ZoneMix> zones = new List<ZoneMix>();

        public ZoneMix Mix(string zone)
        {
            foreach (var z in zones)
                if (z.zone == zone)
                    return z;
            return zones.Count > 0 ? zones[0] : null;
        }
    }
}
