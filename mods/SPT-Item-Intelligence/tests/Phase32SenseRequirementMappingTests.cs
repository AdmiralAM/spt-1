using System;
using SPTItemIntelligence;

static class Phase32SenseRequirementMappingTests
{
    public static int Run()
    {
        int assertions = 0;

        ItemRequirementAllocation overlap = new ItemRequirementAllocation(0, 0, 2, 3, 4, 1, 2);
        SenseRequirementPresentation fir = SenseRequirementMapper.Map(ItemIntelligenceDecisionEngine.Evaluate(overlap, true));
        Expect(fir.OverridesSense && fir.Icon == ItemNeedIcon.Quest && fir.PrimaryReason == ItemNeedReason.ActiveQuest,
            "active quest is the primary unmet reason", ref assertions);
        Expect(fir.OutlineReason == ItemNeedReason.Hideout && fir.Remaining == 2,
            "hideout is the secondary outline and remaining belongs to the primary reason", ref assertions);

        ItemRequirementAllocation firOnlyAndHideout = new ItemRequirementAllocation(0, 0, 2, 1, 3, 2, 1);
        SenseRequirementPresentation nonFir = SenseRequirementMapper.Map(ItemIntelligenceDecisionEngine.Evaluate(firOnlyAndHideout, false));
        Expect(nonFir.Icon == ItemNeedIcon.Hideout && nonFir.PrimaryReason == ItemNeedReason.Hideout,
            "a non-FIR candidate does not pretend to satisfy an FIR-only quest", ref assertions);
        Expect(nonFir.OutlineReason == ItemNeedReason.None,
            "ineligible FIR-only future demand is not emitted as a secondary outline", ref assertions);

        ItemRequirementAllocation currentAndHideout = new ItemRequirementAllocation(0, 0, 1, 0, 1, 1, 0);
        SenseRequirementPresentation first = SenseRequirementMapper.Map(ItemIntelligenceDecisionEngine.Evaluate(currentAndHideout, true));
        SenseRequirementPresentation afterPickup = SenseRequirementMapper.Map(ItemIntelligenceDecisionEngine.Evaluate(currentAndHideout, true, 1, 1));
        SenseRequirementPresentation complete = SenseRequirementMapper.Map(ItemIntelligenceDecisionEngine.Evaluate(currentAndHideout, true, 2, 2));
        Expect(first.Icon == ItemNeedIcon.Quest && first.OutlineReason == ItemNeedReason.Hideout,
            "one item can expose two reasons without being counted twice", ref assertions);
        Expect(afterPickup.Icon == ItemNeedIcon.Hideout && afterPickup.Remaining == 1,
            "a raid pickup advances the single allocation to the next unmet requirement", ref assertions);
        Expect(!complete.OverridesSense && complete.Icon == ItemNeedIcon.None,
            "Enough yields to the existing Sense presentation", ref assertions);

        ItemRequirementAllocation future = new ItemRequirementAllocation(0, 0, 0, 2, 0, 0, 0);
        SenseRequirementPresentation later = SenseRequirementMapper.Map(ItemIntelligenceDecisionEngine.Evaluate(future, true));
        Expect(later.Icon == ItemNeedIcon.FutureQuest && later.Remaining == 2,
            "future quest has a distinct added indicator", ref assertions);

        ItemRequirementAllocation notNeeded = new ItemRequirementAllocation(5, 2, 0, 0, 0, 0, 0);
        Expect(!SenseRequirementMapper.Map(ItemIntelligenceDecisionEngine.Evaluate(notNeeded, true)).OverridesSense,
            "Not Needed never replaces valuable, category, or wishlist Sense visuals", ref assertions);

        ItemRequirementAllocation enough = new ItemRequirementAllocation(3, 1, 1, 1, 1, 1, 0);
        Expect(!SenseRequirementMapper.Map(ItemIntelligenceDecisionEngine.Evaluate(enough, true)).OverridesSense,
            "Enough never replaces the native Sense result", ref assertions);
        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 32 assertion failed: " + message);
    }
}
