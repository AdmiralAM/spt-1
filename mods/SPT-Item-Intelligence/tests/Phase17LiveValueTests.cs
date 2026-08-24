using System;
using SPTItemIntelligence;

static class Phase17LiveValueTests
{
    public static int Run()
    {
        int assertions = 0;
        object[] snapshot = { new ItemPriceSnapshotEntry("VALUE", 15000, "Trader", 42000, 9000, 2, 2) };
        ItemPriceIndex index = new SptPriceDataProjector().Project(snapshot);
        ItemPriceState state;
        Expect(index.TryGet("value", out state), "schema-v2 price entry is projected", ref assertions);
        Expect(state.BestSource == PriceSource.Flea && state.TotalValue == 42000, "highest live source drives Value", ref assertions);
        Expect(state.ValuePerSlot == 10500, "template dimensions drive per-slot Value", ref assertions);
        ItemPriceState stack = ItemPriceEvaluator.WithStackCount(state, 10);
        Expect(stack.TotalValue == 420000 && stack.ValuePerSlot == 105000, "live ItemView stack count drives instance Value", ref assertions);

        object[] fallbackSnapshot = { new ItemPriceSnapshotEntry("NO_FLEA", 0, string.Empty, 0, 9000, 1, 1) };
        ItemPriceIndex fallbackIndex = new SptPriceDataProjector().Project(fallbackSnapshot);
        ItemPriceState fallback;
        Expect(fallbackIndex.TryGet("no_flea", out fallback), "fallback-only entry is projected", ref assertions);
        Expect(fallback.FleaUnitValue == 0 && fallback.FallbackUnitValue == 9000,
            "missing flea value remains distinct from handbook fallback", ref assertions);
        Expect(fallback.BestSource == PriceSource.Fallback && fallback.BestUnitValue == 9000,
            "fallback remains usable without being mislabeled as flea", ref assertions);

        ItemHoverText fleaText = new ItemHoverTextFormatter().Format(new ItemHoverState(new ItemPresentationStoreForTests(fallback).Presentation), ItemValueMode.Flea);
        Expect(fleaText.Primary.Length == 0, "Flea mode does not surface handbook fallback as Flea", ref assertions);
        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 17 assertion failed: " + message);
    }

    sealed class ItemPresentationStoreForTests
    {
        public ItemPresentationStoreForTests(ItemPriceState price)
        {
            ItemPresentationStore store = new ItemPresentationStore();
            store.Refresh(ItemRequirementStateIndex.Empty, ItemPriceIndexBuilder.Build(new[]
            {
                new ItemPriceInput(price.TemplateId, price.TraderUnitValue, price.TraderName, price.FleaUnitValue, price.FallbackUnitValue)
            }));
            Presentation = store.Get(price.TemplateId);
        }

        public ItemPresentationState Presentation { get; }
    }
}
