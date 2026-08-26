namespace SPTEconomy;

public sealed record ReplacementRelationshipEvidence
{
    public required string SubjectItemTemplateId { get; init; }
    public required string CandidateItemTemplateId { get; init; }
    public required string RelationshipClass { get; init; }
}

public sealed record ReplacementComparabilityEvidence
{
    public required string SubjectItemTemplateId { get; init; }
    public required string CandidateItemTemplateId { get; init; }
    public required string RelationshipClass { get; init; }
    public required bool SubjectHasRenewablePath { get; init; }
    public required bool CandidateHasRenewablePath { get; init; }
    public required int SubjectRenewableChannelCount { get; init; }
    public required int CandidateRenewableChannelCount { get; init; }
    public int? SubjectEarliestRenewableProgressionLevel { get; init; }
    public int? CandidateEarliestRenewableProgressionLevel { get; init; }
    public int? RenewableProgressionLevelDelta { get; init; }
    public required bool HasKnownRenewableProgressionComparison { get; init; }
    public required double SubjectProgressionEvidenceCoverage { get; init; }
    public required double CandidateProgressionEvidenceCoverage { get; init; }
    public required bool SubjectHasCompleteProgressionEvidence { get; init; }
    public required bool CandidateHasCompleteProgressionEvidence { get; init; }
    public required List<AcquisitionChannel> SharedChannels { get; init; }
    public required List<AcquisitionChannel> SubjectOnlyChannels { get; init; }
    public required List<AcquisitionChannel> CandidateOnlyChannels { get; init; }
    public double? ChannelJaccardOverlap { get; init; }
}

public static class ReplacementComparabilityAnalyzer
{
    public static IReadOnlyList<ReplacementComparabilityEvidence> Analyze(
        IEnumerable<ItemSourcePressureEvidence> itemEvidence,
        IEnumerable<ReplacementRelationshipEvidence> relationships
    )
    {
        ArgumentNullException.ThrowIfNull(itemEvidence);
        ArgumentNullException.ThrowIfNull(relationships);

        var items = itemEvidence
            .Select(ValidateItem)
            .GroupBy(item => item.ItemTemplateId, StringComparer.Ordinal)
            .Select(ResolveUniqueItem)
            .ToDictionary(item => item.ItemTemplateId, StringComparer.Ordinal);

        var normalizedRelationships = relationships
            .Select(ValidateAndNormalizeRelationship)
            .GroupBy(
                relationship => (relationship.SubjectItemTemplateId, relationship.CandidateItemTemplateId),
                StringTupleComparer.Ordinal
            )
            .Select(ResolveRelationship)
            .OrderBy(relationship => relationship.SubjectItemTemplateId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.CandidateItemTemplateId, StringComparer.Ordinal)
            .ThenBy(relationship => relationship.RelationshipClass, StringComparer.Ordinal)
            .ToList();

        var results = new List<ReplacementComparabilityEvidence>(normalizedRelationships.Count);
        foreach (var relationship in normalizedRelationships)
        {
            if (!items.TryGetValue(relationship.SubjectItemTemplateId, out var subject))
            {
                throw new InvalidOperationException(
                    $"Economy Admiral replacement evidence: subject item '{relationship.SubjectItemTemplateId}' is missing source-pressure evidence."
                );
            }

            if (!items.TryGetValue(relationship.CandidateItemTemplateId, out var candidate))
            {
                throw new InvalidOperationException(
                    $"Economy Admiral replacement evidence: candidate item '{relationship.CandidateItemTemplateId}' is missing source-pressure evidence."
                );
            }

            results.Add(BuildEvidence(subject, candidate, relationship));
        }

        return results;
    }

    private static ItemSourcePressureEvidence ValidateItem(ItemSourcePressureEvidence item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrWhiteSpace(item.ItemTemplateId))
        {
            throw new InvalidOperationException("Economy Admiral replacement evidence: item template id must not be empty.");
        }

        if (item.Channels is null)
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: channels must be present for item '{item.ItemTemplateId}'."
            );
        }

        if (!double.IsFinite(item.ProgressionEvidenceCoverage)
            || item.ProgressionEvidenceCoverage < 0
            || item.ProgressionEvidenceCoverage > 1)
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: progression coverage must be within [0,1] for item '{item.ItemTemplateId}'."
            );
        }

        if (item.RenewableChannelCount < 0)
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: renewable channel count must be non-negative for item '{item.ItemTemplateId}'."
            );
        }

        if (item.EarliestRenewableProgressionLevel is < 1)
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: renewable progression level must be >= 1 for item '{item.ItemTemplateId}'."
            );
        }

        if (item.Channels.GroupBy(channel => channel.Channel).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: duplicate channel summaries are not allowed for item '{item.ItemTemplateId}'."
            );
        }

        return item with { ItemTemplateId = item.ItemTemplateId.Trim() };
    }

    private static ItemSourcePressureEvidence ResolveUniqueItem(IGrouping<string, ItemSourcePressureEvidence> group)
    {
        var candidates = group.ToList();
        if (candidates.Count != 1)
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: duplicate source-pressure item '{group.Key}' is ambiguous."
            );
        }

        return candidates[0];
    }

    private static ReplacementRelationshipEvidence ValidateAndNormalizeRelationship(ReplacementRelationshipEvidence relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        var subjectId = relationship.SubjectItemTemplateId?.Trim();
        var candidateId = relationship.CandidateItemTemplateId?.Trim();
        var relationshipClass = relationship.RelationshipClass?.Trim();

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            throw new InvalidOperationException("Economy Admiral replacement evidence: subject item id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(candidateId))
        {
            throw new InvalidOperationException("Economy Admiral replacement evidence: candidate item id must not be empty.");
        }

        if (string.Equals(subjectId, candidateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: subject and candidate must differ for item '{subjectId}'."
            );
        }

        if (string.IsNullOrWhiteSpace(relationshipClass))
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: relationship class must not be empty for '{subjectId}' -> '{candidateId}'."
            );
        }

        return relationship with
        {
            SubjectItemTemplateId = subjectId,
            CandidateItemTemplateId = candidateId,
            RelationshipClass = relationshipClass,
        };
    }

    private static ReplacementRelationshipEvidence ResolveRelationship(
        IGrouping<(string SubjectItemTemplateId, string CandidateItemTemplateId), ReplacementRelationshipEvidence> group
    )
    {
        var candidates = group.ToList();
        var first = candidates[0];

        if (candidates.Any(candidate =>
            !string.Equals(candidate.RelationshipClass, first.RelationshipClass, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Economy Admiral replacement evidence: conflicting relationship evidence for '{first.SubjectItemTemplateId}' -> '{first.CandidateItemTemplateId}'."
            );
        }

        return first;
    }

    private static ReplacementComparabilityEvidence BuildEvidence(
        ItemSourcePressureEvidence subject,
        ItemSourcePressureEvidence candidate,
        ReplacementRelationshipEvidence relationship
    )
    {
        var subjectChannels = subject.Channels
            .Select(channel => channel.Channel)
            .ToHashSet();
        var candidateChannels = candidate.Channels
            .Select(channel => channel.Channel)
            .ToHashSet();

        var sharedChannels = subjectChannels
            .Intersect(candidateChannels)
            .OrderBy(channel => channel)
            .ToList();
        var subjectOnlyChannels = subjectChannels
            .Except(candidateChannels)
            .OrderBy(channel => channel)
            .ToList();
        var candidateOnlyChannels = candidateChannels
            .Except(subjectChannels)
            .OrderBy(channel => channel)
            .ToList();
        var unionCount = subjectChannels.Union(candidateChannels).Count();

        var subjectLevel = subject.EarliestRenewableProgressionLevel;
        var candidateLevel = candidate.EarliestRenewableProgressionLevel;
        var hasKnownProgressionComparison = subjectLevel.HasValue && candidateLevel.HasValue;

        return new ReplacementComparabilityEvidence
        {
            SubjectItemTemplateId = relationship.SubjectItemTemplateId,
            CandidateItemTemplateId = relationship.CandidateItemTemplateId,
            RelationshipClass = relationship.RelationshipClass,
            SubjectHasRenewablePath = subject.HasRenewablePath,
            CandidateHasRenewablePath = candidate.HasRenewablePath,
            SubjectRenewableChannelCount = subject.RenewableChannelCount,
            CandidateRenewableChannelCount = candidate.RenewableChannelCount,
            SubjectEarliestRenewableProgressionLevel = subjectLevel,
            CandidateEarliestRenewableProgressionLevel = candidateLevel,
            RenewableProgressionLevelDelta = hasKnownProgressionComparison ? candidateLevel!.Value - subjectLevel!.Value : null,
            HasKnownRenewableProgressionComparison = hasKnownProgressionComparison,
            SubjectProgressionEvidenceCoverage = subject.ProgressionEvidenceCoverage,
            CandidateProgressionEvidenceCoverage = candidate.ProgressionEvidenceCoverage,
            SubjectHasCompleteProgressionEvidence = subject.HasCompleteProgressionEvidence,
            CandidateHasCompleteProgressionEvidence = candidate.HasCompleteProgressionEvidence,
            SharedChannels = sharedChannels,
            SubjectOnlyChannels = subjectOnlyChannels,
            CandidateOnlyChannels = candidateOnlyChannels,
            ChannelJaccardOverlap = unionCount == 0
                ? null
                : Math.Round((double)sharedChannels.Count / unionCount, 6),
        };
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string SubjectItemTemplateId, string CandidateItemTemplateId)>
    {
        public static StringTupleComparer Ordinal { get; } = new();

        public bool Equals(
            (string SubjectItemTemplateId, string CandidateItemTemplateId) x,
            (string SubjectItemTemplateId, string CandidateItemTemplateId) y
        ) => string.Equals(x.SubjectItemTemplateId, y.SubjectItemTemplateId, StringComparison.Ordinal)
            && string.Equals(x.CandidateItemTemplateId, y.CandidateItemTemplateId, StringComparison.Ordinal);

        public int GetHashCode((string SubjectItemTemplateId, string CandidateItemTemplateId) value)
            => HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.SubjectItemTemplateId),
                StringComparer.Ordinal.GetHashCode(value.CandidateItemTemplateId)
            );
    }
}
