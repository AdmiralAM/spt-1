namespace SPTEconomy;

public sealed record GroupedItemRewardEntry(
    string TemplateId,
    double Count,
    bool HasKnownHandbookPrice,
    double? HandbookUnitPrice = null);

public sealed record GroupedItemRewardSelection
{
    public required bool Eligible { get; init; }
    public int? SelectedIndex { get; init; }
    public required string Reason { get; init; }
}

public static class GroupedItemRewardSelectorCore
{
    private const double IntegerTolerance = 0.000001;
    private const double ValueTolerance = 0.01;

    public static GroupedItemRewardSelection Select(
        IReadOnlyList<GroupedItemRewardEntry> entries,
        bool requireKnownHandbookPrice = true)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0) return Block("EmptyItemRewardRecord");

        var candidateIndex = -1;
        var candidateValue = 0d;
        var reducibleCount = 0;
        var dominantCount = 0;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (string.IsNullOrWhiteSpace(entry.TemplateId)) return Block("MissingTemplateId");
            if (!double.IsFinite(entry.Count) || entry.Count <= 0)
                return Block("InvalidStackCount");

            var rounded = Math.Round(entry.Count, 0);
            if (Math.Abs(entry.Count - rounded) > IntegerTolerance)
                return Block("NonIntegralStackCount");

            if (requireKnownHandbookPrice)
            {
                if (!entry.HasKnownHandbookPrice)
                    return Block("UnknownHandbookPrice");
                if (entry.HandbookUnitPrice is not { } unitPrice || !double.IsFinite(unitPrice) || unitPrice <= 0)
                    return Block("InvalidHandbookPrice");
            }

            if (rounded <= 1) continue;
            reducibleCount++;

            if (!requireKnownHandbookPrice && reducibleCount > 1)
                return Block("AmbiguousMultipleReducibleStacks");

            var economicValue = requireKnownHandbookPrice
                ? rounded * entry.HandbookUnitPrice!.Value
                : rounded;
            if (!double.IsFinite(economicValue) || economicValue <= 0)
                return Block("InvalidRewardEconomicValue");

            if (candidateIndex < 0 || economicValue > candidateValue + ValueTolerance)
            {
                candidateIndex = index;
                candidateValue = economicValue;
                dominantCount = 1;
            }
            else if (Math.Abs(economicValue - candidateValue) <= ValueTolerance)
            {
                dominantCount++;
            }
        }

        if (candidateIndex < 0)
            return Block(requireKnownHandbookPrice ? "NoReducibleKnownPriceStack" : "NoReducibleStack");
        if (requireKnownHandbookPrice && reducibleCount > 1 && dominantCount != 1)
            return Block("AmbiguousMultipleReducibleStacks");

        return new GroupedItemRewardSelection
        {
            Eligible = true,
            SelectedIndex = candidateIndex,
            Reason = reducibleCount > 1
                ? "UniqueDominantEconomicValueStackInGroupedReward"
                : entries.Count == 1
                    ? (requireKnownHandbookPrice ? "SingleReducibleStack" : "SingleReducibleStackManualExact")
                    : (requireKnownHandbookPrice ? "OneReducibleStackInGroupedReward" : "OneReducibleStackInGroupedRewardManualExact"),
        };
    }

    private static GroupedItemRewardSelection Block(string reason) => new()
    {
        Eligible = false,
        SelectedIndex = null,
        Reason = reason,
    };
}
