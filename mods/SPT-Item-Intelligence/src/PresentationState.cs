using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace SPTItemIntelligence
{
    public sealed class ItemPresentationState
    {
        internal static readonly ItemPresentationState Empty = new ItemPresentationState(
            string.Empty,
            ItemRequirementState.Empty,
            null);

        internal ItemPresentationState(string templateId, ItemRequirementState requirement, ItemPriceState price)
        {
            TemplateId = templateId ?? string.Empty;
            Requirement = requirement ?? ItemRequirementState.Empty;
            Price = price;
        }

        public string TemplateId { get; }
        public ItemRequirementState Requirement { get; }
        public ItemPriceState Price { get; }
        public bool HasRequirementData => Requirement != null && Requirement != ItemRequirementState.Empty;
        public bool HasPriceData => Price != null;
        public bool IsSafeToSell => Requirement != null && Requirement.IsSafeToSell;
        public string HoldReason => Requirement == null ? string.Empty : Requirement.HoldReason;
        public long TotalValue => Price == null ? 0 : Price.TotalValue;
        public long ValuePerSlot => Price == null ? 0 : Price.ValuePerSlot;
        public PriceSource BestPriceSource => Price == null ? PriceSource.None : Price.BestSource;
        public ValueTier TotalTier => Price == null ? ValueTier.White : Price.TotalTier;
        public ValueTier PerSlotTier => Price == null ? ValueTier.White : Price.PerSlotTier;
    }

    public sealed class ItemPresentationIndex
    {
        static readonly ItemPresentationIndex empty = new ItemPresentationIndex(
            new Dictionary<string, ItemPresentationState>(StringComparer.OrdinalIgnoreCase));

        readonly ReadOnlyDictionary<string, ItemPresentationState> states;

        internal ItemPresentationIndex(Dictionary<string, ItemPresentationState> states)
        {
            this.states = new ReadOnlyDictionary<string, ItemPresentationState>(states);
        }

        public static ItemPresentationIndex Empty => empty;
        public int Count => states.Count;
        public IReadOnlyDictionary<string, ItemPresentationState> States => states;

        public ItemPresentationState Get(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId)) return ItemPresentationState.Empty;
            ItemPresentationState state;
            return states.TryGetValue(templateId.Trim(), out state) ? state : ItemPresentationState.Empty;
        }
    }

    public static class ItemPresentationIndexBuilder
    {
        public static ItemPresentationIndex Build(ItemRequirementStateIndex requirements, ItemPriceIndex prices)
        {
            requirements = requirements ?? ItemRequirementStateIndex.Empty;
            prices = prices ?? ItemPriceIndex.Empty;

            Dictionary<string, ItemPresentationState> states = new Dictionary<string, ItemPresentationState>(
                Math.Max(requirements.Count, prices.States.Count),
                StringComparer.OrdinalIgnoreCase);

            foreach (KeyValuePair<string, ItemRequirementState> pair in requirements.Entries)
            {
                if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Key)) continue;
                ItemPriceState price;
                prices.TryGet(pair.Key, out price);
                states[pair.Key] = new ItemPresentationState(pair.Key, pair.Value, price);
            }

            foreach (KeyValuePair<string, ItemPriceState> pair in prices.States)
            {
                if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Key) || states.ContainsKey(pair.Key)) continue;
                states[pair.Key] = new ItemPresentationState(pair.Key, ItemRequirementState.Empty, pair.Value);
            }

            return new ItemPresentationIndex(states);
        }
    }

    public sealed class ItemPresentationStore
    {
        ItemPresentationIndex current = ItemPresentationIndex.Empty;

        public ItemPresentationIndex Current => Volatile.Read(ref current);

        public void Refresh(ItemRequirementStateIndex requirements, ItemPriceIndex prices)
        {
            ItemPresentationIndex replacement = ItemPresentationIndexBuilder.Build(requirements, prices);
            Interlocked.Exchange(ref current, replacement);
        }

        public ItemPresentationState Get(string templateId)
        {
            return Current.Get(templateId);
        }
    }
}
