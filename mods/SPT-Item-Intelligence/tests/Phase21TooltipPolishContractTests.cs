using System;
using SPTItemIntelligence;

static class Phase21TooltipPolishContractTests
{
    public static int Run()
    {
        int assertions = 0;

        ItemHoverText fir = new ItemHoverText(
            "1 ₽ · Therapist", "", "", "fir", 1, 0, 7, 0, 7, "", null,
            ownedFoundInRaid: 1, questNowFoundInRaid: 0, questLaterFoundInRaid: 7);

        Expect(fir.QuestLaterLine.Contains("FIR"), "raw requirement line retains FIR detail for Full mode", ref assertions);
        Expect(fir.OwnedLine.Contains("FIR"), "raw owned line retains FIR detail for Full mode", ref assertions);
        Expect(fir.GetLineCount(ItemTooltipMode.Normal) >= 2, "normal contract still exposes requirement progress", ref assertions);

        ItemHoverText partial = new ItemHoverText("1 ₽", "", "", "partial", 2, 0, 4, 0, 4);
        Expect(partial.QuestLaterLine == "Quest Later: 2/4", "partial requirement preserves compact owned/required form", ref assertions);

        ItemHoverText complete = new ItemHoverText("1 ₽", "", "", "complete", 4, 0, 4, 0, 4);
        Expect(complete.QuestLaterLine.EndsWith("✓", StringComparison.Ordinal), "completed requirement keeps semantic completion marker", ref assertions);

        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 21 assertion failed: " + message);
    }
}
