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

        var selected = 3d;
        var siblingA = 2d;
        var siblingB = 1d;
        var rewardValue = 6d;
        IReadOnlyList<double?> ReadStacks() => [selected, siblingA, siblingB];
        var groupedSlot = GroupedItemRewardSlot.Create(
            selectedStackRead: () => selected,
            selectedStackWrite: value => selected = value,
            allStackCountsRead: ReadStacks,
            selectedIndex: 0,
            rewardValueRead: () => rewardValue,
            rewardValueWrite: value => rewardValue = value,
            label: "grouped-success-reward");

        var commit = NumericRewardTransactionCore.Execute([
            new NumericRewardTransactionRequest
            {
                QuestId = "grouped-item-reward",
                Dimension = "ItemRewardStackCount",
                ExpectedBefore = 3d,
                Target = 1d,
                Slots = [groupedSlot],
            },
        ]);
        Require(commit.Committed && !commit.RolledBack, "grouped item transaction must commit");
        Require(selected == 1d && siblingA == 2d && siblingB == 1d, "grouped item transaction must change only the selected stack");
        Require(rewardValue == 4d, "grouped item transaction must update Reward.Value by the selected-stack delta");
        Require(!NumericRewardTransactionCore.NeedsMutation(selected, 1d, false), "grouped item automatic second pass must be idempotent");

        selected = 3d;
        siblingA = 2d;
        siblingB = 1d;
        rewardValue = 6d;
        var failingValue = 10d;
        var failOnce = true;
        var rollback = NumericRewardTransactionCore.Execute([
            new NumericRewardTransactionRequest
            {
                QuestId = "grouped-item-first",
                Dimension = "ItemRewardStackCount",
                ExpectedBefore = 3d,
                Target = 1d,
                Slots = [groupedSlot],
            },
            new NumericRewardTransactionRequest
            {
                QuestId = "grouped-followup-failure",
                Dimension = "Experience",
                ExpectedBefore = 10d,
                Target = 5d,
                Slots = [new NumericRewardSlot(
                    () => failingValue,
                    value =>
                    {
                        if (failOnce)
                        {
                            failOnce = false;
                            throw new InvalidOperationException("synthetic grouped-batch failure");
                        }
                        failingValue = value;
                    })],
            },
        ]);
        Require(!rollback.Committed && rollback.RolledBack, "later batch failure must roll back grouped item mutation");
        Require(selected == 3d && siblingA == 2d && siblingB == 1d, "grouped rollback must restore selected stack and leave siblings unchanged");
        Require(rewardValue == 6d, "grouped rollback must restore aggregate Reward.Value");
    }
}
