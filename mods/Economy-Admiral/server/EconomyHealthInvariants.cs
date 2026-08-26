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
    RenewablePathContinuity,
    ProgressionAccessRegression,
    SourceConcentrationRegression,
    AttributionConfidence,
}

public sealed record EconomyHealthInvariantInput
{
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string Dimension { get; init; }
    public required bool PristineUntouched { get; init; }
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
}

public sealed record EconomyHealthInvariantResult
{
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string Dimension { get; init; }
    public required EconomyHealthInvariantKind Kind { get; init; }
    public required EconomyInvariantState State { get; init; }
    public required string Reason { get; init; }
}

public sealed record EconomyHealthInvariantEvaluation
{
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string Dimension { get; init; }
    public required List<EconomyHealthInvariantResult> Invariants { get; init; }
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

        var results = new List<EconomyHealthInvariantResult>
        {
            EvaluateProtectedPristine(input),
            EvaluateRenewableContinuity(input),
            EvaluateProgression(input),
            EvaluateConcentration(input),
            EvaluateAttribution(input),
        };

        var hasFailure = results.Any(result => result.State == EconomyInvariantState.Fail);
        var hasUnknown = results.Any(result => result.State == EconomyInvariantState.Unknown);

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
        if (string.IsNullOrWhiteSpace(input.SubjectType))
        {
            throw new InvalidOperationException("Economy Admiral health: subject type must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(input.SubjectId))
        {
            throw new InvalidOperationException("Economy Admiral health: subject id must not be empty.");
        }
        if (string.IsNullOrWhiteSpace(input.Dimension))
        {
            throw new InvalidOperationException("Economy Admiral health: dimension must not be empty.");
        }
        if (input.EarliestProgressionLevelBefore is < 1 || input.EarliestProgressionLevelAfter is < 1)
        {
            throw new InvalidOperationException("Economy Admiral health: progression levels must be >= 1 when known.");
        }
        if (input.AllowedProgressionDelay is < 0)
        {
            throw new InvalidOperationException("Economy Admiral health: allowed progression delay must be non-negative.");
        }
        ValidateProbability(input.ChannelConcentrationHhiBefore, nameof(input.ChannelConcentrationHhiBefore));
        ValidateProbability(input.ChannelConcentrationHhiAfter, nameof(input.ChannelConcentrationHhiAfter));
        if (input.AllowedConcentrationIncrease is < 0 or > 1 || (input.AllowedConcentrationIncrease.HasValue && !double.IsFinite(input.AllowedConcentrationIncrease.Value)))
        {
            throw new InvalidOperationException("Economy Admiral health: allowed concentration increase must be finite and between 0 and 1.");
        }
        if (input.AttributionConfidence.HasValue && !Enum.IsDefined(input.AttributionConfidence.Value))
        {
            throw new InvalidOperationException("Economy Admiral health: attribution confidence is invalid.");
        }
        if (input.MinimumRequiredAttributionConfidence.HasValue && !Enum.IsDefined(input.MinimumRequiredAttributionConfidence.Value))
        {
            throw new InvalidOperationException("Economy Admiral health: minimum attribution confidence is invalid.");
        }
        if (input.AttributionState.HasValue && !Enum.IsDefined(input.AttributionState.Value))
        {
            throw new InvalidOperationException("Economy Admiral health: attribution state is invalid.");
        }
    }

    private static void ValidateProbability(double? value, string name)
    {
        if (!value.HasValue)
        {
            return;
        }
        if (!double.IsFinite(value.Value) || value.Value < 0 || value.Value > 1)
        {
            throw new InvalidOperationException($"Economy Admiral health: {name} must be finite and between 0 and 1.");
        }
    }

    private static EconomyHealthInvariantResult EvaluateProtectedPristine(EconomyHealthInvariantInput input)
        => Result(
            input,
            EconomyHealthInvariantKind.ProtectedPristine,
            input.PristineUntouched ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            input.PristineUntouched
                ? "Target dimension is untouched pristine content and remains protected."
                : "Target dimension is not classified as untouched pristine content."
        );

    private static EconomyHealthInvariantResult EvaluateRenewableContinuity(EconomyHealthInvariantInput input)
    {
        if (!input.HadRenewablePathBefore.HasValue || !input.HasRenewablePathAfter.HasValue)
        {
            return Result(input, EconomyHealthInvariantKind.RenewablePathContinuity, EconomyInvariantState.Unknown, "Renewable-path evidence is incomplete.");
        }

        var removedLastPath = input.HadRenewablePathBefore.Value && !input.HasRenewablePathAfter.Value;
        return Result(
            input,
            EconomyHealthInvariantKind.RenewablePathContinuity,
            removedLastPath ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            removedLastPath ? "Candidate removes the last known renewable acquisition path." : "Renewable-path continuity is preserved."
        );
    }

    private static EconomyHealthInvariantResult EvaluateProgression(EconomyHealthInvariantInput input)
    {
        if (!input.EarliestProgressionLevelBefore.HasValue || !input.EarliestProgressionLevelAfter.HasValue || !input.AllowedProgressionDelay.HasValue)
        {
            return Result(input, EconomyHealthInvariantKind.ProgressionAccessRegression, EconomyInvariantState.Unknown, "Progression comparison evidence or tolerance is incomplete.");
        }

        var delay = input.EarliestProgressionLevelAfter.Value - input.EarliestProgressionLevelBefore.Value;
        var failed = delay > input.AllowedProgressionDelay.Value;
        return Result(
            input,
            EconomyHealthInvariantKind.ProgressionAccessRegression,
            failed ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            failed
                ? $"Candidate delays earliest known access by {delay} levels, above tolerance {input.AllowedProgressionDelay.Value}."
                : $"Earliest known access delay {delay} is within tolerance {input.AllowedProgressionDelay.Value}."
        );
    }

    private static EconomyHealthInvariantResult EvaluateConcentration(EconomyHealthInvariantInput input)
    {
        if (!input.ChannelConcentrationHhiBefore.HasValue || !input.ChannelConcentrationHhiAfter.HasValue || !input.AllowedConcentrationIncrease.HasValue)
        {
            return Result(input, EconomyHealthInvariantKind.SourceConcentrationRegression, EconomyInvariantState.Unknown, "Source-concentration evidence or tolerance is incomplete.");
        }

        var increase = input.ChannelConcentrationHhiAfter.Value - input.ChannelConcentrationHhiBefore.Value;
        var failed = increase > input.AllowedConcentrationIncrease.Value;
        return Result(
            input,
            EconomyHealthInvariantKind.SourceConcentrationRegression,
            failed ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            failed
                ? $"Channel concentration increases by {increase:0.######}, above tolerance {input.AllowedConcentrationIncrease.Value:0.######}."
                : $"Channel concentration change {increase:0.######} is within tolerance {input.AllowedConcentrationIncrease.Value:0.######}."
        );
    }

    private static EconomyHealthInvariantResult EvaluateAttribution(EconomyHealthInvariantInput input)
    {
        if (!input.AttributionState.HasValue || !input.AttributionConfidence.HasValue || !input.MinimumRequiredAttributionConfidence.HasValue)
        {
            return Result(input, EconomyHealthInvariantKind.AttributionConfidence, EconomyInvariantState.Unknown, "Attribution evidence or required confidence is incomplete.");
        }

        if (input.AttributionState.Value == EconomyAttributionResolutionState.Unknown)
        {
            return Result(input, EconomyHealthInvariantKind.AttributionConfidence, EconomyInvariantState.Unknown, "Attribution owner is unknown.");
        }

        if (input.AttributionState.Value == EconomyAttributionResolutionState.Conflict)
        {
            return Result(input, EconomyHealthInvariantKind.AttributionConfidence, EconomyInvariantState.Fail, "Attribution evidence contains a top-confidence owner conflict.");
        }

        var failed = input.AttributionConfidence.Value < input.MinimumRequiredAttributionConfidence.Value;
        return Result(
            input,
            EconomyHealthInvariantKind.AttributionConfidence,
            failed ? EconomyInvariantState.Fail : EconomyInvariantState.Pass,
            failed
                ? $"Attribution confidence {input.AttributionConfidence.Value} is below required {input.MinimumRequiredAttributionConfidence.Value}."
                : $"Attribution confidence {input.AttributionConfidence.Value} satisfies required {input.MinimumRequiredAttributionConfidence.Value}."
        );
    }

    private static EconomyHealthInvariantResult Result(
        EconomyHealthInvariantInput input,
        EconomyHealthInvariantKind kind,
        EconomyInvariantState state,
        string reason
    ) => new()
    {
        SubjectType = input.SubjectType.Trim(),
        SubjectId = input.SubjectId.Trim(),
        Dimension = input.Dimension.Trim(),
        Kind = kind,
        State = state,
        Reason = reason,
    };
}
