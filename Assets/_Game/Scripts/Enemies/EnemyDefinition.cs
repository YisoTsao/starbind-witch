using UnityEngine;

namespace Starbind.Enemies
{
    public enum EnemyBehaviour { Chaser, Ranged, Charger, Flanker, Elite, Boss, GolemBoss, ShadowBoss, GuardianBoss, GrimoireBoss, Bomber }

    /// <summary>Static enemy data (GDD §10.1). Runtime state lives on Enemy, never here.</summary>
    [CreateAssetMenu(menuName = "Starbind/Enemy Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public EnemyBehaviour behaviour;
        public float hp = 45f;
        public float damage = 10f;
        public float speed = 2f;
        public float attackRange = 0.7f;
        public float attackCooldown = 1.2f;
        public float projectileSpeed = 4.5f;
        public float preferredDistance;
        public float radius = 0.3f;
        public float knockbackResist;
        public int xp = 1;
        public string sprite;
        public float spriteFps = 6f;
        public bool isElite;
        public bool isBoss;
        public float lootChance = 0.1f;
        /// <summary>Inventory material id dropped on a loot roll ("" = star sand).</summary>
        public string lootMaterial = "";
        /// <summary>Sprite of this enemy's shots ("" = spore).</summary>
        public string projectileSprite = "";
        /// <summary>Minion id elites and bosses summon ("" = moss slime).</summary>
        public string summonId = "";
        /// <summary>Second minion id for bosses that alternate summons ("" = same as summonId).</summary>
        public string summonId2 = "";
        /// <summary>Element of this enemy's shots (marks the player only visually; enemies never react).</summary>
        public Starbind.Combat.ElementType element;
    }
}
