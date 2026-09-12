namespace SPTEconomy;

public readonly record struct ItemRewardRecordCandidate(
    int RecordIndex,
    double StackCount,
    double? EconomicValue = null,
    double? ReducibleEconomicValue = null);

public sealed record ItemRewardRecordSelection
{
    public required bool Eligible { get; init; }
    public int? SelectedRecordIndex { get; init; }
    public required string Reason { get; init; }
}

public static class ItemRewardRecordSelectorCore
{
    private const double IntegerTolerance = 0.0001d;
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
        if (!IsValidAutomaticCandidate(selected))
            return Block("InvalidReducibleRecordEconomicValue");

        var dominantCount = 1;
        for (var index = 1; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (!IsValidAutomaticCandidate(candidate))
                return Block("InvalidReducibleRecordEconomicValue");

            var candidateValue = candidate.EconomicValue!.Value;
            var selectedValue = selected.EconomicValue!.Value;
            if (candidateValue > selectedValue + ValueTolerance)
            {
                selected = candidate;
                dominantCount = 1;
            }
            else if (Math.Abs(candidateValue - selectedValue) <= ValueTolerance)
            {
                var candidateReducibleValue = candidate.ReducibleEconomicValue!.Value;
                var selectedReducibleValue = selected.ReducibleEconomicValue!.Value;
                if (candidateReducibleValue > selectedReducibleValue + ValueTolerance)
                {
                    selected = candidate;
                    dominantCount = 1;
                }
                else if (Math.Abs(candidateReducibleValue - selectedReducibleValue) <= ValueTolerance)
                {
                    dominantCount++;
                }
            }
        }

        if (dominantCount != 1)
            return Block("AmbiguousMultipleReducibleRewardRecords");

        return Allow(
            selected.RecordIndex,
            candidates.Count == 1 ? "SingleReducibleRewardRecord" : "UniqueDominantReducibleRewardRecord");
    }

    private static bool IsValidAutomaticCandidate(ItemRewardRecordCandidate candidate)
    {
        if (!double.IsFinite(candidate.StackCount) || candidate.StackCount <= 1)
            return false;

        var rounded = Math.Round(candidate.StackCount, 0);
        if (Math.Abs(candidate.StackCount - rounded) > IntegerTolerance)
            return false;

        return candidate.EconomicValue is { } economicValue
            && double.IsFinite(economicValue)
            && economicValue > 0
            && candidate.ReducibleEconomicValue is { } reducibleEconomicValue
            && double.IsFinite(reducibleEconomicValue)
            && reducibleEconomicValue > 0;
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
