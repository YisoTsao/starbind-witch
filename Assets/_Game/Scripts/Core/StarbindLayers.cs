namespace Starbind.Core
{
    /// <summary>Sorting layer and physics layer names (TECH_ARCHITECTURE.md §4, §5).</summary>
    public static class StarbindLayers
    {
        public const string SortGround = "Ground";
        public const string SortGroundDecal = "GroundDecal";
        public const string SortWorld = "World";
        public const string SortVfx = "VFX";
        public const string SortTelegraph = "Telegraph";
        public const string SortForeground = "Foreground";
        public const string SortWorldUI = "WorldUI";

        public static readonly string[] SortingOrder =
            { SortGround, SortGroundDecal, SortWorld, SortVfx, SortTelegraph, SortForeground, SortWorldUI };

        public const string Player = "Player";
        public const string Enemy = "Enemy";
        public const string WorldSolid = "WorldSolid";
        public const string Water = "Water"; // Unity built-in layer 4
        public const string Interactable = "Interactable";
        public const string Trigger = "Trigger";
        public const string Projectile = "Projectile";
    }
}
