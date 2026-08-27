namespace SPTEconomy;

public sealed record ItemRewardStackPlan
{
    public required bool Eligible { get; init; }
    public required double CurrentCount { get; init; }
    public required double UnitHandbookPrice { get; init; }
    public required double CurrentHandbookValue { get; init; }
    public required double ImmutableHandbookValue { get; init; }
    public required double BudgetCap { get; init; }
    public double? TargetCount { get; init; }
    public double? TargetHandbookValue { get; init; }
    public double? TargetBundleHandbookValue { get; init; }
    public required string Reason { get; init; }
}

public static class ItemRewardStackPlanner
{
    private const double IntegerTolerance = 0.000001;

    public static ItemRewardStackPlan Plan(double currentCount, double unitHandbookPrice, double budgetCap)
        => PlanWithinBundle(currentCount, unitHandbookPrice, immutableHandbookValue: 0, budgetCap);

    public static ItemRewardStackPlan PlanWithinBundle(
        double currentCount,
        double unitHandbookPrice,
        double immutableHandbookValue,
        double budgetCap)
    {
        if (!double.IsFinite(currentCount)
            || !double.IsFinite(unitHandbookPrice)
            || !double.IsFinite(immutableHandbookValue)
            || !double.IsFinite(budgetCap))
            return Block(currentCount, unitHandbookPrice, immutableHandbookValue, budgetCap, "NonFiniteInput");

        if (currentCount < 1 || unitHandbookPrice <= 0 || immutableHandbookValue < 0 || budgetCap <= 0)
            return Block(currentCount, unitHandbookPrice, immutableHandbookValue, budgetCap, "NonPositiveInput");

        var roundedCount = Math.Round(currentCount, 0);
        if (Math.Abs(currentCount - roundedCount) > IntegerTolerance)
            return Block(currentCount, unitHandbookPrice, immutableHandbookValue, budgetCap, "NonIntegralStackCount");

        if (roundedCount <= 1)
            return Block(currentCount, unitHandbookPrice, immutableHandbookValue, budgetCap, "SingleItemCannotBeReducedWithoutStructuralRemoval");

        var currentStackValue = roundedCount * unitHandbookPrice;
        var currentBundleValue = immutableHandbookValue + currentStackValue;
        if (currentBundleValue <= budgetCap + IntegerTolerance)
            return Block(currentCount, unitHandbookPrice, immutableHandbookValue, budgetCap, "AlreadyWithinBudget");

        var mutableBudget = budgetCap - immutableHandbookValue;
        if (mutableBudget < unitHandbookPrice)
            return Block(
                currentCount,
                unitHandbookPrice,
                immutableHandbookValue,
                budgetCap,
                immutableHandbookValue > 0 ? "ImmutableRewardsConsumeBudget" : "BudgetBelowOneItemFloor");

        var targetCount = Math.Floor(mutableBudget / unitHandbookPrice);
        if (targetCount < 1)
            return Block(currentCount, unitHandbookPrice, immutableHandbookValue, budgetCap, "BudgetBelowOneItemFloor");

        targetCount = Math.Min(targetCount, roundedCount);
        if (targetCount >= roundedCount)
            return Block(currentCount, unitHandbookPrice, immutableHandbookValue, budgetCap, "NoReductionRequired");

        var targetStackValue = targetCount * unitHandbookPrice;
        return new ItemRewardStackPlan
        {
            Eligible = true,
            CurrentCount = roundedCount,
            UnitHandbookPrice = unitHandbookPrice,
            CurrentHandbookValue = Math.Round(currentStackValue, 2),
            ImmutableHandbookValue = Math.Round(immutableHandbookValue, 2),
            BudgetCap = Math.Round(budgetCap, 2),
            TargetCount = targetCount,
            TargetHandbookValue = Math.Round(targetStackValue, 2),
            TargetBundleHandbookValue = Math.Round(immutableHandbookValue + targetStackValue, 2),
            Reason = immutableHandbookValue > 0
                ? "SingleMutableKnownPriceStackCanBeReducedWithinWholeBundleBudget"
                : "SingleKnownPriceStackCanBeReducedWithoutChangingTemplateOrRewardStructure",
        };
    }

    private static ItemRewardStackPlan Block(
        double currentCount,
        double unitHandbookPrice,
        double immutableHandbookValue,
        double budgetCap,
        string reason)
    {
        var currentValue = double.IsFinite(currentCount) && double.IsFinite(unitHandbookPrice)
            ? currentCount * unitHandbookPrice
            : double.NaN;

        return new ItemRewardStackPlan
        {
            Eligible = false,
            CurrentCount = currentCount,
            UnitHandbookPrice = unitHandbookPrice,
            CurrentHandbookValue = double.IsFinite(currentValue) ? Math.Round(currentValue, 2) : currentValue,
            ImmutableHandbookValue = double.IsFinite(immutableHandbookValue) ? Math.Round(immutableHandbookValue, 2) : immutableHandbookValue,
            BudgetCap = double.IsFinite(budgetCap) ? Math.Round(budgetCap, 2) : budgetCap,
            TargetCount = null,
            TargetHandbookValue = null,
            TargetBundleHandbookValue = null,
            Reason = reason,
        };
    }
}
