namespace SPTEconomy;

public enum EconomyAttributionConfidence { Unknown = 0, Heuristic = 1, ObservedLifecycleDelta = 2, DeclaredOwnership = 3, ExplicitAdapter = 4 }
public enum EconomyAttributionResolutionState { Unknown, Attributed, Conflict }

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
    public required IReadOnlyList<string> EvidenceSources { get; init; }
    public required IReadOnlyList<string> ClaimedOwners { get; init; }
}

public static class EconomyAttributionEvidenceAnalyzer
{
    public static IReadOnlyList<EconomyDeltaAttributionResolution> Analyze(IEnumerable<EconomyDeltaAttributionClaim> claims)
    {
        ArgumentNullException.ThrowIfNull(claims);
        var normalized = claims.Select(Validate).Distinct().OrderBy(x => x.EntityType, StringComparer.Ordinal)
            .ThenBy(x => x.EntityId, StringComparer.Ordinal).ThenBy(x => x.Dimension, StringComparer.Ordinal)
            .ThenByDescending(x => x.Confidence).ThenBy(x => x.OwnerId, StringComparer.Ordinal).ThenBy(x => x.EvidenceSource, StringComparer.Ordinal).ToList();
        return normalized.GroupBy(x => (x.EntityType, x.EntityId, x.Dimension))
            .Select(Resolve).OrderBy(x => x.EntityType, StringComparer.Ordinal).ThenBy(x => x.EntityId, StringComparer.Ordinal).ThenBy(x => x.Dimension, StringComparer.Ordinal).ToArray();
    }

    private static EconomyDeltaAttributionClaim Validate(EconomyDeltaAttributionClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        if (string.IsNullOrWhiteSpace(claim.EntityType) || string.IsNullOrWhiteSpace(claim.EntityId) || string.IsNullOrWhiteSpace(claim.Dimension) || string.IsNullOrWhiteSpace(claim.EvidenceSource))
            throw new InvalidOperationException("Economy Admiral attribution: identity and evidence source must not be empty.");
        if (!Enum.IsDefined(claim.Confidence)) throw new InvalidOperationException("Economy Admiral attribution: unsupported confidence.");
        var owner = string.IsNullOrWhiteSpace(claim.OwnerId) ? null : claim.OwnerId.Trim();
        if (claim.Confidence == EconomyAttributionConfidence.Unknown && owner is not null)
            throw new InvalidOperationException("Economy Admiral attribution: Unknown confidence cannot claim an owner.");
        if (claim.Confidence != EconomyAttributionConfidence.Unknown && owner is null)
            throw new InvalidOperationException("Economy Admiral attribution: known confidence requires an owner.");
        return claim with { EntityType = claim.EntityType.Trim(), EntityId = claim.EntityId.Trim(), Dimension = claim.Dimension.Trim(), EvidenceSource = claim.EvidenceSource.Trim(), OwnerId = owner };
    }

    private static EconomyDeltaAttributionResolution Resolve(IGrouping<(string EntityType, string EntityId, string Dimension), EconomyDeltaAttributionClaim> group)
    {
        var claims = group.ToList();
        var attributed = claims.Where(x => x.Confidence != EconomyAttributionConfidence.Unknown).ToList();
        if (attributed.Count == 0) return Build(group.Key, EconomyAttributionResolutionState.Unknown, null, EconomyAttributionConfidence.Unknown, claims);
        var confidence = attributed.Max(x => x.Confidence);
        var owners = attributed.Where(x => x.Confidence == confidence).Select(x => x.OwnerId!).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
        return owners.Count == 1
            ? Build(group.Key, EconomyAttributionResolutionState.Attributed, owners[0], confidence, claims)
            : Build(group.Key, EconomyAttributionResolutionState.Conflict, null, confidence, claims);
    }

    private static EconomyDeltaAttributionResolution Build((string EntityType, string EntityId, string Dimension) key, EconomyAttributionResolutionState state, string? owner, EconomyAttributionConfidence confidence, IReadOnlyCollection<EconomyDeltaAttributionClaim> claims) => new()
    {
        EntityType = key.EntityType, EntityId = key.EntityId, Dimension = key.Dimension, State = state, OwnerId = owner, Confidence = confidence,
        EvidenceSources = claims.Select(x => x.EvidenceSource).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        ClaimedOwners = claims.Where(x => x.OwnerId is not null).Select(x => x.OwnerId!).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
    };
}
