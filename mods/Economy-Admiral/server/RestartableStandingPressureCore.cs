namespace SPTEconomy;

public static class RestartableStandingPressureCore
{
    public const string Flag = "RESTARTABLE_HIGH_STANDING";

    public static double ResolveThreshold(PlayableQuestRewardCaps policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var threshold = Math.Min(policy.StandingMultiple, policy.RestartableXpMultiple);
        if (!double.IsFinite(threshold) || threshold <= 0)
            throw new InvalidOperationException("Economy Admiral restartable standing threshold must be finite and > 0.");
        return threshold;
    }

    public static bool ShouldFlag(bool restartable, double? standingVsVanillaMedian, double threshold)
    {
        if (!double.IsFinite(threshold) || threshold <= 0)
            throw new ArgumentOutOfRangeException(nameof(threshold), "Restartable standing threshold must be finite and > 0.");
        return restartable
            && standingVsVanillaMedian is { } ratio
            && double.IsFinite(ratio)
            && ratio >= threshold;
    }
}
