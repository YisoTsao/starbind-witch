using Starbind.Combat;
using UnityEngine;

namespace Starbind.Spells
{
    public enum SpellForm { Missile, Area, Orbit, Pull, Homing, Pulse }
    public enum SpellModifier { None, Split, Pierce }
    public enum SpellTrigger { Active, Auto }

    /// <summary>Static spell data (GDD §5.3, §5.4). Never modified at runtime: RuntimeSpell copies it.</summary>
    [CreateAssetMenu(menuName = "Starbind/Spell Definition")]
    public sealed class SpellDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public SpellForm form;
        public ElementType element;
        public SpellModifier modifier;
        public SpellTrigger trigger;
        public float damage = 10f;
        public float cooldown = 1f;
        public float mpCost;
        public float speed = 9f;
        public float range = 8f;
        public float radius = 0.4f;
        public float statusDuration = 3f;
        public int count = 1;
        public int pierce;
        public float duration;
        public string icon;
        public string vfx;
    }
}
