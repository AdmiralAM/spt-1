using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class GroupedItemRewardSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException($"Economy Admiral grouped item reward smoke: {message}");
        }

        double?[] stacks = [3d, 2d, 1d];
        Require(
            ItemRewardQuantityCore.TryReadSynchronizedTotal(6d, stacks, out var total) && Math.Abs(total - 6d) < 0.001,
            "Reward.Value must equal the sum of StackObjectsCount values for a same-template multi-item reward record");

        Require(
            !ItemRewardQuantityCore.TryReadSynchronizedTotal(3d, stacks, out _),
            "legacy single-stack equality must not accept a grouped reward whose Reward.Value represents the record total");

        Require(
            ItemRewardQuantityCore.TryCalculateRewardValueAfterSelectedStackChange(
                rewardValue: 6d,
                stackCounts: stacks,
                selectedIndex: 0,
                targetCount: 1d,
                out var targetRewardValue)
            && Math.Abs(targetRewardValue - 4d) < 0.001,
            "reducing one selected stack 3->1 must reduce Reward.Value by the same delta while siblings remain immutable");

        Require(
            !ItemRewardQuantityCore.TryCalculateRewardValueAfterSelectedStackChange(
                rewardValue: 6d,
                stackCounts: stacks,
                selectedIndex: 0,
                targetCount: 0d,
                out _),
            "grouped item mutation must not require structural deletion of the selected item");
    }
}
