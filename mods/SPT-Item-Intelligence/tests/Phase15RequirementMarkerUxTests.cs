using System;
using SPTItemIntelligence;

static class Phase15RequirementMarkerUxTests
{
    public static int Run()
    {
        int assertions = 0;
        ItemHoverText text = new ItemHoverText(
            "42,000 ₽", "21,000 ₽/slot", "SAFE TO SELL · surplus 1", "tpl-a",
            ownedCount: 5, questNeededNow: 2, questNeededLater: 3, hideoutNeeded: 4, keepCount: 4);

        Expect(text.GetLineCount(ItemTooltipMode.Minimal) == 2, "minimal mode remains compact", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Minimal, 0) == "Value: 42,000 ₽", "value is the first minimal line", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Minimal, 1) == "Keep ×4", "keep count is the second minimal line", ref assertions);

        Expect(text.GetLineCount(ItemTooltipMode.Normal) == 5, "normal mode exposes the prioritized requirement facts", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 1) == "Quest Now ×2", "current quest precedes future requirements", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 2) == "Quest Later ×3", "future quest is explicit", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 3) == "Hideout ×4", "hideout requirement is explicit", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 4) == "Keep ×4", "aggregate keep count remains visible", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 5).Length == 0, "safe-to-sell and surplus are not central in normal mode", ref assertions);

        Expect(text.GetLineCount(ItemTooltipMode.Detailed) == 7, "detailed mode adds per-slot and owned counts", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Detailed, 5) == "Per slot: 21,000 ₽/slot", "detailed mode adds per-slot value after requirements", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Detailed, 6) == "Owned ×5", "detailed mode adds owned count", ref assertions);

        Expect(text.GetLineCount(ItemTooltipMode.Full) == 9, "full mode adds low-priority diagnostics", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Full, 7) == "SAFE TO SELL · surplus 1", "decision appears only near the end of full mode", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Full, 8) == "ID: tpl-a", "full mode exposes template id last", ref assertions);

        ItemMarkerPresentation now = ItemMarkerPresentation.From(text);
        Expect(now.Kind == ItemMarkerKind.QuestNow && now.Glyph == "ⓘ", "Quest Now owns the highest marker priority and keeps the info glyph", ref assertions);
        Expect(ItemMarkerPresentation.From(Facts(0, 2, 3, 3)).Kind == ItemMarkerKind.QuestLater, "Quest Later owns the next marker priority", ref assertions);
        Expect(ItemMarkerPresentation.From(Facts(0, 0, 3, 3)).Kind == ItemMarkerKind.Hideout, "Hideout owns the next marker priority", ref assertions);
        Expect(ItemMarkerPresentation.From(Facts(0, 0, 0, 3)).Kind == ItemMarkerKind.Keep, "generic Keep is the final requirement color", ref assertions);
        Expect(ItemMarkerPresentation.From(new ItemHoverText("12,000 ₽", "", "SAFE TO SELL")).Kind == ItemMarkerKind.Neutral, "value and sell decision never color the marker", ref assertions);
        Expect(object.ReferenceEquals(ItemMarkerPresentation.From(text), ItemMarkerPresentation.From(text)), "marker classification is allocation-free", ref assertions);
        return assertions;
    }

    static ItemHoverText Facts(int now, int later, int hideout, int keep)
    {
        return new ItemHoverText("", "", "KEEP", "tpl", 1, now, later, hideout, keep);
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 15 assertion failed: " + message);
    }
}
