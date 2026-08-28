using SPTEconomy;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException($"Economy Admiral health smoke: {message}");
}

static EconomyHealthInvariantInput SafeInput() => new()
{
    SubjectType = "Item",
    SubjectId = "item-a",
    Dimension = "Supply",
    PristineUntouched = false,
    DimensionProvenChangedByMod = true,
    HadRenewablePathBefore = true,
    HasRenewablePathAfter = true,
    EarliestProgressionLevelBefore = 10,
    EarliestProgressionLevelAfter = 11,
    AllowedProgressionDelay = 2,
    ChannelConcentrationHhiBefore = 0.5,
    ChannelConcentrationHhiAfter = 0.55,
    AllowedConcentrationIncrease = 0.1,
    AttributionState = EconomyAttributionResolutionState.Attributed,
    AttributionConfidence = EconomyAttributionConfidence.ExplicitAdapter,
    MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
    ProposedRelativeInterventionMagnitude = 0.1,
    MaximumAllowedRelativeInterventionMagnitude = 0.25,
};

var safe = EconomyHealthInvariantEvaluator.Evaluate(SafeInput());
Require(!safe.FutureAutomaticActionBlocked && !safe.HasFailure && !safe.HasUnknown, "complete safe evidence must pass");

var pristine = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with { PristineUntouched = true });
Require(pristine.HasFailure && pristine.Invariants.Single(x => x.Kind == EconomyHealthInvariantKind.ProtectedPristine).State == EconomyInvariantState.Fail, "untouched pristine must fail");

var dimension = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with { DimensionProvenChangedByMod = false });
Require(dimension.HasFailure, "unproven changed dimension must fail");

var renewable = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with { HasRenewablePathAfter = false });
Require(renewable.HasFailure, "removing last renewable path must fail");

var progression = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with { EarliestProgressionLevelAfter = 14 });
Require(progression.HasFailure, "progression delay above tolerance must fail");

var concentration = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with { ChannelConcentrationHhiAfter = 0.8 });
Require(concentration.HasFailure, "concentration regression must fail");

var attributionConflict = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with { AttributionState = EconomyAttributionResolutionState.Conflict });
Require(attributionConflict.HasFailure, "attribution conflict must fail");

var weakAttribution = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with { AttributionConfidence = EconomyAttributionConfidence.Heuristic });
Require(weakAttribution.HasFailure, "weak attribution must fail");

var intervention = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with { ProposedRelativeInterventionMagnitude = 0.5 });
Require(intervention.HasFailure, "unbounded intervention must fail");

var incomplete = EconomyHealthInvariantEvaluator.Evaluate(SafeInput() with
{
    DimensionProvenChangedByMod = null,
    HasRenewablePathAfter = null,
    AllowedProgressionDelay = null,
    AllowedConcentrationIncrease = null,
    AttributionState = null,
    ProposedRelativeInterventionMagnitude = null,
});
Require(incomplete.HasUnknown && incomplete.FutureAutomaticActionBlocked, "Unknown evidence must fail closed");

var claims = EconomyAttributionEvidenceAnalyzer.Analyze([
    new EconomyDeltaAttributionClaim { EntityType = "Item", EntityId = "x", Dimension = "Supply", OwnerId = "explicit", Confidence = EconomyAttributionConfidence.ExplicitAdapter, EvidenceSource = "adapter" },
    new EconomyDeltaAttributionClaim { EntityType = "Item", EntityId = "x", Dimension = "Supply", OwnerId = "heuristic", Confidence = EconomyAttributionConfidence.Heuristic, EvidenceSource = "guess" },
]);
Require(claims.Single().State == EconomyAttributionResolutionState.Attributed && claims.Single().OwnerId == "explicit", "highest unique attribution confidence must win");
var conflicts = EconomyAttributionEvidenceAnalyzer.Analyze([
    new EconomyDeltaAttributionClaim { EntityType = "Item", EntityId = "x", Dimension = "Supply", OwnerId = "a", Confidence = EconomyAttributionConfidence.DeclaredOwnership, EvidenceSource = "a" },
    new EconomyDeltaAttributionClaim { EntityType = "Item", EntityId = "x", Dimension = "Supply", OwnerId = "b", Confidence = EconomyAttributionConfidence.DeclaredOwnership, EvidenceSource = "b" },
]);
Require(conflicts.Single().State == EconomyAttributionResolutionState.Conflict, "equal top-confidence ownership conflict must remain conflict");

var pressure = new SourcePressureRuntimeReport
{
    LoadedAdapterCount = 0,
    SourceCount = 1,
    CapacityEvidenceCount = 0,
    LoadedAdapters = Array.Empty<string>(),
    ChannelCoverage = Enum.GetValues<AcquisitionChannel>().Select(channel => new ChannelObservationCoverage
    {
        Channel = channel,
        State = channel == AcquisitionChannel.WorldLoot ? "UnknownNoMaintainedAdapter" : "ObservedFinalDb",
        ObservedSourceCount = channel == AcquisitionChannel.QuestReward ? 1 : 0,
    }).ToArray(),
    Items = [new ItemSourcePressureEvidence
    {
        ItemTemplateId = "item-a", SourceCount = 1, ChannelCount = 1, RenewableSourceCount = 0, OneTimeSourceCount = 1,
        RenewableSourceShare = 0, HasRenewablePath = false, RenewableChannelCount = 0, SingleRenewableSourceRisk = false,
        EarliestProgressionLevel = 10, EarliestRenewableProgressionLevel = null, KnownProgressionSourceCount = 1, UnknownProgressionSourceCount = 0,
        ProgressionEvidenceCoverage = 1, HasCompleteProgressionEvidence = true, SingleSourceDominated = true, DominantChannel = AcquisitionChannel.QuestReward,
        DominantChannelSourceShare = 1, ChannelConcentrationHhi = 1, EffectiveChannelCount = 1,
        Channels = [new ChannelSourceSummary { Channel = AcquisitionChannel.QuestReward, SourceCount = 1, RenewableSourceCount = 0 }], ProvenanceClasses = ["ModAdded"],
    }],
    Capacity = Array.Empty<ItemBoundedSupplyEvidence>(),
    AcquisitionGraph = EffectiveAcquisitionGraph.Resolve([]),
    StartupMilliseconds = 2,
};
var health = EconomyHealthRuntimeReportBuilder.Build(pressure);
Require(!health.CompositeScoreSelected && !health.MutationAuthorized, "health observation must not select score or authorize mutation");
Require(health.Items.Single().HasUnknownObservationBoundary, "unknown channel boundary must remain visible in health evidence");

Console.WriteLine("Economy Admiral health invariant + observation smoke PASS");
