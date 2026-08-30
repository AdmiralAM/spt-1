namespace SPTEconomy;

public sealed record GroupedItemRewardEntry(string TemplateId, double Count, bool HasKnownHandbookPrice);

public sealed record GroupedItemRewardSelection
{
    public required bool Eligible { get; init; }
    public int? SelectedIndex { get; init; }
    public required string Reason { get; init; }
}

public static class GroupedItemRewardSelectorCore
{
    private const double IntegerTolerance = 0.000001;

    public static GroupedItemRewardSelection Select(
        IReadOnlyList<GroupedItemRewardEntry> entries,
        bool requireKnownHandbookPrice = true)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0) return Block("EmptyItemRewardRecord");

        var candidateIndex = -1;
        var candidateCount = 0d;
        var multipleReducibleStacks = false;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (string.IsNullOrWhiteSpace(entry.TemplateId)) return Block("MissingTemplateId");
            if (!double.IsFinite(entry.Count) || entry.Count <= 0)
                return Block("InvalidStackCount");

            var rounded = Math.Round(entry.Count, 0);
            if (Math.Abs(entry.Count - rounded) > IntegerTolerance)
                return Block("NonIntegralStackCount");

            if (requireKnownHandbookPrice && !entry.HasKnownHandbookPrice)
                return Block("UnknownHandbookPrice");

            if (rounded <= 1) continue;
            if (candidateIndex < 0)
            {
                candidateIndex = index;
                candidateCount = rounded;
                continue;
            }

            multipleReducibleStacks = true;
            if (!requireKnownHandbookPrice)
                return Block("AmbiguousMultipleReducibleStacks");

            if (Math.Abs(rounded - candidateCount) <= IntegerTolerance)
                return Block("AmbiguousMultipleReducibleStacks");

            if (rounded > candidateCount)
            {
                candidateIndex = index;
                candidateCount = rounded;
            }
        }

        if (candidateIndex < 0)
            return Block(requireKnownHandbookPrice ? "NoReducibleKnownPriceStack" : "NoReducibleStack");

        return new GroupedItemRewardSelection
        {
            Eligible = true,
            SelectedIndex = candidateIndex,
            Reason = multipleReducibleStacks
                ? "UniqueDominantReducibleStackInGroupedReward"
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
