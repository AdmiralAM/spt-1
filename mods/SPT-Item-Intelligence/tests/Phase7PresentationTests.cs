using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase7PresentationTests
{
    public static int Run()
    {
        int assertions = 0;

        ItemRequirementState requirement = new ItemRequirementState(
            "item-a", 3, 1, 0, 0, 1, 2,
            RequirementReasonFlags.CurrentQuest,
            ItemRequirementDecision.SafeToSell,
            "Current quest");

        ItemRequirementStateIndex requirementIndex = new ItemRequirementStateIndex(
            123,
            new Dictionary<string, ItemRequirementState>(StringComparer.Ordinal)
            {
                ["item-a"] = requirement
            });

        ItemPriceIndex priceIndex = ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("ITEM-A", traderUnitValue: 20000, traderName: "Therapist", fleaUnitValue: 30000, width: 2, height: 1),
            new ItemPriceInput("item-b", fallbackUnitValue: 9000)
        });

        ItemPresentationIndex combined = ItemPresentationIndexBuilder.Build(requirementIndex, priceIndex);
        ItemPresentationState a = combined.Get(" ITEM-A ");
        Expect(a.Requirement == requirement, "requirement state is preserved", ref assertions);
        Expect(a.HasPriceData && a.Price.BestSource == PriceSource.Flea, "pricing state is joined case-insensitively", ref assertions);
        Expect(a.IsSafeToSell && a.HoldReason == "Current quest", "decision and hold reason are exposed", ref assertions);
        Expect(a.TotalValue == 30000 && a.ValuePerSlot == 15000, "precomputed value fields are exposed", ref assertions);

        ItemPresentationState b = combined.Get("item-b");
        Expect(!b.HasRequirementData, "price-only item does not invent requirement data", ref assertions);
        Expect(b.HasPriceData && b.TotalValue == 9000, "price-only item remains available", ref assertions);

        ItemPresentationState missing = combined.Get("missing");
        Expect(object.ReferenceEquals(missing, ItemPresentationState.Empty), "missing item uses canonical empty state", ref assertions);
        Expect(!missing.HasPriceData && !missing.IsSafeToSell && missing.TotalValue == 0, "empty state is safe and allocation-free", ref assertions);

        ItemPresentationStore store = new ItemPresentationStore();
        store.Refresh(requirementIndex, priceIndex);
        ItemPresentationIndex first = store.Current;
        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndex.Empty);
        Expect(!object.ReferenceEquals(first, store.Current), "store atomically replaces snapshots", ref assertions);
        Expect(store.Get("item-a") == ItemPresentationState.Empty, "replacement removes stale entries", ref assertions);

        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 7 assertion failed: " + message);
    }
}
