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

        var selection = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("same-tpl", 3d, true),
            new GroupedItemRewardEntry("same-tpl", 1d, true),
            new GroupedItemRewardEntry("same-tpl", 1d, true),
        ]);
        Require(selection.Eligible && selection.SelectedIndex == 0 && selection.Reason == "OneReducibleStackInGroupedReward",
            "same-template grouped reward with one reducible stack must select that exact stack");

        var mixedTpl = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, true),
            new GroupedItemRewardEntry("tpl-b", 1d, true),
            new GroupedItemRewardEntry("tpl-c", 1d, true),
        ]);
        Require(mixedTpl.Eligible && mixedTpl.SelectedIndex == 0 && mixedTpl.Reason == "OneReducibleStackInGroupedReward",
            "mixed-template grouped reward with exactly one reducible known-price stack must select only that stack");

        var ambiguous = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, true),
            new GroupedItemRewardEntry("tpl-b", 2d, true),
        ]);
        Require(!ambiguous.Eligible && ambiguous.Reason == "AmbiguousMultipleReducibleStacks",
            "multiple reducible stacks must fail closed across different templates");

        var unknownSelectedPrice = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, false),
            new GroupedItemRewardEntry("tpl-b", 1d, true),
        ]);
        Require(!unknownSelectedPrice.Eligible && unknownSelectedPrice.Reason == "UnknownHandbookPrice",
            "automatic grouped selection must reject an unknown-price mutable stack");

        var unknownSiblingPrice = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, true),
            new GroupedItemRewardEntry("tpl-b", 1d, false),
        ]);
        Require(!unknownSiblingPrice.Eligible && unknownSiblingPrice.Reason == "UnknownHandbookPrice",
            "automatic grouped selection must reject unknown immutable sibling pricing");

        var hiddenAmbiguity = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, false),
            new GroupedItemRewardEntry("tpl-b", 2d, true),
        ]);
        Require(!hiddenAmbiguity.Eligible,
            "unknown price must never hide a second reducible stack and permit mutation");

        var invalidZero = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, true),
            new GroupedItemRewardEntry("tpl-b", 0d, true),
        ]);
        Require(!invalidZero.Eligible && invalidZero.Reason == "InvalidStackCount", "zero stack count must fail closed");

        var invalidFraction = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, true),
            new GroupedItemRewardEntry("tpl-b", 1.5d, true),
        ]);
        Require(!invalidFraction.Eligible && invalidFraction.Reason == "NonIntegralStackCount", "fractional stack count must fail closed");

        var manualUnknownPrice = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, false),
            new GroupedItemRewardEntry("tpl-b", 1d, false),
        ], requireKnownHandbookPrice: false);
        Require(manualUnknownPrice.Eligible && manualUnknownPrice.SelectedIndex == 0 && manualUnknownPrice.Reason == "OneReducibleStackInGroupedRewardManualExact",
            "manual exact grouped selection may ignore price while preserving structural gates");

        var manualAmbiguous = GroupedItemRewardSelectorCore.Select([
            new GroupedItemRewardEntry("tpl-a", 3d, false),
            new GroupedItemRewardEntry("tpl-b", 2d, false),
        ], requireKnownHandbookPrice: false);
        Require(!manualAmbiguous.Eligible && manualAmbiguous.Reason == "AmbiguousMultipleReducibleStacks",
            "manual exact grouped selection must not bypass unique-stack safety");

        double?[] stacks = [3d, 1d, 1d];
        Require(ItemRewardQuantityCore.TryReadSynchronizedTotal(5d, stacks, out var total) && Math.Abs(total - 5d) < 0.001,
            "Reward.Value must equal the sum of StackObjectsCount values");
        Require(!ItemRewardQuantityCore.TryReadSynchronizedTotal(3d, stacks, out _),
            "inconsistent aggregate Reward.Value must fail closed");
        Require(ItemRewardQuantityCore.TryCalculateRewardValueAfterSelectedStackChange(5d, stacks, 0, 1d, out var targetRewardValue)
                && Math.Abs(targetRewardValue - 3d) < 0.001,
            "selected 3->1 mutation must update aggregate Reward.Value by the same delta");
        Require(!ItemRewardQuantityCore.TryCalculateRewardValueAfterSelectedStackChange(5d, stacks, 0, 0d, out _),
            "mutation must not delete the selected item");

        var selected = 3d;
        var siblingA = 1d;
        var siblingB = 1d;
        var rewardValue = 5d;
        IReadOnlyList<double?> ReadStacks() => [selected, siblingA, siblingB];
        var groupedSlot = GroupedItemRewardSlot.Create(
            () => selected,
            value => selected = value,
            ReadStacks,
            0,
            () => rewardValue,
            value => rewardValue = value,
            "mixed-template-grouped-success-reward");

        var commit = NumericRewardTransactionCore.Execute([
            new NumericRewardTransactionRequest
            {
                QuestId = "mixed-grouped-item-reward",
                Dimension = "ItemRewardStackCount",
                ExpectedBefore = 3d,
                Target = 1d,
                Slots = [groupedSlot],
            },
        ]);
        Require(commit.Committed && !commit.RolledBack, "grouped item transaction must commit");
        Require(selected == 1d && siblingA == 1d && siblingB == 1d, "transaction must mutate only selected stack");
        Require(rewardValue == 3d, "transaction must atomically update aggregate Reward.Value");
        Require(!NumericRewardTransactionCore.NeedsMutation(selected, 1d, false), "second pass must be idempotent");

        selected = 3d;
        siblingA = 1d;
        siblingB = 1d;
        rewardValue = 5d;
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
                Slots = [new NumericRewardSlot(() => failingValue, value =>
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
        Require(selected == 3d && siblingA == 1d && siblingB == 1d, "rollback must restore selected stack and siblings");
        Require(rewardValue == 5d && failingValue == 10d, "whole-batch rollback must restore aggregate and later numeric slot");
    }
}
