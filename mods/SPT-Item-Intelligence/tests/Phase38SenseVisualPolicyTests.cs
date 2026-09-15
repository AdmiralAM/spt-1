using System;
using SPTItemIntelligence;

static class Phase38SenseVisualPolicyTests
{
    public static int Run()
    {
        int assertions = 0;
        SenseVisualPolicy missing = SenseVisualPolicyEngine.Evaluate(new ItemRequirementAllocation(0, 0, 2, 0, 0, 0, 0));
        Expect(missing.Icon == ItemNeedIcon.Quest && missing.Stock == SenseStockState.Missing && missing.Remaining == 2,
            "category icon and red-state contract are independent", ref assertions);
        SenseVisualPolicy partial = SenseVisualPolicyEngine.Evaluate(new ItemRequirementAllocation(1, 0, 2, 0, 0, 0, 0));
        Expect(partial.Stock == SenseStockState.Partial, "some stock but insufficient immediate coverage is partial", ref assertions);
        SenseVisualPolicy next = SenseVisualPolicyEngine.Evaluate(new ItemRequirementAllocation(2, 0, 2, 3, 0, 0, 0));
        Expect(next.Stock == SenseStockState.NextCovered && next.Category == ItemNeedReason.ActiveQuest && next.SecondaryCategory == ItemNeedReason.FutureQuest,
            "light-green state means nearest category covered while later demand remains", ref assertions);
        SenseVisualPolicy complete = SenseVisualPolicyEngine.Evaluate(new ItemRequirementAllocation(5, 0, 2, 3, 0, 0, 0));
        Expect(complete.Stock == SenseStockState.Complete, "bright-green state means all demand covered", ref assertions);
        Expect(!complete.ShouldReplaceIcon(true) && complete.ShouldReplaceIcon(false),
            "wishlist/value/category icons owned by Sense are preserved", ref assertions);
        return assertions;
    }
    static void Expect(bool value, string message, ref int assertions) { assertions++; if (!value) throw new InvalidOperationException("Phase 38 assertion failed: " + message); }
}
