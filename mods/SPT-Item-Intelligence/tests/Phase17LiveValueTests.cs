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
        return assertions;
    }

    static void Expect(bool condition, string message, ref int assertions)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 17 assertion failed: " + message);
    }
}
