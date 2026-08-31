namespace SPTEconomy;

public readonly record struct ItemRewardRecordCandidate(
    int RecordIndex,
    double StackCount,
    double? EconomicValue = null);

public sealed record ItemRewardRecordSelection
{
    public required bool Eligible { get; init; }
    public int? SelectedRecordIndex { get; init; }
    public required string Reason { get; init; }
}

public static class ItemRewardRecordSelectorCore
{
    private const double ValueTolerance = 0.01d;

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
        if (!ValidAutomaticCandidate(selected))
            return Block("InvalidReducibleRecordEconomicValue");

        var selectedValue = selected.EconomicValue!.Value;
        var dominantCount = 1;
        for (var index = 1; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!ValidAutomaticCandidate(candidate))
                return Block("InvalidReducibleRecordEconomicValue");

            var candidateValue = candidate.EconomicValue!.Value;
            if (candidateValue > selectedValue + ValueTolerance)
            {
                selected = candidate;
                selectedValue = candidateValue;
                dominantCount = 1;
            }
            else if (Math.Abs(candidateValue - selectedValue) <= ValueTolerance)
            {
                dominantCount++;
            }
        }

        if (dominantCount != 1)
            return Block("AmbiguousMultipleReducibleRewardRecords");

        return Allow(
            selected.RecordIndex,
            candidates.Count == 1 ? "SingleReducibleRewardRecord" : "UniqueDominantEconomicValueRewardRecord");
    }

    private static bool ValidAutomaticCandidate(ItemRewardRecordCandidate candidate) =>
        double.IsFinite(candidate.StackCount)
        && candidate.StackCount > 1
        && candidate.EconomicValue is { } value
        && double.IsFinite(value)
        && value > 0;

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
