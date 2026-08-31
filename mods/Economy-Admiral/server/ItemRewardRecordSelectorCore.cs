namespace SPTEconomy;

public readonly record struct ItemRewardRecordCandidate(int RecordIndex, double StackCount);

public sealed record ItemRewardRecordSelection
{
    public required bool Eligible { get; init; }
    public int? SelectedRecordIndex { get; init; }
    public required string Reason { get; init; }
}

public static class ItemRewardRecordSelectorCore
{
    private const double IntegerTolerance = 0.0001d;

    public static ItemRewardRecordSelection Select(
        IReadOnlyList<ItemRewardRecordCandidate> candidates,
        bool allowUniqueDominant)
    {
        if (candidates.Count == 0)
            return Block("NoReducibleRewardRecord");

        if (!allowUniqueDominant)
        {
            return candidates.Count == 1
                ? Allow(candidates[0].RecordIndex, "SingleReducibleRewardRecordManualExact")
                : Block("AmbiguousMultipleReducibleRewardRecords");
        }

        var selected = candidates[0];
        var dominantCount = 1;
        for (var index = 1; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!double.IsFinite(candidate.StackCount) || candidate.StackCount <= 1)
                return Block("InvalidReducibleRecordStackCount");

            if (candidate.StackCount > selected.StackCount + IntegerTolerance)
            {
                selected = candidate;
                dominantCount = 1;
            }
            else if (Math.Abs(candidate.StackCount - selected.StackCount) <= IntegerTolerance)
            {
                dominantCount++;
            }
        }

        if (!double.IsFinite(selected.StackCount) || selected.StackCount <= 1)
            return Block("InvalidReducibleRecordStackCount");

        if (dominantCount != 1)
            return Block("AmbiguousMultipleReducibleRewardRecords");

        return Allow(
            selected.RecordIndex,
            candidates.Count == 1 ? "SingleReducibleRewardRecord" : "UniqueDominantReducibleRewardRecord");
    }

    private static ItemRewardRecordSelection Allow(int recordIndex, string reason) => new()
    {
        Eligible = true,
        SelectedRecordIndex = recordIndex,
        Reason = reason,
    };

    private static ItemRewardRecordSelection Block(string reason) => new()
    {
        Eligible = false,
        SelectedRecordIndex = null,
        Reason = reason,
    };
}
