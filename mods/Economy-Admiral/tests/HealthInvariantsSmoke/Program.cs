using SPTEconomy;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static EconomyHealthInvariantResult Find(EconomyHealthInvariantEvaluation evaluation, EconomyHealthInvariantKind kind)
    => evaluation.Invariants.Single(result => result.Kind == kind);

static EconomyPolicyPreviewCandidateInput PreviewCandidate(EconomyHealthInvariantEvaluation health) => new()
{
    CandidateId = "candidate-preview",
    PolicyId = "Normal",
    SubjectType = health.SubjectType,
    SubjectId = health.SubjectId,
    Dimension = health.Dimension,
    AnomalyReason = "Candidate exceeds pristine-relative acquisition-pressure envelope.",
    BaselineSource = "PristineStartupSnapshot",
    CurrentValue = 10,
    TargetValue = 7,
    ProjectedValue = 7,
    Health = health,
};

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

var healthy = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "Item",
    SubjectId = "item-safe",
    Dimension = "Availability",
    PristineUntouched = false,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = true,
    EarliestProgressionLevelBefore = 10,
    EarliestProgressionLevelAfter = 11,
    AllowedProgressionDelay = 2,
    ChannelConcentrationHhiBefore = 0.40,
    ChannelConcentrationHhiAfter = 0.45,
    AllowedConcentrationIncrease = 0.10,
    AttributionState = EconomyAttributionResolutionState.Attributed,
    AttributionConfidence = EconomyAttributionConfidence.ExplicitAdapter,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
});
Require(!healthy.HasFailure && !healthy.HasUnknown, "Complete safe evidence should pass all invariants.");
Require(!healthy.FutureAutomaticActionBlocked, "Complete safe evidence should not be blocked by invariant domain.");
Require(healthy.AllKnownInvariantsPass, "All known invariants should pass.");

var protectedPristine = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "Quest",
    SubjectId = "pristine-quest",
    Dimension = "Experience",
    PristineUntouched = true,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = true,
    EarliestProgressionLevelBefore = 1,
    EarliestProgressionLevelAfter = 1,
    AllowedProgressionDelay = 0,
    ChannelConcentrationHhiBefore = 1.0,
    ChannelConcentrationHhiAfter = 1.0,
    AllowedConcentrationIncrease = 0,
    AttributionState = EconomyAttributionResolutionState.Attributed,
    AttributionConfidence = EconomyAttributionConfidence.ExplicitAdapter,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
});
Require(Find(protectedPristine, EconomyHealthInvariantKind.ProtectedPristine).State == EconomyInvariantState.Fail, "Untouched pristine must fail protection invariant.");
Require(protectedPristine.FutureAutomaticActionBlocked, "Protected pristine must block future automatic action.");

var lastRenewableRemoved = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "Item",
    SubjectId = "item-no-renewable",
    Dimension = "Availability",
    PristineUntouched = false,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = false,
    EarliestProgressionLevelBefore = 5,
    EarliestProgressionLevelAfter = 5,
    AllowedProgressionDelay = 0,
    ChannelConcentrationHhiBefore = 0.5,
    ChannelConcentrationHhiAfter = 0.5,
    AllowedConcentrationIncrease = 0,
    AttributionState = EconomyAttributionResolutionState.Attributed,
    AttributionConfidence = EconomyAttributionConfidence.ExplicitAdapter,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
});
Require(Find(lastRenewableRemoved, EconomyHealthInvariantKind.RenewablePathContinuity).State == EconomyInvariantState.Fail, "Removing last renewable path must fail.");

var progressionRegression = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "Item",
    SubjectId = "item-late",
    Dimension = "Availability",
    PristineUntouched = false,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = true,
    EarliestProgressionLevelBefore = 10,
    EarliestProgressionLevelAfter = 15,
    AllowedProgressionDelay = 2,
    ChannelConcentrationHhiBefore = 0.5,
    ChannelConcentrationHhiAfter = 0.5,
    AllowedConcentrationIncrease = 0,
    AttributionState = EconomyAttributionResolutionState.Attributed,
    AttributionConfidence = EconomyAttributionConfidence.ExplicitAdapter,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
});
Require(Find(progressionRegression, EconomyHealthInvariantKind.ProgressionAccessRegression).State == EconomyInvariantState.Fail, "Excessive progression delay must fail.");

var concentrationRegression = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "Item",
    SubjectId = "item-concentrated",
    Dimension = "Availability",
    PristineUntouched = false,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = true,
    EarliestProgressionLevelBefore = 10,
    EarliestProgressionLevelAfter = 10,
    AllowedProgressionDelay = 0,
    ChannelConcentrationHhiBefore = 0.34,
    ChannelConcentrationHhiAfter = 0.80,
    AllowedConcentrationIncrease = 0.10,
    AttributionState = EconomyAttributionResolutionState.Attributed,
    AttributionConfidence = EconomyAttributionConfidence.ExplicitAdapter,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
});
Require(Find(concentrationRegression, EconomyHealthInvariantKind.SourceConcentrationRegression).State == EconomyInvariantState.Fail, "Excessive concentration increase must fail.");

var weakAttribution = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "TraderOffer",
    SubjectId = "offer-weak",
    Dimension = "Availability",
    PristineUntouched = false,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = true,
    EarliestProgressionLevelBefore = 10,
    EarliestProgressionLevelAfter = 10,
    AllowedProgressionDelay = 0,
    ChannelConcentrationHhiBefore = 1.0,
    ChannelConcentrationHhiAfter = 1.0,
    AllowedConcentrationIncrease = 0,
    AttributionState = EconomyAttributionResolutionState.Attributed,
    AttributionConfidence = EconomyAttributionConfidence.Heuristic,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
});
Require(Find(weakAttribution, EconomyHealthInvariantKind.AttributionConfidence).State == EconomyInvariantState.Fail, "Heuristic attribution must not satisfy declared-ownership requirement.");

var conflictAttribution = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "TraderOffer",
    SubjectId = "offer-conflict",
    Dimension = "Availability",
    PristineUntouched = false,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = true,
    EarliestProgressionLevelBefore = 10,
    EarliestProgressionLevelAfter = 10,
    AllowedProgressionDelay = 0,
    ChannelConcentrationHhiBefore = 1.0,
    ChannelConcentrationHhiAfter = 1.0,
    AllowedConcentrationIncrease = 0,
    AttributionState = EconomyAttributionResolutionState.Conflict,
    AttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
});
Require(Find(conflictAttribution, EconomyHealthInvariantKind.AttributionConfidence).State == EconomyInvariantState.Fail, "Conflicting attribution must fail even at sufficient confidence class.");

var unknownAttribution = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "TraderOffer",
    SubjectId = "offer-unknown",
    Dimension = "Availability",
    PristineUntouched = false,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = true,
    EarliestProgressionLevelBefore = 10,
    EarliestProgressionLevelAfter = 10,
    AllowedProgressionDelay = 0,
    ChannelConcentrationHhiBefore = 1.0,
    ChannelConcentrationHhiAfter = 1.0,
    AllowedConcentrationIncrease = 0,
    AttributionState = EconomyAttributionResolutionState.Unknown,
    AttributionConfidence = EconomyAttributionConfidence.Unknown,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
});
Require(Find(unknownAttribution, EconomyHealthInvariantKind.AttributionConfidence).State == EconomyInvariantState.Unknown, "Unknown attribution must remain Unknown, not measured Fail.");
Require(unknownAttribution.FutureAutomaticActionBlocked, "Unknown attribution must still fail closed for future automatic action.");
Require(!unknownAttribution.HasFailure, "Unknown attribution must remain distinct from conflict/weak-confidence failure.");

var unknown = EconomyHealthInvariantEvaluator.Evaluate(new EconomyHealthInvariantInput
{
    SubjectType = "Item",
    SubjectId = "item-unknown",
    Dimension = "Availability",
    PristineUntouched = false,
    HadRenewablePathBefore = null,
    HasRenewablePathAfter = null,
    EarliestProgressionLevelBefore = null,
    EarliestProgressionLevelAfter = null,
    AllowedProgressionDelay = null,
    ChannelConcentrationHhiBefore = null,
    ChannelConcentrationHhiAfter = null,
    AllowedConcentrationIncrease = null,
    AttributionState = null,
    AttributionConfidence = null,
    MinimumRequiredAttributionConfidence = null,
});
Require(unknown.HasUnknown, "Incomplete evidence must expose Unknown.");
Require(unknown.FutureAutomaticActionBlocked, "Unknown invariant state must fail closed for future automatic action.");
Require(!unknown.HasFailure, "Unknown evidence should remain distinct from measured failure.");
Require(unknown.AllKnownInvariantsPass, "Known non-pristine invariant may pass while unknowns still block action.");

var previewable = EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(healthy));
Require(previewable.Disposition == EconomyPolicyPreviewDisposition.Previewable, "Passing complete health evidence should produce Previewable disposition.");
Require(previewable.BlockingReasons.Count == 0 && previewable.UnknownReasons.Count == 0, "Previewable decision must have no blocking/unknown reasons.");
Require(previewable.ProjectedDelta == -3 && !previewable.IsNoOp, "Preview projection delta mismatch.");
Require(!previewable.MutationAuthorized, "Preview domain must never authorize mutation.");

var blockedPreview = EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(protectedPristine));
Require(blockedPreview.Disposition == EconomyPolicyPreviewDisposition.Blocked, "Failed health invariant must produce Blocked disposition.");
Require(blockedPreview.BlockingReasons.Any(reason => reason.StartsWith("ProtectedPristine:", StringComparison.Ordinal)), "Blocked preview must expose exact invariant reason.");
Require(!blockedPreview.MutationAuthorized, "Blocked preview must never authorize mutation.");

var incompletePreview = EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(unknown));
Require(incompletePreview.Disposition == EconomyPolicyPreviewDisposition.IncompleteEvidence, "Unknown health evidence must produce IncompleteEvidence disposition.");
Require(incompletePreview.UnknownReasons.Count > 0, "Incomplete preview must preserve unknown reasons.");
Require(!incompletePreview.MutationAuthorized, "Incomplete preview must never authorize mutation.");

var noOpPreview = EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(healthy) with { TargetValue = 10, ProjectedValue = 10 });
Require(noOpPreview.IsNoOp && noOpPreview.ProjectedDelta == 0, "Equal current/projected values must be explicit no-op evidence.");

var reversedHealthy = healthy with { Invariants = healthy.Invariants.AsEnumerable().Reverse().ToList() };
var deterministicPreview = EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(reversedHealthy));
Require(
    previewable.HealthInvariants.Select(result => result.Kind).SequenceEqual(deterministicPreview.HealthInvariants.Select(result => result.Kind)),
    "Preview invariant ordering must be deterministic regardless of input ordering."
);

MustFail("preview subject mismatch", () => EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(healthy) with { SubjectId = "different-item" }));
MustFail("preview non-finite value", () => EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(healthy) with { CurrentValue = double.NaN }));
MustFail("preview empty policy id", () => EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(healthy) with { PolicyId = " " }));
MustFail("preview inconsistent health summary", () => EconomyPolicyPreviewEvidenceBuilder.Build(PreviewCandidate(healthy with { HasFailure = true })));

Console.WriteLine("Economy Admiral health invariants + policy preview smoke PASS");
