using System;

namespace SPTItemIntelligence
{
    public enum ItemMarkerKind
    {
        Hidden,
        Neutral,
        QuestNow,
        QuestLater,
        Hideout,
        Keep,
        Loading,
        Unavailable
    }

    public sealed class ItemMarkerPresentation
    {
        const string InfoGlyph = "ⓘ";
        static readonly ItemMarkerPresentation hidden = new ItemMarkerPresentation(ItemMarkerKind.Hidden, string.Empty);
        static readonly ItemMarkerPresentation neutral = new ItemMarkerPresentation(ItemMarkerKind.Neutral, InfoGlyph);
        static readonly ItemMarkerPresentation questNow = new ItemMarkerPresentation(ItemMarkerKind.QuestNow, InfoGlyph);
        static readonly ItemMarkerPresentation questLater = new ItemMarkerPresentation(ItemMarkerKind.QuestLater, InfoGlyph);
        static readonly ItemMarkerPresentation hideout = new ItemMarkerPresentation(ItemMarkerKind.Hideout, InfoGlyph);
        static readonly ItemMarkerPresentation keep = new ItemMarkerPresentation(ItemMarkerKind.Keep, InfoGlyph);
        static readonly ItemMarkerPresentation loading = new ItemMarkerPresentation(ItemMarkerKind.Loading, InfoGlyph);
        static readonly ItemMarkerPresentation unavailable = new ItemMarkerPresentation(ItemMarkerKind.Unavailable, InfoGlyph);

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
            if (string.Equals(text.Status, "LOADING ITEM DATA", StringComparison.OrdinalIgnoreCase)) return loading;
            if (string.Equals(text.Status, "DATA UNAVAILABLE", StringComparison.OrdinalIgnoreCase)) return unavailable;
            if (text.QuestNeededNow > 0) return questNow;
            if (text.QuestNeededLater > 0) return questLater;
            if (text.HideoutNeeded > 0) return hideout;
            if (text.KeepCount > 0 || text.Status.StartsWith("KEEP", StringComparison.OrdinalIgnoreCase)) return keep;
            return neutral;
        }
    }
}
