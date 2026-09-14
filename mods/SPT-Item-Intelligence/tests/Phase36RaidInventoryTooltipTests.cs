using System;
using SPTItemIntelligence;

static class Phase36RaidInventoryTooltipTests
{
    public static int Run()
    {
        int assertions = 0;
        ItemRequirementAllocation allocation = new ItemRequirementAllocation(2, 1, 3, 0, 1, 2, 0);
        ItemRequirementState requirement = new ItemRequirementState(
            "tpl", 2, 3, 0, 1, 4, 0,
            RequirementReasonFlags.CurrentQuest | RequirementReasonFlags.Hideout | RequirementReasonFlags.FoundInRaid,
            ItemRequirementDecision.Keep, "Current quest (FIR)", null, allocation);
        ItemPresentationState baseline = new ItemPresentationState("tpl", requirement, null);
        RaidRequirementLedger ledger = new RaidRequirementLedger();
        ledger.Observe("raid-stack", "tpl", 3, true);

        ItemPresentationState combined = ledger.Apply(baseline);
        Expect(combined.Requirement.OwnedCount == 5 && combined.Requirement.Allocation.OwnedFir == 4,
            "raid stack is included in the authoritative owned/FIR totals", ref assertions);
        Expect(combined.Requirement.Allocation.Coverage == RequirementCoverage.Enough,
            "combined stash and raid stock drives the final state", ref assertions);

        ItemHoverText text = new ItemHoverTextFormatter().Format(new ItemHoverState(combined));
        Expect(text.SummaryOwnedLine == "Owned ×5", "regular modes expose one combined owned count", ref assertions);
        Expect(text.OwnedBreakdownLine == "FIR ×4 · non-FIR ×1", "Full owns the FIR/non-FIR breakdown", ref assertions);
        Expect(!Contains(text, ItemTooltipMode.Normal, "non-FIR"), "Normal omits the breakdown", ref assertions);
        Expect(Contains(text, ItemTooltipMode.Full, "FIR ×4 · non-FIR ×1"), "Full renders the breakdown", ref assertions);
        return assertions;
    }

    static bool Contains(ItemHoverText text, ItemTooltipMode mode, string expected)
    {
        for (int i = 0; i < text.GetLineCount(mode); i++)
            if (text.GetLine(mode, i).Contains(expected, StringComparison.Ordinal)) return true;
        return false;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 36 assertion failed: " + message);
    }
}
