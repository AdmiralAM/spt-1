namespace SPTEconomy;

public sealed record EconomyItemHealthObservation
{
    public required string ItemTemplateId { get; init; }
    public required bool HasRenewablePath { get; init; }
    public required bool SingleRenewableSourceRisk { get; init; }
    public required double ChannelConcentrationHhi { get; init; }
    public required double EffectiveChannelCount { get; init; }
    public int? EarliestProgressionLevel { get; init; }
    public required bool HasCompleteProgressionEvidence { get; init; }
    public required double CapacityEvidenceCoverage { get; init; }
    public required bool HasCompleteCapacityEvidence { get; init; }
    public required bool EffectiveAcquisitionKnown { get; init; }
    public double? EffectiveAcquisitionCost { get; init; }
    public required IReadOnlyList<string> ProvenanceClasses { get; init; }
    public required bool HasUnknownObservationBoundary { get; init; }
}

public sealed record EconomyHealthRuntimeReport
{
    public int SchemaVersion { get; init; } = 1;
    public required int SourcePressureSchemaVersion { get; init; }
    public required bool CompositeScoreSelected { get; init; }
    public required bool MutationAuthorized { get; init; }
    public required int ItemCount { get; init; }
    public required int ItemsWithRenewablePath { get; init; }
    public required int SingleRenewableSourceRiskCount { get; init; }
    public required int IncompleteProgressionEvidenceCount { get; init; }
    public required int IncompleteCapacityEvidenceCount { get; init; }
    public required int UnknownEffectiveAcquisitionCount { get; init; }
    public required IReadOnlyList<ChannelObservationCoverage> ChannelCoverage { get; init; }
    public required IReadOnlyList<EconomyItemHealthObservation> Items { get; init; }
}

public static class EconomyHealthRuntimeReportBuilder
{
    public static EconomyHealthRuntimeReport Build(SourcePressureRuntimeReport sourcePressure)
    {
        ArgumentNullException.ThrowIfNull(sourcePressure);
        var capacity = sourcePressure.Capacity.ToDictionary(x => x.ItemTemplateId, StringComparer.Ordinal);
        var acquisition = sourcePressure.AcquisitionGraph.Items.ToDictionary(x => x.ItemTemplateId, StringComparer.Ordinal);
        var globallyUnknown = sourcePressure.ChannelCoverage.Any(x => x.State.StartsWith("Unknown", StringComparison.Ordinal));

        var items = sourcePressure.Items.OrderBy(x => x.ItemTemplateId, StringComparer.Ordinal).Select(item =>
        {
            capacity.TryGetValue(item.ItemTemplateId, out var supply);
            acquisition.TryGetValue(item.ItemTemplateId, out var reference);
            return new EconomyItemHealthObservation
            {
                ItemTemplateId = item.ItemTemplateId,
                HasRenewablePath = item.HasRenewablePath,
                SingleRenewableSourceRisk = item.SingleRenewableSourceRisk,
                ChannelConcentrationHhi = item.ChannelConcentrationHhi,
                EffectiveChannelCount = item.EffectiveChannelCount,
                EarliestProgressionLevel = item.EarliestProgressionLevel,
                HasCompleteProgressionEvidence = item.HasCompleteProgressionEvidence,
                CapacityEvidenceCoverage = supply?.CapacityEvidenceCoverage ?? 0d,
                HasCompleteCapacityEvidence = supply?.HasCompleteCapacityEvidence ?? !item.HasRenewablePath,
                EffectiveAcquisitionKnown = reference?.Known == true,
                EffectiveAcquisitionCost = reference?.Cost,
                ProvenanceClasses = item.ProvenanceClasses.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
                HasUnknownObservationBoundary = globallyUnknown || !item.HasCompleteProgressionEvidence || (item.HasRenewablePath && supply?.HasCompleteCapacityEvidence != true) || reference?.Known != true,
            };
        }).ToArray();

        return new EconomyHealthRuntimeReport
        {
            SourcePressureSchemaVersion = sourcePressure.SchemaVersion,
            CompositeScoreSelected = false,
            MutationAuthorized = false,
            ItemCount = items.Length,
            ItemsWithRenewablePath = items.Count(x => x.HasRenewablePath),
            SingleRenewableSourceRiskCount = items.Count(x => x.SingleRenewableSourceRisk),
            IncompleteProgressionEvidenceCount = items.Count(x => !x.HasCompleteProgressionEvidence),
            IncompleteCapacityEvidenceCount = items.Count(x => x.HasRenewablePath && !x.HasCompleteCapacityEvidence),
            UnknownEffectiveAcquisitionCount = items.Count(x => !x.EffectiveAcquisitionKnown),
            ChannelCoverage = sourcePressure.ChannelCoverage.OrderBy(x => x.Channel).ToArray(),
            Items = items,
        };
    }
}
