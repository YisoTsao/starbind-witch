using System;
using System.Collections.Generic;

namespace Starbind.Items
{
    /// <summary>Stackable item counts keyed by ItemId (GDD §9.4). Counts never go negative nor above
    /// the stack cap; UI reads, never writes. Raises Changed after every successful mutation.</summary>
    public sealed class Inventory
    {
        public const string HpPotion = "potion_hp";
        public const string MpPotion = "potion_mp";
        public const string StarSand = "star_sand";
        public const string MoonDew = "moon_dew";
        public const string Spore = "spore";
        public const string CoreShard = "core_shard";
        public const string Page = "grimoire_page";

        readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        readonly Dictionary<string, int> caps = new Dictionary<string, int>
        {
            { HpPotion, 5 }, { MpPotion, 5 }, { StarSand, 999 }, { MoonDew, 999 }, { Spore, 999 }, { CoreShard, 99 }, { Page, 99 },
        };

        public event Action<string, int> Changed;

        public int Count(string id) => counts.TryGetValue(id, out var n) ? n : 0;
        public int Cap(string id) => caps.TryGetValue(id, out var c) ? c : 99;

        /// <summary>Adds up to the cap; returns how many were actually added.</summary>
        public int Add(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0)
                return 0;
            int have = Count(id);
            int added = Math.Min(amount, Cap(id) - have);
            if (added <= 0)
                return 0;
            counts[id] = have + added;
            Changed?.Invoke(id, counts[id]);
            return added;
        }

        /// <summary>Removes exactly <paramref name="amount"/> or nothing.</summary>
        public bool TryRemove(string id, int amount)
        {
            if (amount <= 0 || Count(id) < amount)
                return false;
            counts[id] = Count(id) - amount;
            Changed?.Invoke(id, counts[id]);
            return true;
        }

        public IEnumerable<KeyValuePair<string, int>> All => counts;
    }
}
