using System;
using System.Collections.Generic;
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
            "base policy leaves runtime ownership choice explicit", ref assertions);
        SenseVisualPolicy containerNeeded = SenseContainerPolicyEngine.Combine(new List<SenseVisualPolicy> { complete, missing });
        Expect(containerNeeded.Stock == SenseStockState.Missing && containerNeeded.Category == ItemNeedReason.ActiveQuest && containerNeeded.ItemCount == 1,
            "one unmet contained item overrides completed container contents", ref assertions);
        SenseVisualPolicy containerMany = SenseContainerPolicyEngine.Combine(new List<SenseVisualPolicy> { missing, partial, next, complete });
        Expect(containerMany.ItemCount == 3 && containerMany.Stock == SenseStockState.Missing,
            "container count includes useful unfinished items and excludes already-complete contents", ref assertions);
        SenseVisualPolicy containerComplete = SenseContainerPolicyEngine.Combine(new List<SenseVisualPolicy> { complete });
        Expect(containerComplete.Stock == SenseStockState.Complete && containerComplete.HasItemIntelligence,
            "a container with only completed tracked contents remains visibly green", ref assertions);
        SenseVisualPolicy irrelevantContainer = SenseContainerPolicyEngine.Combine(new List<SenseVisualPolicy>());
        Expect(!irrelevantContainer.HasItemIntelligence,
            "a container without tracked contents remains owned by native Sense", ref assertions);
        SenseVisualPolicy food = SenseVisualPolicy.Food(4);
        Expect(food.Category == ItemNeedReason.Food && food.Icon == ItemNeedIcon.Food && food.ItemCount == 4,
            "food uses the native lightning icon as its independent Sense category", ref assertions);
        Expect(SenseVisualPolicy.CategoryOnly(ItemNeedReason.Water, 2).Icon == ItemNeedIcon.Water &&
               SenseVisualPolicy.CategoryOnly(ItemNeedReason.Key, 1).Icon == ItemNeedIcon.Key &&
               SenseVisualPolicy.CategoryOnly(ItemNeedReason.Grenade, 3).Icon == ItemNeedIcon.Grenade &&
               SenseVisualPolicy.CategoryOnly(ItemNeedReason.Currency, 4, "EUR").CurrencyCode == "EUR",
            "water, keys, grenades and denomination-aware money map to separate native Sense icons", ref assertions);
        Expect(SenseContainerValuePolicy.Resolve(0) == SenseContainerValueTier.White &&
               SenseContainerValuePolicy.Resolve(50000) == SenseContainerValueTier.White &&
               SenseContainerValuePolicy.Resolve(50001) == SenseContainerValueTier.Blue &&
               SenseContainerValuePolicy.Resolve(99999) == SenseContainerValueTier.Blue &&
               SenseContainerValuePolicy.Resolve(100000) == SenseContainerValueTier.LightYellow &&
               SenseContainerValuePolicy.Resolve(199999) == SenseContainerValueTier.LightYellow &&
               SenseContainerValuePolicy.Resolve(200000) == SenseContainerValueTier.BrightYellow,
            "container total value color tiers honor every threshold boundary", ref assertions);
        SenseVisualPolicy categoryFallback = SenseVisualPolicy.CategoryOnly(ItemNeedReason.Water, 1);
        SenseVisualPolicy afterValue = SenseContainerPolicyEngine.Select(new[] { complete }, categoryFallback, false, 60000);
        Expect(afterValue.Category == ItemNeedReason.Water && afterValue.Stock == SenseStockState.None,
            "a more useful contained category or value band outranks a completed requirement marker", ref assertions);
        SenseVisualPolicy afterNativeValue = SenseContainerPolicyEngine.Select(new[] { complete }, categoryFallback, true, 0);
        Expect(!afterNativeValue.HasItemIntelligence,
            "native protected valuable and favorite indicators outrank completed requirements and fallback categories", ref assertions);
        SenseVisualPolicy stillUnmet = SenseContainerPolicyEngine.Select(new[] { missing }, categoryFallback, true, 250000);
        Expect(stillUnmet.Stock == SenseStockState.Missing && stillUnmet.Category == ItemNeedReason.ActiveQuest,
            "an unmet requirement remains above native valuables, aggregate price and other categories", ref assertions);
        SenseVisualPolicy onlyComplete = SenseContainerPolicyEngine.Select(new[] { complete }, null, false, 0);
        Expect(onlyComplete.Stock == SenseStockState.Complete,
            "completed contents retain the green state when nothing higher in the container priority exists", ref assertions);
        ItemRegistry registry = ItemRegistry.CreateDefault();
        Expect(registry.Resolve(new ItemDescriptor("test-key", "543be5e94bdc2df1348b4568", "unknown key", "key", "Template")).Category == ItemCategory.Key &&
               registry.Resolve(new ItemDescriptor("test-grenade", "543be6564bdc2df4348b4568", "unknown grenade", "grenade", "Template")).Category == ItemCategory.Grenade &&
               registry.Resolve(new ItemDescriptor("test-money", "543be5dd4bdc2deb348b4569", "unknown currency", "cash", "Template")).Category == ItemCategory.Currency,
            "unknown derived template IDs inherit vanilla key, grenade and currency semantics from parent IDs", ref assertions);
        return assertions;
    }
    static void Expect(bool value, string message, ref int assertions) { assertions++; if (!value) throw new InvalidOperationException("Phase 38 assertion failed: " + message); }
}
