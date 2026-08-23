using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace SPTItemIntelligence
{
    public sealed class ItemHoverText
    {
        internal static readonly ItemHoverText Empty = new ItemHoverText(string.Empty, string.Empty, string.Empty);

        public ItemHoverText(string primary, string secondary, string status)
        {
            Primary = primary ?? string.Empty;
            Secondary = secondary ?? string.Empty;
            Status = status ?? string.Empty;
        }

        public string Primary { get; }
        public string Secondary { get; }
        public string Status { get; }
        public bool HasData => Primary.Length != 0 || Secondary.Length != 0 || Status.Length != 0;
    }

    public sealed class ItemHoverTextFormatter
    {
        public ItemHoverText Format(ItemHoverState hover)
        {
            if (hover == null || !hover.HasData) return ItemHoverText.Empty;

            string primary = hover.TotalValue > 0 ? FormatRoubles(hover.TotalValue) : string.Empty;
            string secondary = hover.ValuePerSlot > 0 ? FormatRoubles(hover.ValuePerSlot) + "/slot" : string.Empty;
            string status = FormatStatus(hover);
            return new ItemHoverText(primary, secondary, status);
        }

        static string FormatStatus(ItemHoverState hover)
        {
            if (hover.IsSafeToSell) return hover.SurplusCount > 0 ? "SAFE TO SELL · surplus " + hover.SurplusCount.ToString(CultureInfo.InvariantCulture) : "SAFE TO SELL";
            if (!string.IsNullOrEmpty(hover.HoldReason)) return "KEEP · " + hover.HoldReason;
            if (hover.KeepCount > 0) return "KEEP · " + hover.KeepCount.ToString(CultureInfo.InvariantCulture) + " needed";
            return string.Empty;
        }

        static string FormatRoubles(long value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture) + " ₽";
        }
    }

    public sealed class ItemHoverTextCache
    {
        readonly ItemHoverTextFormatter formatter;
        readonly Dictionary<ItemPresentationState, ItemHoverText> cache = new Dictionary<ItemPresentationState, ItemHoverText>(ReferenceComparer.Instance);
        ItemPresentationIndex lastIndex;

        public ItemHoverTextCache(ItemHoverTextFormatter formatter = null)
        {
            this.formatter = formatter ?? new ItemHoverTextFormatter();
        }

        public ItemHoverText Get(ItemHoverState hover, ItemPresentationIndex index)
        {
            if (hover == null || !hover.HasData) return ItemHoverText.Empty;
            ItemPresentationState presentation = hover.Presentation;

            if (!object.ReferenceEquals(lastIndex, index))
            {
                cache.Clear();
                lastIndex = index;
            }

            ItemHoverText text;
            if (cache.TryGetValue(presentation, out text)) return text;
            text = formatter.Format(hover);
            cache[presentation] = text;
            return text;
        }

        sealed class ReferenceComparer : IEqualityComparer<ItemPresentationState>
        {
            internal static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(ItemPresentationState x, ItemPresentationState y) => object.ReferenceEquals(x, y);
            public int GetHashCode(ItemPresentationState obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
