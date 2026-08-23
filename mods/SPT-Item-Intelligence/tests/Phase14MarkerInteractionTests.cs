using System;
using SPTItemIntelligence;

static class Phase14MarkerInteractionTests
{
    public static int Run()
    {
        int assertions = 0;
        Expect(!ItemMarkerPresentation.From(ItemHoverText.Empty).IsVisible, "empty hover data hides the marker", ref assertions);
        ItemMarkerPresentation safe = ItemMarkerPresentation.From(new ItemHoverText("12,000 ₽", "", "SAFE TO SELL · surplus 2"));
        Expect(safe.Kind == ItemMarkerKind.Default, "sell value and internal surplus never color the marker", ref assertions);
        Expect(safe.Glyph == "ⓘ", "the attached marker uses the approved information glyph", ref assertions);
        Expect(ItemMarkerPresentation.From(new ItemHoverText("ITEM INTELLIGENCE", "", "LOADING ITEM DATA")).Kind == ItemMarkerKind.Default, "diagnostic state uses the default marker", ref assertions);
        Expect(ItemMarkerPresentation.From(new ItemHoverText("ITEM INTELLIGENCE", "", "DATA UNAVAILABLE")).Kind == ItemMarkerKind.Default, "unavailable data does not invent a requirement state", ref assertions);
        ItemHoverText defaultText = new ItemHoverText("1 ₽", "", "");
        Expect(object.ReferenceEquals(ItemMarkerPresentation.From(defaultText), ItemMarkerPresentation.From(defaultText)), "marker classification reuses immutable presentations", ref assertions);
        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 14 assertion failed: " + message);
    }
}
