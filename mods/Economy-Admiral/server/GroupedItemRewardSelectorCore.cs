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

    public static GroupedItemRewardSelection Select(IReadOnlyList<GroupedItemRewardEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0) return Block("EmptyItemRewardRecord");

        var firstTemplate = entries[0].TemplateId;
        if (string.IsNullOrWhiteSpace(firstTemplate)) return Block("MissingTemplateId");

        var candidateIndex = -1;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (string.IsNullOrWhiteSpace(entry.TemplateId)) return Block("MissingTemplateId");
            if (!string.Equals(entry.TemplateId, firstTemplate, StringComparison.Ordinal))
                return Block("MixedTemplatesInRewardRecord");
            if (!double.IsFinite(entry.Count) || entry.Count <= 0)
                return Block("InvalidStackCount");

            var rounded = Math.Round(entry.Count, 0);
            if (Math.Abs(entry.Count - rounded) > IntegerTolerance)
                return Block("NonIntegralStackCount");

            if (rounded <= 1 || !entry.HasKnownHandbookPrice) continue;
            if (candidateIndex >= 0) return Block("AmbiguousMultipleReducibleStacks");
            candidateIndex = index;
        }

        if (candidateIndex < 0) return Block("NoReducibleKnownPriceStack");
        return new GroupedItemRewardSelection
        {
            Eligible = true,
            SelectedIndex = candidateIndex,
            Reason = entries.Count == 1 ? "SingleReducibleStack" : "OneReducibleStackInSameTemplateGroupedReward",
        };
    }

    private static GroupedItemRewardSelection Block(string reason) => new()
    {
        Eligible = false,
        SelectedIndex = null,
        Reason = reason,
    };
}
