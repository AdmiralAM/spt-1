namespace SPTEconomy;

/// <summary>
/// Quantity rules for SPT Item quest rewards.
/// A reward record with one item uses Reward.Value == that stack count.
/// A reward record with multiple copies of the same _tpl uses Reward.Value == the sum of their stack counts.
/// This helper deliberately has no SPT model dependency so the arithmetic can be regression-tested independently.
/// </summary>
public static class ItemRewardQuantityCore
{
    private const double Tolerance = 0.001;

    public static bool TryReadSynchronizedTotal(
        double? rewardValue,
        IReadOnlyList<double?> stackCounts,
        out double total)
    {
        total = 0d;
        if (rewardValue is not { } value || !double.IsFinite(value) || stackCounts.Count == 0)
            return false;

        foreach (var nullableCount in stackCounts)
        {
            var count = nullableCount ?? 1d;
            if (!double.IsFinite(count) || count <= 0)
                return false;

            total += count;
            if (!double.IsFinite(total))
                return false;
        }

        return Math.Abs(value - total) <= Tolerance;
    }

    public static bool TryCalculateRewardValueAfterSelectedStackChange(
        double? rewardValue,
        IReadOnlyList<double?> stackCounts,
        int selectedIndex,
        double targetCount,
        out double targetRewardValue)
    {
        targetRewardValue = 0d;
        if (!TryReadSynchronizedTotal(rewardValue, stackCounts, out var currentTotal))
            return false;
        if (selectedIndex < 0 || selectedIndex >= stackCounts.Count)
            return false;
        if (!double.IsFinite(targetCount) || targetCount <= 0)
            return false;

        var roundedTarget = Math.Round(targetCount, 0);
        if (Math.Abs(targetCount - roundedTarget) > 0.000001)
            return false;

        var currentSelected = stackCounts[selectedIndex] ?? 1d;
        targetRewardValue = currentTotal - currentSelected + roundedTarget;
        return double.IsFinite(targetRewardValue) && targetRewardValue > 0;
    }
}
