using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase8HoverTests
{
    public static int Run()
    {
        int assertions = 0;

        ItemRequirementState requirement = new ItemRequirementState(
            "item-a", 5, 1, 2, 1, 4, 1,
            RequirementReasonFlags.CurrentQuest | RequirementReasonFlags.Hideout,
            ItemRequirementDecision.SafeToSell,
            "Current quest");

        ItemRequirementStateIndex requirements = new ItemRequirementStateIndex(
            456,
            new Dictionary<string, ItemRequirementState>(StringComparer.Ordinal)
            {
                ["item-a"] = requirement
            });

        ItemPriceIndex prices = ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("item-a", traderUnitValue: 22000, traderName: "Therapist", fleaUnitValue: 32000, width: 2, height: 1)
        });

        ItemPresentationStore store = new ItemPresentationStore();
        store.Refresh(requirements, prices);
        ItemHoverPresentationAdapter hover = new ItemHoverPresentationAdapter(store);

        Expect(object.ReferenceEquals(hover.Active, ItemHoverState.Empty), "adapter starts empty", ref assertions);

        ItemHoverState first = hover.OnHoverEnter(" ITEM-A ");
        Expect(first.HasData && first.TemplateId == "item-a", "hover enter resolves normalized presentation state", ref assertions);
        Expect(first.TotalValue == 32000 && first.ValuePerSlot == 16000 && first.BestPriceSource == PriceSource.Flea, "hover projection exposes precomputed price data", ref assertions);
        Expect(first.OwnedCount == 5 && first.KeepCount == 4 && first.SurplusCount == 1, "hover projection exposes inventory requirement counts", ref assertions);
        Expect(first.QuestNeededNow == 1 && first.QuestNeededLater == 2 && first.HideoutNeeded == 1, "hover projection exposes requirement categories", ref assertions);
        Expect(first.IsSafeToSell && first.HoldReason == "Current quest", "hover projection exposes decision metadata", ref assertions);

        ItemHoverState repeated = hover.OnHoverEnter("item-a");
        Expect(object.ReferenceEquals(first, repeated), "repeated hover over unchanged presentation reuses projection", ref assertions);

        ItemHoverState missing = hover.OnHoverEnter("missing");
        Expect(object.ReferenceEquals(missing, ItemHoverState.Empty), "missing item uses canonical allocation-free hover state", ref assertions);

        ItemHoverState second = hover.OnHoverEnter("item-a");
        Expect(second.HasData, "hover can reactivate after an empty item", ref assertions);
        hover.OnHoverExit();
        Expect(object.ReferenceEquals(hover.Active, ItemHoverState.Empty), "hover exit clears active state", ref assertions);

        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndex.Empty);
        ItemHoverState afterRefresh = hover.OnHoverEnter("item-a");
        Expect(object.ReferenceEquals(afterRefresh, ItemHoverState.Empty), "hover reads current atomic presentation snapshot", ref assertions);

        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 8 assertion failed: " + message);
    }
}
