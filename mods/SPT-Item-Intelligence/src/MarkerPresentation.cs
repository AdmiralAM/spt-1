namespace SPTItemIntelligence
{
    public enum ItemMarkerKind
    {
        Hidden,
        Default,
        QuestNow,
        Hideout,
        QuestLater
    }

    public sealed class ItemMarkerPresentation
    {
        const string InfoGlyph = "ⓘ";
        static readonly ItemMarkerPresentation hidden = new ItemMarkerPresentation(ItemMarkerKind.Hidden, string.Empty);
        static readonly ItemMarkerPresentation defaultMarker = new ItemMarkerPresentation(ItemMarkerKind.Default, InfoGlyph);
        static readonly ItemMarkerPresentation questNow = new ItemMarkerPresentation(ItemMarkerKind.QuestNow, InfoGlyph);
        static readonly ItemMarkerPresentation hideout = new ItemMarkerPresentation(ItemMarkerKind.Hideout, InfoGlyph);
        static readonly ItemMarkerPresentation questLater = new ItemMarkerPresentation(ItemMarkerKind.QuestLater, InfoGlyph);

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
            if (text.QuestNowMissing > 0) return questNow;
            if (text.HideoutMissing > 0) return hideout;
            if (text.QuestLaterMissing > 0) return questLater;
            return defaultMarker;
        }
    }
}
