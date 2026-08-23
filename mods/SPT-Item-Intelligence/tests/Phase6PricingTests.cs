using System;
using System.Collections.Generic;
using SPTItemIntelligence;

static class Phase6PricingTests
{
    static int assertions;

    public static int Run()
    {
        assertions = 0;

        ItemPriceState trader = ItemPriceEvaluator.Evaluate(new ItemPriceInput("trader", 22000, "Therapist", 18000, 5000, 1, 1, 1));
        Expect(trader.BestSource == PriceSource.Trader && trader.BestUnitValue == 22000, "trader wins when higher than flea");
        Expect(trader.TraderName == "Therapist", "best trader label is preserved");

        ItemPriceState flea = ItemPriceEvaluator.Evaluate(new ItemPriceInput("flea", 18000, "Mechanic", 42000, 5000, 2, 2, 1));
        Expect(flea.BestSource == PriceSource.Flea && flea.BestUnitValue == 42000, "flea wins when higher than trader");
        Expect(flea.TotalValue == 42000 && flea.ValuePerSlot == 10500, "price per slot uses occupied cells");

        ItemPriceState fallback = ItemPriceEvaluator.Evaluate(new ItemPriceInput("fallback", 0, null, 0, 12345, 1, 1, 1));
        Expect(fallback.BestSource == PriceSource.Fallback && fallback.BestUnitValue == 12345, "fallback is used only without market values");

        ItemPriceState noValue = ItemPriceEvaluator.Evaluate(new ItemPriceInput("none"));
        Expect(noValue.BestSource == PriceSource.None && !noValue.HasMarketValue, "missing valuation remains explicit");

        ItemPriceState stack = ItemPriceEvaluator.Evaluate(new ItemPriceInput("stack", 1000, "Trader", 0, 0, 1, 1, 60));
        Expect(stack.TotalValue == 60000 && stack.ValuePerSlot == 60000, "stack count contributes to total and per-slot value");
        ItemPriceState scaledStack = ItemPriceEvaluator.WithStackCount(flea, 10);
        Expect(scaledStack.TotalValue == 420000 && scaledStack.ValuePerSlot == 105000, "registered item stack count rescales cached unit pricing");

        ValueTierThresholds defaults = new ValueTierThresholds();
        Expect(defaults.Resolve(14999) == ValueTier.White, "default white threshold");
        Expect(defaults.Resolve(15000) == ValueTier.Green, "default green threshold");
        Expect(defaults.Resolve(30000) == ValueTier.Blue, "default blue threshold");
        Expect(defaults.Resolve(50000) == ValueTier.Purple, "default purple threshold");
        Expect(defaults.Resolve(100000) == ValueTier.Gold, "default gold threshold");

        ValueTierThresholds custom = new ValueTierThresholds(10000, 20000, 40000, 80000);
        ItemPriceState customTier = ItemPriceEvaluator.Evaluate(new ItemPriceInput("custom", 25000, "Trader"), custom);
        Expect(customTier.TotalTier == ValueTier.Blue, "custom thresholds are honored");

        ItemPriceIndex index = ItemPriceIndexBuilder.Build(new[]
        {
            new ItemPriceInput(" ABC ", 20000, "Trader"),
            new ItemPriceInput("def", 30000, "Trader")
        });
        ItemPriceState indexed;
        Expect(index.TryGet("abc", out indexed) && indexed.BestUnitValue == 20000, "index normalizes and resolves template ids");
        Expect(index.TryGet("ABC", out indexed), "index lookup is case-insensitive");

        ItemPriceStore store = new ItemPriceStore();
        store.Replace(index);
        Expect(store.TryGet("def", out indexed) && indexed.BestUnitValue == 30000, "atomic price store exposes published snapshot");
        store.Replace(null);
        Expect(!store.TryGet("def", out indexed), "null replacement publishes an empty snapshot");

        bool invalidThresholds = false;
        try { new ValueTierThresholds(30000, 20000, 40000, 80000); }
        catch (ArgumentOutOfRangeException) { invalidThresholds = true; }
        Expect(invalidThresholds, "invalid tier ordering is rejected");

        ItemPriceState sanitized = ItemPriceEvaluator.Evaluate(new ItemPriceInput("sanitized", -1, null, -2, -3, 0, -5, 0));
        Expect(sanitized.BestSource == PriceSource.None && sanitized.SlotCount == 1 && sanitized.StackCount == 1, "invalid input dimensions and negative values are sanitized");

        ItemPriceState saturated = ItemPriceEvaluator.Evaluate(new ItemPriceInput("saturated", long.MaxValue, "Trader", 0, 0, 1, 1, int.MaxValue));
        Expect(saturated.TotalValue == long.MaxValue, "stack total saturates instead of overflowing");

        return assertions;
    }

    static void Expect(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Phase 6 assertion failed: " + message);
    }
}
