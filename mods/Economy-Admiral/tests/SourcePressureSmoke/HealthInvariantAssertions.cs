using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class HealthInvariantAssertions
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException($"Economy Admiral health assertions: {message}");
        }

        static EconomyHealthInvariantInput Safe() => new()
        {
            SubjectType = "Item", SubjectId = "item", Dimension = "Supply", PristineUntouched = false,
            DimensionProvenChangedByMod = true, HadRenewablePathBefore = true, HasRenewablePathAfter = true,
            EarliestProgressionLevelBefore = 10, EarliestProgressionLevelAfter = 11, AllowedProgressionDelay = 2,
            ChannelConcentrationHhiBefore = 0.5, ChannelConcentrationHhiAfter = 0.55, AllowedConcentrationIncrease = 0.1,
            AttributionState = EconomyAttributionResolutionState.Attributed, AttributionConfidence = EconomyAttributionConfidence.ExplicitAdapter,
            MinimumRequiredAttributionConfidence = EconomyAttributionConfidence.DeclaredOwnership,
            ProposedRelativeInterventionMagnitude = 0.1, MaximumAllowedRelativeInterventionMagnitude = 0.25,
        };

        Require(!EconomyHealthInvariantEvaluator.Evaluate(Safe()).FutureAutomaticActionBlocked, "safe complete evidence must pass");
        Require(EconomyHealthInvariantEvaluator.Evaluate(Safe() with { PristineUntouched = true }).HasFailure, "pristine protection");
        Require(EconomyHealthInvariantEvaluator.Evaluate(Safe() with { DimensionProvenChangedByMod = false }).HasFailure, "dimension proof");
        Require(EconomyHealthInvariantEvaluator.Evaluate(Safe() with { HasRenewablePathAfter = false }).HasFailure, "renewable continuity");
        Require(EconomyHealthInvariantEvaluator.Evaluate(Safe() with { EarliestProgressionLevelAfter = 14 }).HasFailure, "progression regression");
        Require(EconomyHealthInvariantEvaluator.Evaluate(Safe() with { ChannelConcentrationHhiAfter = 0.8 }).HasFailure, "concentration regression");
        Require(EconomyHealthInvariantEvaluator.Evaluate(Safe() with { AttributionState = EconomyAttributionResolutionState.Conflict }).HasFailure, "attribution conflict");
        Require(EconomyHealthInvariantEvaluator.Evaluate(Safe() with { ProposedRelativeInterventionMagnitude = 0.5 }).HasFailure, "intervention bound");
        Require(EconomyHealthInvariantEvaluator.Evaluate(Safe() with { DimensionProvenChangedByMod = null }).HasUnknown, "unknown evidence must remain unknown");

        var attribution = EconomyAttributionEvidenceAnalyzer.Analyze([
            new EconomyDeltaAttributionClaim { EntityType = "Item", EntityId = "x", Dimension = "Supply", OwnerId = "explicit", Confidence = EconomyAttributionConfidence.ExplicitAdapter, EvidenceSource = "adapter" },
            new EconomyDeltaAttributionClaim { EntityType = "Item", EntityId = "x", Dimension = "Supply", OwnerId = "guess", Confidence = EconomyAttributionConfidence.Heuristic, EvidenceSource = "heuristic" },
        ]).Single();
        Require(attribution.State == EconomyAttributionResolutionState.Attributed && attribution.OwnerId == "explicit", "explicit attribution must outrank heuristic evidence");

        Console.WriteLine("Economy Admiral health invariant assertions PASS");
    }
}
