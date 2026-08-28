namespace SPTEconomy;

public sealed record ReplacementCandidateFact
{
    public required string SubjectItemId { get; init; }
    public required string CandidateItemId { get; init; }
    public required string Relationship { get; init; }
}

public sealed record ReplacementComparabilityEvidence
{
    public required string SubjectItemId { get; init; }
    public required string CandidateItemId { get; init; }
    public required string Relationship { get; init; }
    public required bool SubjectHasRenewablePath { get; init; }
    public required bool CandidateHasRenewablePath { get; init; }
    public required int SubjectRenewableChannelCount { get; init; }
    public required int CandidateRenewableChannelCount { get; init; }
    public int? SubjectEarliestRenewableProgressionLevel { get; init; }
    public int? CandidateEarliestRenewableProgressionLevel { get; init; }
    public int? RenewableProgressionLevelDelta { get; init; }
    public required double SubjectProgressionEvidenceCoverage { get; init; }
    public required double CandidateProgressionEvidenceCoverage { get; init; }
    public required bool SubjectHasCompleteProgressionEvidence { get; init; }
    public required bool CandidateHasCompleteProgressionEvidence { get; init; }
    public required IReadOnlyList<AcquisitionChannel> ChannelIntersection { get; init; }
    public required IReadOnlyList<AcquisitionChannel> SubjectOnlyChannels { get; init; }
    public required IReadOnlyList<AcquisitionChannel> CandidateOnlyChannels { get; init; }
    public required double ChannelJaccardOverlap { get; init; }
}

public static class ReplacementComparabilityEvidenceAnalyzer
{
    public static IReadOnlyList<ReplacementComparabilityEvidence> Analyze(
        IEnumerable<ItemSourcePressureEvidence> items,
        IEnumerable<ReplacementCandidateFact> candidates)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(candidates);

        var byId = items.ToDictionary(x => x.ItemTemplateId, StringComparer.Ordinal);
        var normalized = candidates.Select(Validate)
            .GroupBy(x => (x.SubjectItemId, x.CandidateItemId), EqualityComparer<(string,string)>.Default)
            .Select(ResolveRelationship)
            .OrderBy(x => x.SubjectItemId, StringComparer.Ordinal)
            .ThenBy(x => x.CandidateItemId, StringComparer.Ordinal)
            .ThenBy(x => x.Relationship, StringComparer.Ordinal)
            .ToArray();

        return normalized.Select(f => Build(byId, f)).ToArray();
    }

    private static ReplacementCandidateFact Validate(ReplacementCandidateFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        var subject = fact.SubjectItemId?.Trim();
        var candidate = fact.CandidateItemId?.Trim();
        var relationship = fact.Relationship?.Trim();
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(relationship))
            throw new InvalidOperationException("Economy Admiral replacement comparability: subject, candidate and relationship are required.");
        if (string.Equals(subject, candidate, StringComparison.Ordinal))
            throw new InvalidOperationException("Economy Admiral replacement comparability: subject and candidate must differ.");
        return fact with { SubjectItemId = subject, CandidateItemId = candidate, Relationship = relationship };
    }

    private static ReplacementCandidateFact ResolveRelationship(IGrouping<(string SubjectItemId, string CandidateItemId), ReplacementCandidateFact> group)
    {
        var distinct = group.Select(x => x.Relationship).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (distinct.Length != 1)
            throw new InvalidOperationException($"Economy Admiral replacement comparability: conflicting relationships for '{group.Key.SubjectItemId}' -> '{group.Key.CandidateItemId}'.");
        return group.First();
    }

    private static ReplacementComparabilityEvidence Build(IReadOnlyDictionary<string, ItemSourcePressureEvidence> byId, ReplacementCandidateFact fact)
    {
        if (!byId.TryGetValue(fact.SubjectItemId, out var subject))
            throw new InvalidOperationException($"Economy Admiral replacement comparability: missing subject '{fact.SubjectItemId}'.");
        if (!byId.TryGetValue(fact.CandidateItemId, out var candidate))
            throw new InvalidOperationException($"Economy Admiral replacement comparability: missing candidate '{fact.CandidateItemId}'.");

        var subjectChannels = subject.Channels.Where(x => x.RenewableSourceCount > 0).Select(x => x.Channel).ToHashSet();
        var candidateChannels = candidate.Channels.Where(x => x.RenewableSourceCount > 0).Select(x => x.Channel).ToHashSet();
        var intersection = subjectChannels.Intersect(candidateChannels).OrderBy(x => x).ToArray();
        var unionCount = subjectChannels.Union(candidateChannels).Count();
        var delta = subject.EarliestRenewableProgressionLevel.HasValue && candidate.EarliestRenewableProgressionLevel.HasValue
            ? candidate.EarliestRenewableProgressionLevel.Value - subject.EarliestRenewableProgressionLevel.Value
            : null;

        return new ReplacementComparabilityEvidence
        {
            SubjectItemId = subject.ItemTemplateId,
            CandidateItemId = candidate.ItemTemplateId,
            Relationship = fact.Relationship,
            SubjectHasRenewablePath = subject.HasRenewablePath,
            CandidateHasRenewablePath = candidate.HasRenewablePath,
            SubjectRenewableChannelCount = subject.RenewableChannelCount,
            CandidateRenewableChannelCount = candidate.RenewableChannelCount,
            SubjectEarliestRenewableProgressionLevel = subject.EarliestRenewableProgressionLevel,
            CandidateEarliestRenewableProgressionLevel = candidate.EarliestRenewableProgressionLevel,
            RenewableProgressionLevelDelta = delta,
            SubjectProgressionEvidenceCoverage = subject.ProgressionEvidenceCoverage,
            CandidateProgressionEvidenceCoverage = candidate.ProgressionEvidenceCoverage,
            SubjectHasCompleteProgressionEvidence = subject.HasCompleteProgressionEvidence,
            CandidateHasCompleteProgressionEvidence = candidate.HasCompleteProgressionEvidence,
            ChannelIntersection = intersection,
            SubjectOnlyChannels = subjectChannels.Except(candidateChannels).OrderBy(x => x).ToArray(),
            CandidateOnlyChannels = candidateChannels.Except(subjectChannels).OrderBy(x => x).ToArray(),
            ChannelJaccardOverlap = unionCount == 0 ? 0d : Math.Round((double)intersection.Length / unionCount, 6),
        };
    }
}
