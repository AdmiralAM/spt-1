using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase10HoverRuntimeTests
{
    public static int Run()
    {
        int assertions = 0;
        ItemPresentationStore store = new ItemPresentationStore();
        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("item-a", traderUnitValue: 10000, traderName: "Therapist", fleaUnitValue: 24000, width: 2, height: 1)
        }));

        RecordingSink sink = new RecordingSink();
        ItemHoverRuntimeController controller = new ItemHoverRuntimeController(store, sink);

        ItemHoverText first = controller.OnHoverEnter(" ITEM-A ");
        Expect(first.Primary == "10,000 ₽ · Therapist", "hover enter publishes vendor-mode presentation text", ref assertions);
        Expect(first.Secondary.Length == 0, "hover runtime omits price-per-slot", ref assertions);
        Expect(sink.ShowCount == 1 && sink.ClearCount == 0, "first hover emits one UI update", ref assertions);
        Expect(controller.HasActiveItem, "controller tracks active hovered template", ref assertions);

        ItemHoverText repeated = controller.OnHoverEnter("item-a");
        Expect(object.ReferenceEquals(first, repeated), "repeated hover reuses formatted text object", ref assertions);
        Expect(sink.ShowCount == 1, "unchanged repeated hover does not republish UI", ref assertions);

        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput("item-a", traderUnitValue: 15000, traderName: "Therapist", fleaUnitValue: 40000, width: 2, height: 1)
        }));
        ItemHoverText refreshed = controller.RefreshActive();
        Expect(refreshed.Primary == "15,000 ₽ · Therapist", "snapshot refresh reprojects active vendor value without polling", ref assertions);
        Expect(sink.ShowCount == 2, "changed snapshot publishes exactly one UI update", ref assertions);

        store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndex.Empty);
        Expect(object.ReferenceEquals(ItemHoverText.Empty, controller.RefreshActive()), "removed active item clears projection", ref assertions);
        Expect(sink.ClearCount == 1 && !controller.HasActiveItem, "missing item clears UI and active template", ref assertions);

        controller.OnHoverExit();
        Expect(sink.ClearCount == 1, "redundant exit does not republish clear", ref assertions);

        Expect(object.ReferenceEquals(ItemHoverText.Empty, controller.OnHoverEnter("missing")), "unknown hover stays canonical empty", ref assertions);
        Expect(sink.ShowCount == 2 && sink.ClearCount == 1, "unknown hover produces no UI churn", ref assertions);
        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 10 assertion failed: " + message);
    }

    sealed class RecordingSink : IItemHoverViewSink
    {
        public int ShowCount;
        public int ClearCount;
        public ItemHoverText Last = ItemHoverText.Empty;

        public void Show(ItemHoverText text)
        {
            ShowCount++;
            Last = text;
        }

        public void Clear()
        {
            ClearCount++;
            Last = ItemHoverText.Empty;
        }
    }
}
