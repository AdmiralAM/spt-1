using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace SPTItemIntelligence
{
    public sealed class ItemRelevanceState
    {
        internal static readonly ItemRelevanceState Empty = new ItemRelevanceState(0, 0);

        public ItemRelevanceState(int craftCount, int barterCount)
        {
            CraftCount = Math.Max(0, craftCount);
            BarterCount = Math.Max(0, barterCount);
        }

        public int CraftCount { get; }
        public int BarterCount { get; }
        public bool HasData => CraftCount > 0 || BarterCount > 0;
    }

    public static class ItemRelevanceRegistry
    {
        static IReadOnlyDictionary<string, ItemRelevanceState> current = EmptyMap();

        public static ItemRelevanceState Get(string templateId)
        {
            string normalized = RequirementContribution.NormalizeId(templateId);
            if (normalized.Length == 0) return ItemRelevanceState.Empty;
            IReadOnlyDictionary<string, ItemRelevanceState> snapshot = Volatile.Read(ref current);
            ItemRelevanceState state;
            return snapshot.TryGetValue(normalized, out state) ? state : ItemRelevanceState.Empty;
        }

        public static void Replace(IDictionary<string, ItemRelevanceState> entries)
        {
            Dictionary<string, ItemRelevanceState> next = new Dictionary<string, ItemRelevanceState>(StringComparer.Ordinal);
            if (entries != null)
            {
                foreach (KeyValuePair<string, ItemRelevanceState> pair in entries)
                {
                    string normalized = RequirementContribution.NormalizeId(pair.Key);
                    if (normalized.Length == 0 || pair.Value == null || !pair.Value.HasData) continue;
                    next[normalized] = pair.Value;
                }
            }
            Volatile.Write(ref current, new ReadOnlyDictionary<string, ItemRelevanceState>(next));
        }

        static IReadOnlyDictionary<string, ItemRelevanceState> EmptyMap()
        {
            return new ReadOnlyDictionary<string, ItemRelevanceState>(new Dictionary<string, ItemRelevanceState>(StringComparer.Ordinal));
        }
    }
}
