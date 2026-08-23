using System;
using SPTItemIntelligence;

static class Phase14MarkerInteractionTests
{
    public static int Run()
    {
        int assertions = 0;
        Expect(!ItemMarkerPresentation.From(ItemHoverText.Empty).IsVisible, "empty hover data hides the marker", ref assertions);
        ItemMarkerPresentation neutral = ItemMarkerPresentation.From(new ItemHoverText("", "", "SAFE TO SELL · surplus 2"));
        Expect(neutral.Kind == ItemMarkerKind.Neutral, "safe-to-sell does not drive marker color", ref assertions);
        Expect(neutral.Glyph == "ⓘ", "the anchored marker uses the approved information glyph", ref assertions);
        Expect(ItemMarkerPresentation.From(new ItemHoverText("", "", "KEEP · quest now")).Kind == ItemMarkerKind.Keep, "required items use the keep marker", ref assertions);
        Expect(ItemMarkerPresentation.From(new ItemHoverText("ITEM INTELLIGENCE", "a", "LOADING ITEM DATA")).Kind == ItemMarkerKind.Loading, "loading state has a diagnostic marker", ref assertions);
        Expect(ItemMarkerPresentation.From(new ItemHoverText("ITEM INTELLIGENCE", "a", "DATA UNAVAILABLE")).Kind == ItemMarkerKind.Unavailable, "unavailable state has an error marker", ref assertions);
        Expect(ItemMarkerPresentation.From(new ItemHoverText("ITEM INTELLIGENCE", "a", "NO REQUIREMENT DATA")).Kind == ItemMarkerKind.Neutral, "neutral data uses the neutral requirement marker", ref assertions);

        ItemHoverText safeText = new ItemHoverText("", "", "SAFE TO SELL");
        Expect(object.ReferenceEquals(ItemMarkerPresentation.From(safeText), ItemMarkerPresentation.From(safeText)), "per-frame classification reuses immutable marker objects", ref assertions);
        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 14 assertion failed: " + message);
    }
}
