namespace SPTEconomy;

public enum RenewableSupplyBound
{
    Unknown,
    Bounded,
    Unbounded,
}

public sealed record RenewableSupplyCapacityEvidence
{
    public required string ItemTemplateId { get; init; }
    public required string SourceId { get; init; }
    public required AcquisitionChannel Channel { get; init; }
    public required RenewableSupplyBound SupplyBound { get; init; }
    public int? MaxUnitsPerReset { get; init; }
    public int? MaxAcquisitionsPerReset { get; init; }
}

public sealed record ItemBoundedSupplyEvidence
{
    public required string ItemTemplateId { get; init; }
    public required int RenewableSourceCount { get; init; }
    public required int KnownBoundedRenewableSourceCount { get; init; }
    public required int KnownUnboundedRenewableSourceCount { get; init; }
    public required int UnknownCapacityRenewableSourceCount { get; init; }
    public required double CapacityEvidenceCoverage { get; init; }
    public required bool HasCompleteCapacityEvidence { get; init; }
    public required bool HasKnownUnboundedRenewablePath { get; init; }
    public required bool HasOnlyKnownBoundedRenewablePaths { get; init; }
    public int? TotalKnownMaxUnitsPerReset { get; init; }
    public int? TotalKnownMaxAcquisitionsPerReset { get; init; }
}

public static class BoundedSupplyEvidenceAnalyzer
{
    public static IReadOnlyList<ItemBoundedSupplyEvidence> Analyze(
        IEnumerable<AcquisitionSourceEvidence> sources,
        IEnumerable<RenewableSupplyCapacityEvidence> capacities
    )
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(capacities);

        var normalizedSources = sources
            .Select(ValidateSource)
            .GroupBy(source => (source.ItemTemplateId, source.SourceId, source.Channel))
            .Select(ResolveSourceDuplicate)
            .ToList();

        var renewableSources = normalizedSources
            .Where(source => source.Renewable)
            .ToDictionary(
                source => (source.ItemTemplateId, source.SourceId, source.Channel),
                source => source
            );

        var normalizedCapacities = capacities
            .Select(ValidateCapacity)
            .GroupBy(capacity => (capacity.ItemTemplateId, capacity.SourceId, capacity.Channel))
            .Select(ResolveCapacityDuplicate)
            .ToDictionary(
                capacity => (capacity.ItemTemplateId, capacity.SourceId, capacity.Channel),
                capacity => capacity
            );

        foreach (var key in normalizedCapacities.Keys)
        {
            if (!renewableSources.ContainsKey(key))
            {
                throw new InvalidOperationException(
                    $"Economy Admiral bounded supply: capacity evidence references a missing or non-renewable source '{key.SourceId}' for item '{key.ItemTemplateId}'."
                );
            }
        }

        return renewableSources.Values
            .GroupBy(source => source.ItemTemplateId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => BuildItemEvidence(group, normalizedCapacities))
            .ToList();
    }

    private static AcquisitionSourceEvidence ValidateSource(AcquisitionSourceEvidence source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrWhiteSpace(source.ItemTemplateId) || string.IsNullOrWhiteSpace(source.SourceId))
        {
            throw new InvalidOperationException("Economy Admiral bounded supply: source identity must not be empty.");
        }

        return source with
        {
            ItemTemplateId = source.ItemTemplateId.Trim(),
            SourceId = source.SourceId.Trim(),
        };
    }

    private static RenewableSupplyCapacityEvidence ValidateCapacity(RenewableSupplyCapacityEvidence capacity)
    {
        ArgumentNullException.ThrowIfNull(capacity);
        if (string.IsNullOrWhiteSpace(capacity.ItemTemplateId) || string.IsNullOrWhiteSpace(capacity.SourceId))
        {
            throw new InvalidOperationException("Economy Admiral bounded supply: capacity identity must not be empty.");
        }

        if (capacity.MaxUnitsPerReset is < 1 || capacity.MaxAcquisitionsPerReset is < 1)
        {
            throw new InvalidOperationException("Economy Admiral bounded supply: positive reset limits are required when supplied.");
        }

        if (capacity.SupplyBound == RenewableSupplyBound.Bounded
            && capacity.MaxUnitsPerReset is null
            && capacity.MaxAcquisitionsPerReset is null)
        {
            throw new InvalidOperationException("Economy Admiral bounded supply: bounded evidence requires at least one explicit reset limit.");
        }

        if (capacity.SupplyBound == RenewableSupplyBound.Unbounded
            && (capacity.MaxUnitsPerReset is not null || capacity.MaxAcquisitionsPerReset is not null))
        {
            throw new InvalidOperationException("Economy Admiral bounded supply: unbounded evidence cannot carry finite reset limits.");
        }

        if (capacity.SupplyBound == RenewableSupplyBound.Unknown
            && (capacity.MaxUnitsPerReset is not null || capacity.MaxAcquisitionsPerReset is not null))
        {
            throw new InvalidOperationException("Economy Admiral bounded supply: unknown evidence cannot carry asserted reset limits.");
        }

        return capacity with
        {
            ItemTemplateId = capacity.ItemTemplateId.Trim(),
            SourceId = capacity.SourceId.Trim(),
        };
    }

    private static AcquisitionSourceEvidence ResolveSourceDuplicate(
        IGrouping<(string ItemTemplateId, string SourceId, AcquisitionChannel Channel), AcquisitionSourceEvidence> group
    )
    {
        var candidates = group.ToList();
        var first = candidates[0];
        if (candidates.Any(candidate => candidate.Renewable != first.Renewable))
        {
            throw new InvalidOperationException(
                $"Economy Admiral bounded supply: conflicting renewable state for source '{first.SourceId}' and item '{first.ItemTemplateId}'."
            );
        }
        return first;
    }

    private static RenewableSupplyCapacityEvidence ResolveCapacityDuplicate(
        IGrouping<(string ItemTemplateId, string SourceId, AcquisitionChannel Channel), RenewableSupplyCapacityEvidence> group
    )
    {
        var candidates = group.ToList();
        var first = candidates[0];
        if (candidates.Any(candidate =>
            candidate.SupplyBound != first.SupplyBound
            || candidate.MaxUnitsPerReset != first.MaxUnitsPerReset
            || candidate.MaxAcquisitionsPerReset != first.MaxAcquisitionsPerReset))
        {
            throw new InvalidOperationException(
                $"Economy Admiral bounded supply: conflicting capacity evidence for source '{first.SourceId}' and item '{first.ItemTemplateId}'."
            );
        }
        return first;
    }

    private static ItemBoundedSupplyEvidence BuildItemEvidence(
        IGrouping<string, AcquisitionSourceEvidence> group,
        IReadOnlyDictionary<(string ItemTemplateId, string SourceId, AcquisitionChannel Channel), RenewableSupplyCapacityEvidence> capacities
    )
    {
        var sources = group.ToList();
        var evidence = sources
            .Select(source => capacities.GetValueOrDefault((source.ItemTemplateId, source.SourceId, source.Channel)))
            .ToList();

        var bounded = evidence.Count(capacity => capacity?.SupplyBound == RenewableSupplyBound.Bounded);
        var unbounded = evidence.Count(capacity => capacity?.SupplyBound == RenewableSupplyBound.Unbounded);
        var unknown = evidence.Count - bounded - unbounded;
        var known = bounded + unbounded;
        var complete = evidence.Count > 0 && unknown == 0;
        var boundedOnly = complete && bounded > 0 && unbounded == 0;

        var boundedEvidence = evidence
            .Where(capacity => capacity?.SupplyBound == RenewableSupplyBound.Bounded)
            .Select(capacity => capacity!)
            .ToList();

        int? totalUnits = boundedOnly && boundedEvidence.All(capacity => capacity.MaxUnitsPerReset.HasValue)
            ? boundedEvidence.Sum(capacity => capacity.MaxUnitsPerReset!.Value)
            : null;
        int? totalAcquisitions = boundedOnly && boundedEvidence.All(capacity => capacity.MaxAcquisitionsPerReset.HasValue)
            ? boundedEvidence.Sum(capacity => capacity.MaxAcquisitionsPerReset!.Value)
            : null;

        return new ItemBoundedSupplyEvidence
        {
            ItemTemplateId = group.Key,
            RenewableSourceCount = sources.Count,
            KnownBoundedRenewableSourceCount = bounded,
            KnownUnboundedRenewableSourceCount = unbounded,
            UnknownCapacityRenewableSourceCount = unknown,
            CapacityEvidenceCoverage = sources.Count == 0 ? 0 : Math.Round((double)known / sources.Count, 6),
            HasCompleteCapacityEvidence = complete,
            HasKnownUnboundedRenewablePath = unbounded > 0,
            HasOnlyKnownBoundedRenewablePaths = boundedOnly,
            TotalKnownMaxUnitsPerReset = totalUnits,
            TotalKnownMaxAcquisitionsPerReset = totalAcquisitions,
        };
    }
}
