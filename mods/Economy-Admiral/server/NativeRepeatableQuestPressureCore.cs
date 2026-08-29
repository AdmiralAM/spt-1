namespace SPTEconomy;

public static class NativeRepeatableQuestPressureCore
{
    public static double ResolveStandingMultiple(PlayableQuestRewardCaps caps)
    {
        ArgumentNullException.ThrowIfNull(caps);
        return Math.Min(caps.StandingMultiple, caps.RestartableXpMultiple);
    }

    public static double Cap(double current, double pristine, double multiple)
    {
        if (!double.IsFinite(current) || current < 0)
            throw new ArgumentOutOfRangeException(nameof(current), "Current repeatable reward value must be finite and >= 0.");
        if (!double.IsFinite(pristine) || pristine < 0)
            throw new ArgumentOutOfRangeException(nameof(pristine), "Pristine repeatable reward value must be finite and >= 0.");
        if (!double.IsFinite(multiple) || multiple <= 0)
            throw new ArgumentOutOfRangeException(nameof(multiple), "Repeatable reward multiple must be finite and > 0.");

        return Math.Min(current, pristine * multiple);
    }

    public static bool NeedsMutation(double current, double target) =>
        double.IsFinite(current)
        && double.IsFinite(target)
        && target >= 0
        && target + 0.0000001 < current;

    public static bool Compatible(IReadOnlyList<double> pristine, IList<double> current) =>
        pristine.Count > 0 && pristine.Count == current.Count;
}

public sealed record NativeRepeatableRewardBaseline(
    string Key,
    string Name,
    IReadOnlyList<double> Experience,
    IReadOnlyList<double> Roubles,
    IReadOnlyList<double> GpCoins,
    IReadOnlyList<double> Items,
    IReadOnlyList<double> Reputation);

public sealed record NativeRepeatableMutation(
    string RepeatableKey,
    string RepeatableName,
    string Dimension,
    int TierIndex,
    double Before,
    double Target,
    bool Applied);

public sealed record NativeRepeatablePressureResult
{
    public required bool Enabled { get; init; }
    public required int PlannedMutationCount { get; init; }
    public required int MutationCount { get; init; }
    public required int BlockedDimensionCount { get; init; }
    public required IReadOnlyList<NativeRepeatableMutation> Mutations { get; init; }
}
