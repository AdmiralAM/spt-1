using System.Text.Json;
using SPTEconomy;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void MustFail(string name, Action action)
{
    try
    {
        action();
    }
    catch (InvalidOperationException)
    {
        Console.WriteLine($"PASS {name}");
        return;
    }

    throw new InvalidOperationException($"Expected '{name}' to fail.");
}

var claims = new[]
{
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Quest",
        EntityId = "quest-a",
        Dimension = "Experience",
        OwnerId = "mod-explicit",
        Confidence = EconomyAttributionConfidence.ExplicitAdapter,
        EvidenceSource = "adapter:mod-explicit",
    },
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Quest",
        EntityId = "quest-a",
        Dimension = "Experience",
        OwnerId = "mod-heuristic",
        Confidence = EconomyAttributionConfidence.Heuristic,
        EvidenceSource = "id-prefix",
    },
    new EconomyDeltaAttributionClaim
    {
        EntityType = "TraderOffer",
        EntityId = "trader-a:offer-1",
        Dimension = "Availability",
        OwnerId = null,
        Confidence = EconomyAttributionConfidence.Unknown,
        EvidenceSource = "no-owner-evidence",
    },
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Quest",
        EntityId = "quest-b",
        Dimension = "TraderStanding",
        OwnerId = "mod-one",
        Confidence = EconomyAttributionConfidence.DeclaredOwnership,
        EvidenceSource = "manifest:one",
    },
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Quest",
        EntityId = "quest-b",
        Dimension = "TraderStanding",
        OwnerId = "mod-two",
        Confidence = EconomyAttributionConfidence.DeclaredOwnership,
        EvidenceSource = "manifest:two",
    },
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Item",
        EntityId = "item-c",
        Dimension = "TraderSource",
        OwnerId = "mod-observed",
        Confidence = EconomyAttributionConfidence.ObservedLifecycleDelta,
        EvidenceSource = "lifecycle-window-1",
    },
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Item",
        EntityId = "item-c",
        Dimension = "TraderSource",
        OwnerId = "mod-observed",
        Confidence = EconomyAttributionConfidence.ObservedLifecycleDelta,
        EvidenceSource = "lifecycle-window-1",
    },
};

var result = EconomyAttributionEvidenceAnalyzer.Analyze(claims);
Require(result.Count == 4, "Expected four attribution resolutions.");

var explicitResolution = result.Single(item => item.EntityId == "quest-a");
Require(explicitResolution.State == EconomyAttributionResolutionState.Attributed, "Explicit adapter should resolve attribution.");
Require(explicitResolution.OwnerId == "mod-explicit", "Higher-confidence explicit adapter must beat heuristic evidence.");
Require(explicitResolution.Confidence == EconomyAttributionConfidence.ExplicitAdapter, "Resolved confidence mismatch.");
Require(explicitResolution.ClaimedOwners.SequenceEqual(new[] { "mod-explicit", "mod-heuristic" }), "All claimed owners must remain visible.");
Require(explicitResolution.EvidenceSources.SequenceEqual(new[] { "adapter:mod-explicit", "id-prefix" }), "Evidence sources must be deterministic.");
Require(explicitResolution.ClaimCount == 2, "Claim count mismatch.");
Require(explicitResolution.TopConfidenceClaimCount == 1, "Top-confidence count mismatch.");

var unknown = result.Single(item => item.EntityId == "trader-a:offer-1");
Require(unknown.State == EconomyAttributionResolutionState.Unknown, "Unknown-only evidence must remain Unknown.");
Require(unknown.OwnerId is null, "Unknown attribution must not invent owner.");
Require(unknown.Confidence == EconomyAttributionConfidence.Unknown, "Unknown confidence mismatch.");

var conflict = result.Single(item => item.EntityId == "quest-b");
Require(conflict.State == EconomyAttributionResolutionState.Conflict, "Equal-confidence competing owners must produce Conflict.");
Require(conflict.OwnerId is null, "Conflict must not select an owner.");
Require(conflict.Confidence == EconomyAttributionConfidence.DeclaredOwnership, "Conflict confidence should expose top evidence class.");
Require(conflict.ClaimedOwners.SequenceEqual(new[] { "mod-one", "mod-two" }), "Conflict owner list must be deterministic.");
Require(conflict.TopConfidenceClaimCount == 2, "Conflict top-confidence claim count mismatch.");

var deduplicated = result.Single(item => item.EntityId == "item-c");
Require(deduplicated.State == EconomyAttributionResolutionState.Attributed, "Identical observed claims should resolve.");
Require(deduplicated.OwnerId == "mod-observed", "Observed owner mismatch.");
Require(deduplicated.ClaimCount == 1, "Exact duplicate claims must de-duplicate.");

var reversed = EconomyAttributionEvidenceAnalyzer.Analyze(claims.Reverse());
var forwardJson = JsonSerializer.Serialize(result);
var reversedJson = JsonSerializer.Serialize(reversed);
Require(string.Equals(forwardJson, reversedJson, StringComparison.Ordinal), "Attribution output must be independent of input ordering.");

MustFail("unknown confidence with owner", () => EconomyAttributionEvidenceAnalyzer.Analyze(new[]
{
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Quest",
        EntityId = "bad-1",
        Dimension = "Experience",
        OwnerId = "invented-owner",
        Confidence = EconomyAttributionConfidence.Unknown,
        EvidenceSource = "bad",
    },
}));

MustFail("non-unknown confidence without owner", () => EconomyAttributionEvidenceAnalyzer.Analyze(new[]
{
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Quest",
        EntityId = "bad-2",
        Dimension = "Experience",
        OwnerId = null,
        Confidence = EconomyAttributionConfidence.Heuristic,
        EvidenceSource = "bad",
    },
}));

MustFail("empty identity", () => EconomyAttributionEvidenceAnalyzer.Analyze(new[]
{
    new EconomyDeltaAttributionClaim
    {
        EntityType = " ",
        EntityId = "bad-3",
        Dimension = "Experience",
        OwnerId = null,
        Confidence = EconomyAttributionConfidence.Unknown,
        EvidenceSource = "bad",
    },
}));

MustFail("unsupported enum", () => EconomyAttributionEvidenceAnalyzer.Analyze(new[]
{
    new EconomyDeltaAttributionClaim
    {
        EntityType = "Quest",
        EntityId = "bad-4",
        Dimension = "Experience",
        OwnerId = "owner",
        Confidence = (EconomyAttributionConfidence)999,
        EvidenceSource = "bad",
    },
}));

Console.WriteLine("Economy Admiral attribution evidence smoke PASS");
