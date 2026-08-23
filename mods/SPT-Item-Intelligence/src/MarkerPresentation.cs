using System;

namespace SPTItemIntelligence
{
    public enum ItemMarkerKind
    {
        Hidden,
        Information,
        SafeToSell,
        Keep,
        Loading,
        Unavailable
    }

    public sealed class ItemMarkerPresentation
    {
        static readonly ItemMarkerPresentation hidden = new ItemMarkerPresentation(ItemMarkerKind.Hidden, string.Empty);
        static readonly ItemMarkerPresentation information = new ItemMarkerPresentation(ItemMarkerKind.Information, "i");
        static readonly ItemMarkerPresentation safe = new ItemMarkerPresentation(ItemMarkerKind.SafeToSell, "✓");
        static readonly ItemMarkerPresentation keep = new ItemMarkerPresentation(ItemMarkerKind.Keep, "!");
        static readonly ItemMarkerPresentation loading = new ItemMarkerPresentation(ItemMarkerKind.Loading, "…");
        static readonly ItemMarkerPresentation unavailable = new ItemMarkerPresentation(ItemMarkerKind.Unavailable, "×");

        ItemMarkerPresentation(ItemMarkerKind kind, string glyph)
        {
            Kind = kind;
            Glyph = glyph;
        }

        public ItemMarkerKind Kind { get; }
        public string Glyph { get; }
        public bool IsVisible => Kind != ItemMarkerKind.Hidden;

        public static ItemMarkerPresentation From(ItemHoverText text)
        {
            if (text == null || !text.HasData) return hidden;
            string status = text.Status ?? string.Empty;
            if (status.StartsWith("SAFE TO SELL", StringComparison.OrdinalIgnoreCase)) return safe;
            if (status.StartsWith("KEEP", StringComparison.OrdinalIgnoreCase)) return keep;
            if (string.Equals(status, "LOADING ITEM DATA", StringComparison.OrdinalIgnoreCase)) return loading;
            if (string.Equals(status, "DATA UNAVAILABLE", StringComparison.OrdinalIgnoreCase)) return unavailable;
            return information;
        }
    }
}
