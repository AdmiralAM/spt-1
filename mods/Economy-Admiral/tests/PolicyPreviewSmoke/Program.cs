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

static EconomyHealthInvariantEvaluation Health(
    bool pristineUntouched = false,
    bool? hadRenewable = true,
    bool? hasRenewable = true,
    int? beforeLevel = 10,
    int? afterLevel = 11,
    int? allowedDelay = 2,
    double? beforeHhi = 0.4,
    double? afterHhi = 0.45,
    double? allowedHhiIncrease = 0.1,
    EconomyAttributionResolutionState? attributionState = EconomyAttributionResolutionState.Resolved,
    EconomyAttributionConfidence? attributionConfidence = EconomyAttributionConfidence.ExplicitAdapter,
    EconomyAttributionConfidence? minimumConfidence = EconomyAttributionConfidence.DeclaredOwnership)
    => EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
    {
        SubjectType = "Item",
        SubjectId = "item-a",
        Dimension = "TraderSupply",
        PristineUntouched = pristineUntouched,
        HadRenewablePathBefore = hadRenewable,
        HasRenewablePathAfter = hasRenewable,
        EarliestProgressionLevelBefore = beforeLevel,
        EarliestProgressionLevelAfter = afterLevel,
        AllowedProgressionDelay = allowedDelay,
        ChannelConcentrationHhiBefore = beforeHhi,
        ChannelConcentrationHhiAfter = afterHhi,
        AllowedConcentrationIncrease = allowedHhiIncrease,
        AttributionState = attributionState,
        AttributionConfidence = attributionConfidence,
        MinimumRequiredAttributionConfidence = minimumConfidence,
    });

static EconomyPolicyPreviewCandidateInput Candidate(EconomyHealthInvariantEvaluation health) => new()
{
    CandidateId = "candidate-a",
    PolicyId = "Normal",
    SubjectType = "Item",
    SubjectId = "item-a",
    Dimension = "TraderSupply",
    AnomalyReason = "Renewable trader supply exceeds pristine-relative source pressure envelope.",
    BaselineSource = "PristineStartupSnapshot",
    CurrentValue = 10,
    TargetValue = 7,
    ProjectedValue = 7,
    Health = health,
};

var previewable = EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(Health()));
Require(previewable.Disposition == EconomyPolicyPreviewDisposition.Previewable, "Complete passing health should be previewable.");
Require(previewable.BlockingReasons.Count == 0, "Previewable candidate must not have blocking reasons.");
Require(previewable.UnknownReasons.Count == 0, "Previewable candidate must not have unknown reasons.");
Require(previewable.ProjectedDelta == -3, "Projected delta mismatch.");
Require(!previewable.IsNoOp, "Changed projection must not be a no-op.");
Require(!previewable.MutationAuthorized, "Preview evidence must never authorize mutation.");

var blocked = EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(Health(pristineUntouched: true)));
Require(blocked.Disposition == EconomyPolicyPreviewDisposition.Blocked, "Untouched pristine target should be blocked.");
Require(blocked.BlockingReasons.Any(reason => reason.StartsWith("ProtectedPristine:", StringComparison.Ordinal)), "Blocked preview must explain pristine protection.");
Require(!blocked.MutationAuthorized, "Blocked preview must not authorize mutation.");

var incomplete = EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(Health(afterLevel: null)));
Require(incomplete.Disposition == EconomyPolicyPreviewDisposition.IncompleteEvidence, "Unknown health evidence should remain incomplete.");
Require(incomplete.UnknownReasons.Any(reason => reason.StartsWith("ProgressionAccessRegression:", StringComparison.Ordinal)), "Incomplete preview must expose unknown progression reason.");
Require(!incomplete.MutationAuthorized, "Incomplete preview must not authorize mutation.");

var noOpInput = Candidate(Health()) with { ProjectedValue = 10, TargetValue = 10 };
var noOp = EconomyPolicyPreviewEvidenceBuilder.Build(noOpInput);
Require(noOp.IsNoOp, "Equal current/projected values must be identified as no-op.");
Require(noOp.ProjectedDelta == 0, "No-op delta must be zero.");
Require(!noOp.MutationAuthorized, "No-op preview must not authorize mutation.");

var reversedHealth = Health() with { Invariants = Health().Invariants.AsEnumerable().Reverse().ToList() };
var deterministicA = EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(Health()));
var deterministicB = EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(reversedHealth));
var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
Require(
    JsonSerializer.Serialize(deterministicA, jsonOptions) == JsonSerializer.Serialize(deterministicB, jsonOptions),
    "Preview decision must be deterministic regardless of invariant input ordering."
);

MustFail("subject mismatch", () => EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(Health()) with { SubjectId = "different-item" }));
MustFail("non-finite current value", () => EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(Health()) with { CurrentValue = double.NaN }));
MustFail("empty policy id", () => EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(Health()) with { PolicyId = " " }));

var inconsistentHealth = Health() with { HasFailure = true };
MustFail("inconsistent health summary", () => EconomyPolicyPreviewEvidenceBuilder.Build(Candidate(inconsistentHealth)));

Console.WriteLine("Economy Admiral policy preview smoke PASS");
