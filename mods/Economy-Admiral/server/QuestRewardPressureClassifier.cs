namespace SPTEconomy;

public sealed record QuestRewardPressureSignals
{
    public required bool Restartable { get; init; }
    public double? HandbookValueVsVanillaMedian { get; init; }
    public double? XpVsVanillaMedian { get; init; }
    public double? StandingVsVanillaMedian { get; init; }
    public double? PrerequisiteDepthVsVanillaMedian { get; init; }
    public double? StructuredConstraintsVsVanillaMedian { get; init; }
    public required IReadOnlyList<string> ExistingFlags { get; init; }
}

public static class QuestRewardPressureClassifier
{
    private static readonly HashSet<string> RewardPressureFlags = new(StringComparer.Ordinal)
    {
        "HIGH_ITEM_VALUE_LOW_STRUCTURE",
        "RESTARTABLE_HIGH_ITEM_VALUE",
        "HIGH_XP_LOW_DEPTH",
        "RESTARTABLE_HIGH_XP",
        "HIGH_STANDING_LOW_DEPTH",
        RestartableStandingPressureCore.Flag,
    };

    public static IReadOnlyList<string> Reclassify(QuestRewardPressureSignals signals, AuditPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(policy);

        var flags = signals.ExistingFlags
            .Where(flag => !RewardPressureFlags.Contains(flag))
            .ToList();
        var lowDepth = signals.PrerequisiteDepthVsVanillaMedian is null
            || signals.PrerequisiteDepthVsVanillaMedian <= policy.LowDepthMaxRelativeMultiple;
        var lowStructure = signals.StructuredConstraintsVsVanillaMedian is null
            || signals.StructuredConstraintsVsVanillaMedian <= policy.LowStructureMaxRelativeMultiple;

        if (signals.HandbookValueVsVanillaMedian >= policy.HighItemValueLowStructureWarnMultiple && lowDepth && lowStructure)
            flags.Add("HIGH_ITEM_VALUE_LOW_STRUCTURE");
        if (signals.XpVsVanillaMedian >= policy.HighXpLowDepthWarnMultiple && lowDepth)
            flags.Add("HIGH_XP_LOW_DEPTH");
        if (signals.StandingVsVanillaMedian >= policy.HighStandingLowDepthWarnMultiple && lowDepth)
            flags.Add("HIGH_STANDING_LOW_DEPTH");
        if (signals.Restartable && signals.HandbookValueVsVanillaMedian >= policy.RestartableHighItemValueWarnMultiple)
            flags.Add("RESTARTABLE_HIGH_ITEM_VALUE");
        if (signals.Restartable && signals.XpVsVanillaMedian >= policy.RestartableHighXpWarnMultiple)
            flags.Add("RESTARTABLE_HIGH_XP");
        foreach (var flag in RestartableStandingPressureCore.EnforcementFlags(
                     signals.Restartable,
                     signals.StandingVsVanillaMedian,
                     policy.RestartableHighStandingWarnMultiple))
        {
            flags.Add(flag);
        }

        return flags.Distinct(StringComparer.Ordinal).ToList();
    }
}
