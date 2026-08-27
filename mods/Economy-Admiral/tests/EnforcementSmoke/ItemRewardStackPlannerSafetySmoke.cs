using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class ItemRewardStackPlannerSafetySmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Economy Admiral item planner safety smoke: {message}");
        }

        var overflowCurrentValue = ItemRewardStackPlanner.Plan(
            currentCount: 1e308,
            unitHandbookPrice: 10,
            budgetCap: 100000);
        Require(!overflowCurrentValue.Eligible, "overflowing current stack value must fail closed");
        Require(overflowCurrentValue.Reason == "NonFiniteComputedValue", "overflowing current stack value must have explicit non-finite computed-value reason");

        var overflowBundleValue = ItemRewardStackPlanner.PlanWithinBundle(
            currentCount: 2,
            unitHandbookPrice: 8e307,
            immutableHandbookValue: 8e307,
            budgetCap: 1.7e308);
        Require(!overflowBundleValue.Eligible, "overflowing whole-bundle value must fail closed");
        Require(overflowBundleValue.Reason == "NonFiniteComputedValue", "overflowing bundle value must have explicit non-finite computed-value reason");

        var ordinaryPlan = ItemRewardStackPlanner.PlanWithinBundle(
            currentCount: 10,
            unitHandbookPrice: 25000,
            immutableHandbookValue: 30000,
            budgetCap: 100000);
        Require(ordinaryPlan.Eligible && ordinaryPlan.TargetCount == 2, "finite whole-bundle planning behavior must remain unchanged");
        Require(ordinaryPlan.TargetBundleHandbookValue == 80000, "finite target bundle value must remain within budget");
    }
}
