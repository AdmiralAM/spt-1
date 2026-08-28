namespace SPTEconomy;

public enum EconomyInvariantState
{
    Pass,
    Fail,
    Unknown,
}

public enum EconomyHealthInvariantKind
{
    ProtectedPristine,
    ChangedDimensionProof,
    RenewablePathContinuity,
    ProgressionAccessRegression,
    SourceConcentrationRegression,
    AttributionConfidence,
    InterventionMagnitude,
}

public sealed record EconomyHealthInvariantInput
{
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string Dimension { get; init; }
    public required bool PristineUntouched { get; init; }
    public bool? DimensionProvenChangedByMod { get; init; }
    public bool? HadRenewablePathBefore { get; init; }
    public bool? HasRenewablePathAfter { get; init; }
    public int? EarliestProgressionLevelBefore { get; init; }
    public int? EarliestProgressionLevelAfter { get; init; }
    public int? AllowedProgressionDelay { get; init; }
    public double? ChannelConcentrationHhiBefore { get; init; }
    public double? ChannelConcentrationHhiAfter { get; init; }
    public double? AllowedConcentrationIncrease { get; init; }
    public EconomyAttributionResolutionState? AttributionState { get; init; }
    public EconomyAttributionConfidence? AttributionConfidence { get; init; }
    public EconomyAttributionConfidence? MinimumRequiredAttributionConfidence { get; init; }
    public double? ProposedRelativeInterventionMagnitude { get; init; }
    public double? MaximumAllowedRelativeInterventionMagnitude { get; init; }
}

public sealed record EconomyHealthInvariantResult
{
    public required EconomyHealthInvariantKind Kind { get; init; }
    public required EconomyInvariantState State { get; init; }
    public required string Reason { get; init; }
}

public sealed record EconomyHealthInvariantEvaluation
{
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string Dimension { get; init; }
    public required IReadOnlyList<EconomyHealthInvariantResult> Invariants { get; init; }
    public required bool AllKnownInvariantsPass { get; init; }
    public required bool HasFailure { get; init; }
    public required bool HasUnknown { get; init; }
    public required bool FutureAutomaticActionBlocked { get; init; }
}

public static class EconomyHealthInvariantEvaluator
{
    public static EconomyHealthInvariantEvaluation Evaluate(EconomyHealthInvariantInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var results = new[]
        {
            ProtectedPristine(input),
            ChangedDimension(input),
            RenewableContinuity(input),
            Progression(input),
            Concentration(input),
            Attribution(input),
            Intervention(input),
        };
        var hasFailure = results.Any(x => x.State == EconomyInvariantState.Fail);
        var hasUnknown = results.Any(x => x.State == EconomyInvariantState.Unknown);
        return new EconomyHealthInvariantEvaluation
        {
            SubjectType = input.SubjectType.Trim(),
            SubjectId = input.SubjectId.Trim(),
            Dimension = input.Dimension.Trim(),
            Invariants = results,
            AllKnownInvariantsPass = !hasFailure,
            HasFailure = hasFailure,
            HasUnknown = hasUnknown,
            FutureAutomaticActionBlocked = hasFailure || hasUnknown,
        };
    }

    private static void Validate(EconomyHealthInvariantInput input)
    {
        if (string.IsNullOrWhiteSpace(input.SubjectType) || string.IsNullOrWhiteSpace(input.SubjectId) || string.IsNullOrWhiteSpace(input.Dimension))
            throw new InvalidOperationException("Economy Admiral health: subject and dimension identity must not be empty.");
        if (input.EarliestProgressionLevelBefore is < 1 || input.EarliestProgressionLevelAfter is < 1)
            throw new InvalidOperationException("Economy Admiral health: progression levels must be >= 1 when known.");
        if (input.AllowedProgressionDelay is < 0)
            throw new InvalidOperationException("Economy Admiral health: allowed progression delay must be non-negative.");
        ValidateUnit(input.ChannelConcentrationHhiBefore, nameof(input.ChannelConcentrationHhiBefore));
        ValidateUnit(input.ChannelConcentrationHhiAfter, nameof(input.ChannelConcentrationHhiAfter));
        ValidateUnit(input.AllowedConcentrationIncrease, nameof(input.AllowedConcentrationIncrease));
        ValidateUnit(input.ProposedRelativeInterventionMagnitude, nameof(input.ProposedRelativeInterventionMagnitude));
        ValidateUnit(input.MaximumAllowedRelativeInterventionMagnitude, nameof(input.MaximumAllowedRelativeInterventionMagnitude));
        if (input.AttributionState.HasValue && !Enum.IsDefined(input.AttributionState.Value))
            throw new InvalidOperationException("Economy Admiral health: invalid attribution state.");
        if (input.AttributionConfidence.HasValue && !Enum.IsDefined(input.AttributionConfidence.Value))
            throw new InvalidOperationException("Economy Admiral health: invalid attribution confidence.");
        if (input.MinimumRequiredAttributionConfidence.HasValue && !Enum.IsDefined(input.MinimumRequiredAttributionConfidence.Value))
            throw new InvalidOperationException("Economy Admiral health: invalid minimum attribution confidence.");
    }

    private static void ValidateUnit(double? value, string name)
    {
        if (value.HasValue && (!double.IsFinite(value.Value) || value.Value < 0 || value.Value > 1))
            throw new InvalidOperationException($"Economy Admiral health: {name} must be finite and within [0,1].");
    }

    private static EconomyHealthInvariantResult ProtectedPristine(EconomyHealthInvariantInput input) => Result(
        EconomyHealthInvariantKind.ProtectedPristine,
        input.PristineUntouched ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
        input.PristineUntouched ? "Untouched pristine content remains protected." : "Subject is not untouched pristine content.");

    private static EconomyHealthInvariantResult ChangedDimension(EconomyHealthInvariantInput input)
    {
        if (!input.DimensionProvenChangedByMod.HasValue)
            return Result(EconomyHealthInvariantKind.ChangedDimensionProof, EconomyInvariantState.Unknown, "Changed-dimension provenance evidence is unavailable.");
        return Result(EconomyHealthInvariantKind.ChangedDimensionProof,
            input.DimensionProvenChangedByMod.Value ? EconomyInvariantState.Pass : EconomyInvariantState.Fail,
            input.DimensionProvenChangedByMod.Value ? "Target dimension is proven changed by the mod stack." : "Target dimension is not proven changed by the mod stack.");
    }

    private static EconomyHealthInvariantResult RenewableContinuity(EconomyHealthInvariantInput input)
    {
        if (!input.HadRenewablePathBefore.HasValue || !input.HasRenewablePathAfter.HasValue)
            return Result(EconomyHealthInvariantKind.RenewablePathContinuity, EconomyInvariantState.Unknown, "Renewable-path evidence is incomplete.");
        var removedLast = input.HadRenewablePathBefore.Value && !input.HasRenewablePathAfter.Value;
        return Result(EconomyHealthInvariantKind.RenewablePathContinuity, removedLast ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            removedLast ? "Candidate removes the last known renewable path." : "Renewable-path continuity is preserved.");
    }

    private static EconomyHealthInvariantResult Progression(EconomyHealthInvariantInput input)
    {
        if (!input.EarliestProgressionLevelBefore.HasValue || !input.EarliestProgressionLevelAfter.HasValue || !input.AllowedProgressionDelay.HasValue)
            return Result(EconomyHealthInvariantKind.ProgressionAccessRegression, EconomyInvariantState.Unknown, "Progression comparison evidence or tolerance is incomplete.");
        var delay = input.EarliestProgressionLevelAfter.Value - input.EarliestProgressionLevelBefore.Value;
        var fail = delay > input.AllowedProgressionDelay.Value;
        return Result(EconomyHealthInvariantKind.ProgressionAccessRegression, fail ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            fail ? $"Access is delayed by {delay} levels, above tolerance {input.AllowedProgressionDelay.Value}." : $"Access delay {delay} is within tolerance {input.AllowedProgressionDelay.Value}.");
    }

    private static EconomyHealthInvariantResult Concentration(EconomyHealthInvariantInput input)
    {
        if (!input.ChannelConcentrationHhiBefore.HasValue || !input.ChannelConcentrationHhiAfter.HasValue || !input.AllowedConcentrationIncrease.HasValue)
            return Result(EconomyHealthInvariantKind.SourceConcentrationRegression, EconomyInvariantState.Unknown, "Source-concentration evidence or tolerance is incomplete.");
        var increase = input.ChannelConcentrationHhiAfter.Value - input.ChannelConcentrationHhiBefore.Value;
        var fail = increase > input.AllowedConcentrationIncrease.Value + 0.0000001d;
        return Result(EconomyHealthInvariantKind.SourceConcentrationRegression, fail ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            fail ? $"Channel HHI increases by {increase:0.######}, above tolerance {input.AllowedConcentrationIncrease.Value:0.######}." : $"Channel HHI change {increase:0.######} is within tolerance.");
    }

    private static EconomyHealthInvariantResult Attribution(EconomyHealthInvariantInput input)
    {
        if (!input.AttributionState.HasValue || !input.AttributionConfidence.HasValue || !input.MinimumRequiredAttributionConfidence.HasValue)
            return Result(EconomyHealthInvariantKind.AttributionConfidence, EconomyInvariantState.Unknown, "Attribution evidence or minimum confidence is incomplete.");
        if (input.AttributionState.Value == EconomyAttributionResolutionState.Unknown)
            return Result(EconomyHealthInvariantKind.AttributionConfidence, EconomyInvariantState.Unknown, "Attribution owner is unknown.");
        if (input.AttributionState.Value == EconomyAttributionResolutionState.Conflict)
            return Result(EconomyHealthInvariantKind.AttributionConfidence, EconomyInvariantState.Fail, "Top-confidence attribution claims conflict.");
        var fail = input.AttributionConfidence.Value < input.MinimumRequiredAttributionConfidence.Value;
        return Result(EconomyHealthInvariantKind.AttributionConfidence, fail ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            fail ? $"Attribution confidence {input.AttributionConfidence.Value} is below required {input.MinimumRequiredAttributionConfidence.Value}." : "Attribution confidence is sufficient.");
    }

    private static EconomyHealthInvariantResult Intervention(EconomyHealthInvariantInput input)
    {
        if (!input.ProposedRelativeInterventionMagnitude.HasValue || !input.MaximumAllowedRelativeInterventionMagnitude.HasValue)
            return Result(EconomyHealthInvariantKind.InterventionMagnitude, EconomyInvariantState.Unknown, "Intervention magnitude evidence or bound is incomplete.");
        var fail = input.ProposedRelativeInterventionMagnitude.Value > input.MaximumAllowedRelativeInterventionMagnitude.Value + 0.0000001d;
        return Result(EconomyHealthInvariantKind.InterventionMagnitude, fail ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            fail ? $"Proposed relative intervention {input.ProposedRelativeInterventionMagnitude.Value:0.######} exceeds bound {input.MaximumAllowedRelativeInterventionMagnitude.Value:0.######}." : "Proposed intervention is within the explicit bound.");
    }

    private static EconomyHealthInvariantResult Result(EconomyHealthInvariantKind kind, EconomyInvariantState state, string reason) => new() { Kind = kind, State = state, Reason = reason };
}
