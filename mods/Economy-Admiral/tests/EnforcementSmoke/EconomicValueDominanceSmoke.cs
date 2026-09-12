using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class EconomicValueDominanceSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Economy Admiral economic-value reward smoke: {message}");
        }

        var expensiveSmallStack = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("cheap", 100d, true, 10d),
            new GroupedItemRewardEntry("expensive", 2d, true, 1000d),
        ]);
        Require(expensiveSmallStack.Eligible && expensiveSmallStack.SelectedIndex == 1,
            "automatic grouped selection must choose the largest handbook-value contribution, not the largest raw count");

        var equalValue = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("cheap", 100d, true, 10d),
            new GroupedItemRewardEntry("expensive", 2d, true, 500d),
        ]);
        Require(equalValue.Eligible && equalValue.SelectedIndex == 0,
            "equal total economic contributions must prefer the unique stack with greater reducible value while preserving one item");

        var trueTie = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tie-a", 3d, true, 100d),
            new GroupedItemRewardEntry("tie-b", 3d, true, 100d),
        ]);
        Require(!trueTie.Eligible && trueTie.Reason == "AmbiguousMultipleReducibleStacks",
            "equal total and reducible economic contributions must remain fail-closed");

        var manual = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("cheap", 3d, false),
            new GroupedItemRewardEntry("sibling", 1d, false),
        ], requireKnownHandbookPrice: false);
        Require(manual.Eligible && manual.SelectedIndex == 0,
            "manual exact selection must remain price-independent and structurally strict");
    }
}
