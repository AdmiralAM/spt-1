namespace SPTEconomy;

public enum EconomyPolicyPreviewDisposition
{
    Previewable,
    Blocked,
    IncompleteEvidence,
}

public sealed record EconomyPolicyPreviewCandidateInput
{
    public required string CandidateId { get; init; }
    public required string PolicyId { get; init; }
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string Dimension { get; init; }
    public required string AnomalyReason { get; init; }
    public required string BaselineSource { get; init; }
    public required double CurrentValue { get; init; }
    public required double TargetValue { get; init; }
    public required double ProjectedValue { get; init; }
    public required EconomyHealthInvariantEvaluation Health { get; init; }
}

public sealed record EconomyPolicyPreviewDecision
{
    public required string CandidateId { get; init; }
    public required string PolicyId { get; init; }
    public required string SubjectType { get; init; }
    public required string SubjectId { get; init; }
    public required string Dimension { get; init; }
    public required string AnomalyReason { get; init; }
    public required string BaselineSource { get; init; }
    public required double CurrentValue { get; init; }
    public required double TargetValue { get; init; }
    public required double ProjectedValue { get; init; }
    public required double ProjectedDelta { get; init; }
    public required bool IsNoOp { get; init; }
    public required EconomyPolicyPreviewDisposition Disposition { get; init; }
    public required List<EconomyHealthInvariantResult> HealthInvariants { get; init; }
    public required List<string> BlockingReasons { get; init; }
    public required List<string> UnknownReasons { get; init; }
    public required bool MutationAuthorized { get; init; }
}

public static class EconomyPolicyPreviewEvidenceBuilder
{
    public static EconomyPolicyPreviewDecision Build(EconomyPolicyPreviewCandidateInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        Validate(input);

        var orderedInvariants = input.Health.Invariants
            .OrderBy(result => result.Kind)
            .ThenBy(result => result.Reason, StringComparer.Ordinal)
            .ToList();

        var blockingReasons = orderedInvariants
            .Where(result => result.State == EconomyInvariantState.Fail)
            .Select(result => $"{result.Kind}: {result.Reason}")
            .ToList();

        var unknownReasons = orderedInvariants
            .Where(result => result.State == EconomyInvariantState.Unknown)
            .Select(result => $"{result.Kind}: {result.Reason}")
            .ToList();

        var disposition = blockingReasons.Count > 0
            ? EconomyPolicyPreviewDisposition.Blocked
            : unknownReasons.Count > 0
                ? EconomyPolicyPreviewDisposition.IncompleteEvidence
                : EconomyPolicyPreviewDisposition.Previewable;

        return new EconomyPolicyPreviewDecision
        {
            CandidateId = input.CandidateId.Trim(),
            PolicyId = input.PolicyId.Trim(),
            SubjectType = input.SubjectType.Trim(),
            SubjectId = input.SubjectId.Trim(),
            Dimension = input.Dimension.Trim(),
            AnomalyReason = input.AnomalyReason.Trim(),
            BaselineSource = input.BaselineSource.Trim(),
            CurrentValue = input.CurrentValue,
            TargetValue = input.TargetValue,
            ProjectedValue = input.ProjectedValue,
            ProjectedDelta = input.ProjectedValue - input.CurrentValue,
            IsNoOp = input.ProjectedValue == input.CurrentValue,
            Disposition = disposition,
            HealthInvariants = orderedInvariants,
            BlockingReasons = blockingReasons,
            UnknownReasons = unknownReasons,
            MutationAuthorized = false,
        };
    }

    private static void Validate(EconomyPolicyPreviewCandidateInput input)
    {
        RequireText(input.CandidateId, nameof(input.CandidateId));
        RequireText(input.PolicyId, nameof(input.PolicyId));
        RequireText(input.SubjectType, nameof(input.SubjectType));
        RequireText(input.SubjectId, nameof(input.SubjectId));
        RequireText(input.Dimension, nameof(input.Dimension));
        RequireText(input.AnomalyReason, nameof(input.AnomalyReason));
        RequireText(input.BaselineSource, nameof(input.BaselineSource));

        ValidateFinite(input.CurrentValue, nameof(input.CurrentValue));
        ValidateFinite(input.TargetValue, nameof(input.TargetValue));
        ValidateFinite(input.ProjectedValue, nameof(input.ProjectedValue));

        if (input.Health is null)
        {
            throw new InvalidOperationException("Economy Admiral policy preview: health evaluation must be supplied.");
        }

        if (!string.Equals(input.SubjectType.Trim(), input.Health.SubjectType, StringComparison.Ordinal)
            || !string.Equals(input.SubjectId.Trim(), input.Health.SubjectId, StringComparison.Ordinal)
            || !string.Equals(input.Dimension.Trim(), input.Health.Dimension, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Economy Admiral policy preview: health evidence does not match the candidate subject/dimension.");
        }

        if (input.Health.Invariants is null || input.Health.Invariants.Count == 0)
        {
            throw new InvalidOperationException("Economy Admiral policy preview: health evidence must contain invariants.");
        }

        var hasFailure = input.Health.Invariants.Any(result => result.State == EconomyInvariantState.Fail);
        var hasUnknown = input.Health.Invariants.Any(result => result.State == EconomyInvariantState.Unknown);
        if (input.Health.HasFailure != hasFailure
            || input.Health.HasUnknown != hasUnknown
            || input.Health.FutureAutomaticActionBlocked != (hasFailure || hasUnknown))
        {
            throw new InvalidOperationException("Economy Admiral policy preview: health summary flags conflict with invariant states.");
        }
    }

    private static void RequireText(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Economy Admiral policy preview: {name} must not be empty.");
        }
    }

    private static void ValidateFinite(double value, string name)
    {
        if (!double.IsFinite(value))
        {
            throw new InvalidOperationException($"Economy Admiral policy preview: {name} must be finite.");
        }
    }
}
