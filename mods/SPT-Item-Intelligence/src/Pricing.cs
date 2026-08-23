using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace SPTItemIntelligence
{
    public enum PriceSource
    {
        None,
        Trader,
        Flea,
        Fallback
    }

    public enum ValueTier
    {
        White,
        Green,
        Blue,
        Purple,
        Gold
    }

    public sealed class ValueTierThresholds
    {
        public ValueTierThresholds(long green = 15000, long blue = 30000, long purple = 50000, long gold = 100000)
        {
            if (green < 0) throw new ArgumentOutOfRangeException(nameof(green));
            if (blue < green) throw new ArgumentOutOfRangeException(nameof(blue));
            if (purple < blue) throw new ArgumentOutOfRangeException(nameof(purple));
            if (gold < purple) throw new ArgumentOutOfRangeException(nameof(gold));
            Green = green;
            Blue = blue;
            Purple = purple;
            Gold = gold;
        }

        public long Green { get; }
        public long Blue { get; }
        public long Purple { get; }
        public long Gold { get; }

        public ValueTier Resolve(long value)
        {
            if (value >= Gold) return ValueTier.Gold;
            if (value >= Purple) return ValueTier.Purple;
            if (value >= Blue) return ValueTier.Blue;
            if (value >= Green) return ValueTier.Green;
            return ValueTier.White;
        }
    }

    public sealed class ItemPriceInput
    {
        public ItemPriceInput(
            string templateId,
            long traderUnitValue = 0,
            string traderName = null,
            long fleaUnitValue = 0,
            long fallbackUnitValue = 0,
            int width = 1,
            int height = 1,
            int stackCount = 1)
        {
            TemplateId = NormalizeId(templateId);
            TraderUnitValue = NonNegative(traderUnitValue);
            TraderName = string.IsNullOrWhiteSpace(traderName) ? string.Empty : traderName.Trim();
            FleaUnitValue = NonNegative(fleaUnitValue);
            FallbackUnitValue = NonNegative(fallbackUnitValue);
            Width = width < 1 ? 1 : width;
            Height = height < 1 ? 1 : height;
            StackCount = stackCount < 1 ? 1 : stackCount;
        }

        public string TemplateId { get; }
        public long TraderUnitValue { get; }
        public string TraderName { get; }
        public long FleaUnitValue { get; }
        public long FallbackUnitValue { get; }
        public int Width { get; }
        public int Height { get; }
        public int StackCount { get; }

        static long NonNegative(long value) => value < 0 ? 0 : value;

        static string NormalizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            return value.Trim().ToLowerInvariant();
        }
    }

    public sealed class ItemPriceState
    {
        internal ItemPriceState(
            string templateId,
            long traderUnitValue,
            string traderName,
            long fleaUnitValue,
            long fallbackUnitValue,
            PriceSource bestSource,
            long bestUnitValue,
            long totalValue,
            long valuePerSlot,
            int slotCount,
            int stackCount,
            ValueTier totalTier,
            ValueTier perSlotTier)
        {
            TemplateId = templateId;
            TraderUnitValue = traderUnitValue;
            TraderName = traderName;
            FleaUnitValue = fleaUnitValue;
            FallbackUnitValue = fallbackUnitValue;
            BestSource = bestSource;
            BestUnitValue = bestUnitValue;
            TotalValue = totalValue;
            ValuePerSlot = valuePerSlot;
            SlotCount = slotCount;
            StackCount = stackCount;
            TotalTier = totalTier;
            PerSlotTier = perSlotTier;
        }

        public string TemplateId { get; }
        public long TraderUnitValue { get; }
        public string TraderName { get; }
        public long FleaUnitValue { get; }
        public long FallbackUnitValue { get; }
        public PriceSource BestSource { get; }
        public long BestUnitValue { get; }
        public long TotalValue { get; }
        public long ValuePerSlot { get; }
        public int SlotCount { get; }
        public int StackCount { get; }
        public ValueTier TotalTier { get; }
        public ValueTier PerSlotTier { get; }
        public bool HasMarketValue => BestSource != PriceSource.None && BestUnitValue > 0;
    }

    public static class ItemPriceEvaluator
    {
        public static ItemPriceState Evaluate(ItemPriceInput input, ValueTierThresholds thresholds = null)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            thresholds = thresholds ?? new ValueTierThresholds();

            PriceSource source = PriceSource.None;
            long bestUnit = 0;

            if (input.TraderUnitValue > 0)
            {
                source = PriceSource.Trader;
                bestUnit = input.TraderUnitValue;
            }

            if (input.FleaUnitValue > bestUnit)
            {
                source = PriceSource.Flea;
                bestUnit = input.FleaUnitValue;
            }

            if (bestUnit == 0 && input.FallbackUnitValue > 0)
            {
                source = PriceSource.Fallback;
                bestUnit = input.FallbackUnitValue;
            }

            int slots = SaturatingMultiply(input.Width, input.Height);
            long total = SaturatingMultiply(bestUnit, input.StackCount);
            long perSlot = slots <= 1 ? total : total / slots;

            return new ItemPriceState(
                input.TemplateId,
                input.TraderUnitValue,
                input.TraderName,
                input.FleaUnitValue,
                input.FallbackUnitValue,
                source,
                bestUnit,
                total,
                perSlot,
                slots,
                input.StackCount,
                thresholds.Resolve(total),
                thresholds.Resolve(perSlot));
        }

        public static ItemPriceState WithStackCount(ItemPriceState state, int stackCount, ValueTierThresholds thresholds = null)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return Evaluate(new ItemPriceInput(
                state.TemplateId,
                state.TraderUnitValue,
                state.TraderName,
                state.FleaUnitValue,
                state.FallbackUnitValue,
                state.SlotCount,
                1,
                stackCount), thresholds);
        }

        static int SaturatingMultiply(int left, int right)
        {
            long value = (long)left * right;
            return value > int.MaxValue ? int.MaxValue : (int)value;
        }

        static long SaturatingMultiply(long left, int right)
        {
            if (left <= 0 || right <= 0) return 0;
            if (left > long.MaxValue / right) return long.MaxValue;
            return left * right;
        }
    }

    public sealed class ItemPriceIndex
    {
        static readonly ItemPriceIndex empty = new ItemPriceIndex(new Dictionary<string, ItemPriceState>(StringComparer.OrdinalIgnoreCase));
        readonly ReadOnlyDictionary<string, ItemPriceState> states;

        internal ItemPriceIndex(Dictionary<string, ItemPriceState> states)
        {
            this.states = new ReadOnlyDictionary<string, ItemPriceState>(states);
        }

        public static ItemPriceIndex Empty => empty;
        public IReadOnlyDictionary<string, ItemPriceState> States => states;

        public bool TryGet(string templateId, out ItemPriceState state)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                state = null;
                return false;
            }
            return states.TryGetValue(templateId.Trim(), out state);
        }
    }

    public static class ItemPriceIndexBuilder
    {
        public static ItemPriceIndex Build(IEnumerable<ItemPriceInput> inputs, ValueTierThresholds thresholds = null)
        {
            Dictionary<string, ItemPriceState> states = new Dictionary<string, ItemPriceState>(StringComparer.OrdinalIgnoreCase);
            if (inputs == null) return new ItemPriceIndex(states);

            foreach (ItemPriceInput input in inputs)
            {
                if (input == null || string.IsNullOrEmpty(input.TemplateId)) continue;
                states[input.TemplateId] = ItemPriceEvaluator.Evaluate(input, thresholds);
            }
            return new ItemPriceIndex(states);
        }
    }

    public sealed class ItemPriceStore
    {
        ItemPriceIndex current = ItemPriceIndex.Empty;

        public ItemPriceIndex Current => Volatile.Read(ref current);

        public void Replace(ItemPriceIndex next)
        {
            Volatile.Write(ref current, next ?? ItemPriceIndex.Empty);
        }

        public bool TryGet(string templateId, out ItemPriceState state)
        {
            return Current.TryGet(templateId, out state);
        }
    }
}
