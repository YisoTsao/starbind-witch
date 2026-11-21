using UnityEngine;

namespace Starbind.Progression
{
    public enum UpgradeKind { Active, Element, Auto, Passive, Cursed }

    /// <summary>Level-up card (GDD §8.2). The effect is keyed by id and applied by UpgradeDirector.</summary>
    [CreateAssetMenu(menuName = "Starbind/Upgrade Definition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public UpgradeKind kind;
        public float weight = 10f;
        public int maxStacks = 1;
        [Tooltip("Card only offered when the build has this spell (any slot). Empty = no requirement.")]
        public string requiresSpell;
        [Tooltip("Card only offered when the build lacks this spell. Empty = no requirement.")]
        public string excludesSpell;
        public string icon;
    }
}
