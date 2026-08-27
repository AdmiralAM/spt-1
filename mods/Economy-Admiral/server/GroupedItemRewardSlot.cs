namespace SPTEconomy;

public static class GroupedItemRewardSlot
{
    public static NumericRewardSlot Create(
        Func<double> selectedStackRead,
        Action<double> selectedStackWrite,
        Func<IReadOnlyList<double?>> allStackCountsRead,
        Func<double?> rewardValueRead,
        Action<double> rewardValueWrite,
        string label)
    {
        ArgumentNullException.ThrowIfNull(selectedStackRead);
        ArgumentNullException.ThrowIfNull(selectedStackWrite);
        ArgumentNullException.ThrowIfNull(allStackCountsRead);
        ArgumentNullException.ThrowIfNull(rewardValueRead);
        ArgumentNullException.ThrowIfNull(rewardValueWrite);
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Grouped item reward label must not be empty.", nameof(label));

        double Read()
        {
            var selected = selectedStackRead();
            var stacks = allStackCountsRead();
            if (!double.IsFinite(selected) || selected <= 0)
                throw new InvalidOperationException($"{label} selected item stack must be finite and > 0: {selected}.");
            if (!ItemRewardQuantityCore.TryReadSynchronizedTotal(rewardValueRead(), stacks, out _))
                throw new InvalidOperationException($"{label} Reward.Value does not equal the sum of grouped item StackObjectsCount values.");
            return selected;
        }

        void Write(double target)
        {
            var currentSelected = Read();
            var stacks = allStackCountsRead();
            if (!ItemRewardQuantityCore.TryCalculateRewardValueAfterSelectedStackChange(
                    rewardValueRead(), stacks, FindSelectedIndex(stacks, currentSelected), target, out var targetRewardValue))
                throw new InvalidOperationException($"{label} cannot represent grouped item reward target {target} without structural change.");

            selectedStackWrite(target);
            rewardValueWrite(targetRewardValue);
        }

        return new NumericRewardSlot(Read, Write);
    }

    private static int FindSelectedIndex(IReadOnlyList<double?> stackCounts, double selected)
    {
        var match = -1;
        for (var index = 0; index < stackCounts.Count; index++)
        {
            var value = stackCounts[index] ?? 1d;
            if (Math.Abs(value - selected) > 0.001) continue;
            if (match >= 0)
                throw new InvalidOperationException("Grouped item reward selected stack is ambiguous by quantity; caller must provide an unambiguous item selector.");
            match = index;
        }
        if (match < 0)
            throw new InvalidOperationException("Grouped item reward selected stack is absent from the reward record.");
        return match;
    }
}
