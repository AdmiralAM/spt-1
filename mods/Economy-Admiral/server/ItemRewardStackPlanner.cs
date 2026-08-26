namespace SPTEconomy;

public sealed record ItemRewardStackPlan
{
    public required bool Eligible { get; init; }
    public required double CurrentCount { get; init; }
    public required double UnitHandbookPrice { get; init; }
    public required double CurrentHandbookValue { get; init; }
    public required double BudgetCap { get; init; }
    public double? TargetCount { get; init; }
    public double? TargetHandbookValue { get; init; }
    public required string Reason { get; init; }
}

public static class ItemRewardStackPlanner
{
    private const double IntegerTolerance = 0.000001;

    public static ItemRewardStackPlan Plan(double currentCount, double unitHandbookPrice, double budgetCap)
    {
        if (!double.IsFinite(currentCount) || !double.IsFinite(unitHandbookPrice) || !double.IsFinite(budgetCap))
            return Block(currentCount, unitHandbookPrice, budgetCap, "NonFiniteInput");

        if (currentCount < 1 || unitHandbookPrice <= 0 || budgetCap <= 0)
            return Block(currentCount, unitHandbookPrice, budgetCap, "NonPositiveInput");

        var roundedCount = Math.Round(currentCount, 0);
        if (Math.Abs(currentCount - roundedCount) > IntegerTolerance)
            return Block(currentCount, unitHandbookPrice, budgetCap, "NonIntegralStackCount");

        if (roundedCount <= 1)
            return Block(currentCount, unitHandbookPrice, budgetCap, "SingleItemCannotBeReducedWithoutStructuralRemoval");

        var currentValue = roundedCount * unitHandbookPrice;
        if (currentValue <= budgetCap + 0.000001)
            return Block(currentCount, unitHandbookPrice, budgetCap, "AlreadyWithinBudget");

        var targetCount = Math.Floor(budgetCap / unitHandbookPrice);
        if (targetCount < 1)
            return Block(currentCount, unitHandbookPrice, budgetCap, "BudgetBelowOneItemFloor");

        targetCount = Math.Min(targetCount, roundedCount);
        if (targetCount >= roundedCount)
            return Block(currentCount, unitHandbookPrice, budgetCap, "NoReductionRequired");

        return new ItemRewardStackPlan
        {
            Eligible = true,
            CurrentCount = roundedCount,
            UnitHandbookPrice = unitHandbookPrice,
            CurrentHandbookValue = Math.Round(currentValue, 2),
            BudgetCap = Math.Round(budgetCap, 2),
            TargetCount = targetCount,
            TargetHandbookValue = Math.Round(targetCount * unitHandbookPrice, 2),
            Reason = "SingleKnownPriceStackCanBeReducedWithoutChangingTemplateOrRewardStructure",
        };
    }

    private static ItemRewardStackPlan Block(double currentCount, double unitHandbookPrice, double budgetCap, string reason)
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
            BudgetCap = double.IsFinite(budgetCap) ? Math.Round(budgetCap, 2) : budgetCap,
            TargetCount = null,
            TargetHandbookValue = null,
            Reason = reason,
        };
    }
}
