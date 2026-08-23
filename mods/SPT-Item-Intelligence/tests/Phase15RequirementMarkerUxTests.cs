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

        Expect(text.GetLineCount(ItemTooltipMode.Normal) == 5, "normal mode exposes requirement progress", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 1) == "Quest Now: 2/2 ✓", "inventory is allocated to current quests first", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 2) == "Hideout: 3/4", "hideout is the second allocation and color priority", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 3) == "Quest Later: 0/3", "future quest progress is explicit", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Normal, 4) == "Keep ×4", "aggregate keep count remains visible", ref assertions);

        Expect(text.GetLineCount(ItemTooltipMode.Detailed) == 7, "detailed mode adds per-slot and owned counts", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Detailed, 5) == "Per slot: 21,000 ₽/slot", "detailed mode adds per-slot value", ref assertions);
        Expect(text.GetLine(ItemTooltipMode.Detailed, 6) == "Owned ×5", "detailed mode adds owned count", ref assertions);
        Expect(text.GetLineCount(ItemTooltipMode.Full) == 7, "full mode does not expose sell status or template id", ref assertions);
        for (int i = 0; i < text.GetLineCount(ItemTooltipMode.Full); i++)
        {
            string line = text.GetLine(ItemTooltipMode.Full, i);
            Expect(line.IndexOf("SAFE TO SELL", StringComparison.OrdinalIgnoreCase) < 0, "safe-to-sell is absent from every user mode", ref assertions);
            Expect(line.IndexOf("SURPLUS", StringComparison.OrdinalIgnoreCase) < 0, "surplus is absent from every user mode", ref assertions);
            Expect(!line.StartsWith("ID:", StringComparison.OrdinalIgnoreCase), "template id is absent from every user mode", ref assertions);
        }

        Expect(ItemMarkerPresentation.From(text).Kind == ItemMarkerKind.Hideout, "fulfilled current quest yields to unmet hideout", ref assertions);
        Expect(ItemMarkerPresentation.From(Facts(0, 2, 3, 0)).Kind == ItemMarkerKind.QuestNow, "unmet current quest owns the highest marker priority", ref assertions);
        Expect(ItemMarkerPresentation.From(Facts(2, 2, 3, 3)).Kind == ItemMarkerKind.Hideout, "unmet hideout precedes future quest", ref assertions);
        Expect(ItemMarkerPresentation.From(Facts(5, 2, 3, 3)).Kind == ItemMarkerKind.QuestLater, "future quest colors only while unmet", ref assertions);
        Expect(ItemMarkerPresentation.From(Facts(8, 2, 3, 3)).Kind == ItemMarkerKind.Default, "all fulfilled requirements use the default color", ref assertions);
        Expect(ItemMarkerPresentation.From(new ItemHoverText("12,000 ₽", "", "", "tpl", 99, 0, 0, 0, 7)).Kind == ItemMarkerKind.Default, "generic Keep is not a marker color state", ref assertions);
        return assertions;
    }

    static ItemHoverText Facts(int owned, int now, int later, int hideout)
    {
        return new ItemHoverText("1 ₽", "", "", "tpl", owned, now, later, hideout, now + later + hideout);
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 15 assertion failed: " + message);
    }
}
