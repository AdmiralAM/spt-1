namespace SPTEconomy;

public enum AcquisitionChannel
{
    TraderPurchase,
    TraderBarter,
    QuestReward,
    RepeatableQuestReward,
    Craft,
    Flea,
    WorldLoot,
    Other,
}

public sealed record AcquisitionSourceEvidence
{
    public required string ItemTemplateId { get; init; }
    public required string SourceId { get; init; }
    public required AcquisitionChannel Channel { get; init; }
    public required bool Renewable { get; init; }
    public int? EarliestProgressionLevel { get; init; }
    public required string ProvenanceClass { get; init; }
}

public sealed record ChannelSourceSummary
{
    public required AcquisitionChannel Channel { get; init; }
    public required int SourceCount { get; init; }
    public required int RenewableSourceCount { get; init; }
}

public sealed record ItemSourcePressureEvidence
{
    public required string ItemTemplateId { get; init; }
    public required int SourceCount { get; init; }
    public required int ChannelCount { get; init; }
    public required int RenewableSourceCount { get; init; }
    public required int OneTimeSourceCount { get; init; }
    public required double RenewableSourceShare { get; init; }
    public required bool HasRenewablePath { get; init; }
    public int? EarliestProgressionLevel { get; init; }
    public required bool SingleSourceDominated { get; init; }
    public required List<ChannelSourceSummary> Channels { get; init; }
    public required List<string> ProvenanceClasses { get; init; }
}

public static class SourcePressureEvidenceAnalyzer
{
    public static IReadOnlyList<ItemSourcePressureEvidence> Analyze(IEnumerable<AcquisitionSourceEvidence> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var normalized = sources
            .Select(ValidateAndNormalize)
            .DistinctBy(source => (source.ItemTemplateId, source.SourceId, source.Channel))
            .OrderBy(source => source.ItemTemplateId, StringComparer.Ordinal)
            .ThenBy(source => source.Channel)
            .ThenBy(source => source.SourceId, StringComparer.Ordinal)
            .ToList();

        return normalized
            .GroupBy(source => source.ItemTemplateId, StringComparer.Ordinal)
            .Select(BuildItemEvidence)
            .OrderBy(item => item.ItemTemplateId, StringComparer.Ordinal)
            .ToList();
    }

    private static AcquisitionSourceEvidence ValidateAndNormalize(AcquisitionSourceEvidence source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(source.ItemTemplateId))
        {
            throw new InvalidOperationException("Economy Admiral source pressure: item template id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(source.SourceId))
        {
            throw new InvalidOperationException($"Economy Admiral source pressure: source id must not be empty for item '{source.ItemTemplateId}'.");
        }

        if (string.IsNullOrWhiteSpace(source.ProvenanceClass))
        {
            throw new InvalidOperationException($"Economy Admiral source pressure: provenance class must not be empty for item '{source.ItemTemplateId}'.");
        }

        if (source.EarliestProgressionLevel is < 1)
        {
            throw new InvalidOperationException($"Economy Admiral source pressure: progression level must be >= 1 for source '{source.SourceId}'.");
        }

        return source with
        {
            ItemTemplateId = source.ItemTemplateId.Trim(),
            SourceId = source.SourceId.Trim(),
            ProvenanceClass = source.ProvenanceClass.Trim(),
        };
    }

    private static ItemSourcePressureEvidence BuildItemEvidence(IGrouping<string, AcquisitionSourceEvidence> group)
    {
        var sources = group.ToList();
        var renewableCount = sources.Count(source => source.Renewable);
        var sourceCount = sources.Count;
        var knownLevels = sources
            .Where(source => source.EarliestProgressionLevel.HasValue)
            .Select(source => source.EarliestProgressionLevel!.Value)
            .ToList();

        var channels = sources
            .GroupBy(source => source.Channel)
            .OrderBy(channel => channel.Key)
            .Select(channel => new ChannelSourceSummary
            {
                Channel = channel.Key,
                SourceCount = channel.Count(),
                RenewableSourceCount = channel.Count(source => source.Renewable),
            })
            .ToList();

        return new ItemSourcePressureEvidence
        {
            ItemTemplateId = group.Key,
            SourceCount = sourceCount,
            ChannelCount = channels.Count,
            RenewableSourceCount = renewableCount,
            OneTimeSourceCount = sourceCount - renewableCount,
            RenewableSourceShare = sourceCount == 0 ? 0 : Math.Round((double)renewableCount / sourceCount, 6),
            HasRenewablePath = renewableCount > 0,
            EarliestProgressionLevel = knownLevels.Count == 0 ? null : knownLevels.Min(),
            SingleSourceDominated = sourceCount == 1,
            Channels = channels,
            ProvenanceClasses = sources
                .Select(source => source.ProvenanceClass)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
        };
    }
}
