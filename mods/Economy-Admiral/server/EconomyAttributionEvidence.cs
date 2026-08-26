namespace SPTEconomy;

public enum EconomyAttributionConfidence
{
    Unknown = 0,
    Heuristic = 1,
    ObservedLifecycleDelta = 2,
    DeclaredOwnership = 3,
    ExplicitAdapter = 4,
}

public enum EconomyAttributionResolutionState
{
    Unknown,
    Attributed,
    Conflict,
}

public sealed record EconomyDeltaAttributionClaim
{
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Dimension { get; init; }
    public string? OwnerId { get; init; }
    public required EconomyAttributionConfidence Confidence { get; init; }
    public required string EvidenceSource { get; init; }
}

public sealed record EconomyDeltaAttributionResolution
{
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required string Dimension { get; init; }
    public required EconomyAttributionResolutionState State { get; init; }
    public string? OwnerId { get; init; }
    public required EconomyAttributionConfidence Confidence { get; init; }
    public required List<string> EvidenceSources { get; init; }
    public required List<string> ClaimedOwners { get; init; }
    public required int ClaimCount { get; init; }
    public required int TopConfidenceClaimCount { get; init; }
}

public static class EconomyAttributionEvidenceAnalyzer
{
    public static IReadOnlyList<EconomyDeltaAttributionResolution> Analyze(IEnumerable<EconomyDeltaAttributionClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);

        var normalized = claims
            .Select(ValidateAndNormalize)
            .Distinct()
            .OrderBy(claim => claim.EntityType, StringComparer.Ordinal)
            .ThenBy(claim => claim.EntityId, StringComparer.Ordinal)
            .ThenBy(claim => claim.Dimension, StringComparer.Ordinal)
            .ThenByDescending(claim => claim.Confidence)
            .ThenBy(claim => claim.OwnerId, StringComparer.Ordinal)
            .ThenBy(claim => claim.EvidenceSource, StringComparer.Ordinal)
            .ToList();

        return normalized
            .GroupBy(claim => (claim.EntityType, claim.EntityId, claim.Dimension))
            .Select(Resolve)
            .OrderBy(result => result.EntityType, StringComparer.Ordinal)
            .ThenBy(result => result.EntityId, StringComparer.Ordinal)
            .ThenBy(result => result.Dimension, StringComparer.Ordinal)
            .ToList();
    }

    private static EconomyDeltaAttributionClaim ValidateAndNormalize(EconomyDeltaAttributionClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        if (string.IsNullOrWhiteSpace(claim.EntityType))
        {
            throw new InvalidOperationException("Economy Admiral attribution: entity type must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(claim.EntityId))
        {
            throw new InvalidOperationException("Economy Admiral attribution: entity id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(claim.Dimension))
        {
            throw new InvalidOperationException("Economy Admiral attribution: dimension must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(claim.EvidenceSource))
        {
            throw new InvalidOperationException("Economy Admiral attribution: evidence source must not be empty.");
        }

        if (!Enum.IsDefined(claim.Confidence))
        {
            throw new InvalidOperationException($"Economy Admiral attribution: unsupported confidence value '{claim.Confidence}'.");
        }

        var owner = string.IsNullOrWhiteSpace(claim.OwnerId) ? null : claim.OwnerId.Trim();
        if (claim.Confidence == EconomyAttributionConfidence.Unknown && owner is not null)
        {
            throw new InvalidOperationException("Economy Admiral attribution: Unknown confidence must not claim an owner.");
        }

        if (claim.Confidence != EconomyAttributionConfidence.Unknown && owner is null)
        {
            throw new InvalidOperationException($"Economy Admiral attribution: confidence '{claim.Confidence}' requires an owner.");
        }

        return claim with
        {
            EntityType = claim.EntityType.Trim(),
            EntityId = claim.EntityId.Trim(),
            Dimension = claim.Dimension.Trim(),
            OwnerId = owner,
            EvidenceSource = claim.EvidenceSource.Trim(),
        };
    }

    private static EconomyDeltaAttributionResolution Resolve(
        IGrouping<(string EntityType, string EntityId, string Dimension), EconomyDeltaAttributionClaim> group
    )
    {
        var claims = group.ToList();
        var attributable = claims
            .Where(claim => claim.Confidence != EconomyAttributionConfidence.Unknown)
            .ToList();

        if (attributable.Count == 0)
        {
            return Build(
                group.Key,
                EconomyAttributionResolutionState.Unknown,
                null,
                EconomyAttributionConfidence.Unknown,
                claims,
                claims.Count
            );
        }

        var topConfidence = attributable.Max(claim => claim.Confidence);
        var topClaims = attributable
            .Where(claim => claim.Confidence == topConfidence)
            .ToList();
        var topOwners = topClaims
            .Select(claim => claim.OwnerId!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(owner => owner, StringComparer.Ordinal)
            .ToList();

        if (topOwners.Count != 1)
        {
            return Build(
                group.Key,
                EconomyAttributionResolutionState.Conflict,
                null,
                topConfidence,
                claims,
                topClaims.Count
            );
        }

        return Build(
            group.Key,
            EconomyAttributionResolutionState.Attributed,
            topOwners[0],
            topConfidence,
            claims,
            topClaims.Count
        );
    }

    private static EconomyDeltaAttributionResolution Build(
        (string EntityType, string EntityId, string Dimension) key,
        EconomyAttributionResolutionState state,
        string? ownerId,
        EconomyAttributionConfidence confidence,
        IReadOnlyCollection<EconomyDeltaAttributionClaim> claims,
        int topConfidenceClaimCount
    )
    {
        return new EconomyDeltaAttributionResolution
        {
            EntityType = key.EntityType,
            EntityId = key.EntityId,
            Dimension = key.Dimension,
            State = state,
            OwnerId = ownerId,
            Confidence = confidence,
            EvidenceSources = claims
                .Select(claim => claim.EvidenceSource)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            ClaimedOwners = claims
                .Where(claim => claim.OwnerId is not null)
                .Select(claim => claim.OwnerId!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            ClaimCount = claims.Count,
            TopConfidenceClaimCount = topConfidenceClaimCount,
        };
    }
}
