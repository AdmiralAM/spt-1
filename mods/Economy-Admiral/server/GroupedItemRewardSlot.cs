namespace SPTEconomy;

public static class GroupedItemRewardSlot
{
    public static NumericRewardSlot Create(
        Func<double> selectedStackRead,
        Action<double> selectedStackWrite,
        Func<IReadOnlyList<double?>> allStackCountsRead,
        int selectedIndex,
        Func<double?> rewardValueRead,
        Action<double> rewardValueWrite,
        string label)
    {
        ArgumentNullException.ThrowIfNull(selectedStackRead);
        ArgumentNullException.ThrowIfNull(selectedStackWrite);
        ArgumentNullException.ThrowIfNull(allStackCountsRead);
        ArgumentNullException.ThrowIfNull(rewardValueRead);
        ArgumentNullException.ThrowIfNull(rewardValueWrite);
        if (selectedIndex < 0) throw new ArgumentOutOfRangeException(nameof(selectedIndex));
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("Grouped item reward label must not be empty.", nameof(label));

        double Read()
        {
            var selected = selectedStackRead();
            var stacks = allStackCountsRead();
            if (selectedIndex >= stacks.Count)
                throw new InvalidOperationException($"{label} selected item index {selectedIndex} is outside the grouped reward record.");
            if (!double.IsFinite(selected) || selected <= 0)
                throw new InvalidOperationException($"{label} selected item stack must be finite and > 0: {selected}.");
            var indexed = stacks[selectedIndex] ?? 1d;
            if (!double.IsFinite(indexed) || Math.Abs(indexed - selected) > 0.001)
                throw new InvalidOperationException($"{label} selected item stack drifted from grouped reward index {selectedIndex}: selected={selected}, indexed={indexed}.");
            if (!ItemRewardQuantityCore.TryReadSynchronizedTotal(rewardValueRead(), stacks, out _))
                throw new InvalidOperationException($"{label} Reward.Value does not equal the sum of grouped item StackObjectsCount values.");
            return selected;
        }

        void Write(double target)
        {
            Read();
            var stacks = allStackCountsRead();
            if (!ItemRewardQuantityCore.TryCalculateRewardValueAfterSelectedStackChange(
                    rewardValueRead(), stacks, selectedIndex, target, out var targetRewardValue))
                throw new InvalidOperationException($"{label} cannot represent grouped item reward target {target} without structural change.");

            selectedStackWrite(target);
            rewardValueWrite(targetRewardValue);
        }

        return new NumericRewardSlot(Read, Write);
    }
}
