namespace SPTEconomy;

public static class PlayableQuestRewardPolicy
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

    public static QuestAnalysisReport ApplyToEnforcement(EconomyConfig config, QuestAnalysisReport analysis)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(analysis);

        if (!config.EnableQuestEconomyCluster)
        {
            return analysis with
            {
                Quests = analysis.Quests
                    .Select(row => row with { ObservationalFlags = [] })
                    .ToList(),
                FlagCounts = new Dictionary<string, int>(StringComparer.Ordinal),
                Note = $"{analysis.Note} Quest economy cluster is disabled; quest reward enforcement is bypassed while observational analysis remains available upstream.",
            };
        }

        var playable = PlayableQuestRewardCaps.Resolve(config);
        var source = analysis.Policy;
        var enforcementPolicy = new AuditPolicy
        {
            QuestRewardVsVanillaMedianWarnMultiple = source.QuestRewardVsVanillaMedianWarnMultiple,
            RestartableRewardVsVanillaMedianWarnMultiple = source.RestartableRewardVsVanillaMedianWarnMultiple,
            NormalizedRewardVsVanillaMedianWarnMultiple = source.NormalizedRewardVsVanillaMedianWarnMultiple,
            RestartableNormalizedRewardVsVanillaMedianWarnMultiple = source.RestartableNormalizedRewardVsVanillaMedianWarnMultiple,
            LevelGateWeight = source.LevelGateWeight,
            ObjectiveConditionWeight = source.ObjectiveConditionWeight,
            MaxLevelGateContribution = source.MaxLevelGateContribution,
            MaxObjectiveContribution = source.MaxObjectiveContribution,
            DuplicateTraderSourcesWarnCount = source.DuplicateTraderSourcesWarnCount,
            HighItemValueLowStructureWarnMultiple = playable.ItemBudgetMultiple,
            HighXpLowDepthWarnMultiple = playable.XpMultiple,
            HighStandingLowDepthWarnMultiple = playable.StandingMultiple,
            RestartableHighItemValueWarnMultiple = playable.RestartableItemBudgetMultiple,
            RestartableHighXpWarnMultiple = playable.RestartableXpMultiple,
            RestartableHighStandingWarnMultiple = RestartableStandingPressureCore.ResolveThreshold(playable),
            LowDepthMaxRelativeMultiple = source.LowDepthMaxRelativeMultiple,
            LowStructureMaxRelativeMultiple = source.LowStructureMaxRelativeMultiple,
        };

        var quests = analysis.Quests
            .Select(row => row with
            {
                ObservationalFlags = ReclassifyRewardPressureFlags(row, enforcementPolicy)
                    .Where(flag => QuestMechanismGate.AutomaticFlagEnabled(config, row.Restartable, flag))
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            })
            .ToList();
        var flagCounts = quests
            .SelectMany(row => row.ObservationalFlags)
            .GroupBy(flag => flag, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return analysis with
        {
            Policy = enforcementPolicy,
            Quests = quests,
            FlagCounts = flagCounts,
            Note = $"{analysis.Note} Enforcement uses {playable.PolicyId} caps independently of observational outlier thresholds and reclassifies reward-pressure flags against those caps before mutation planning. " +
                   $"Automatic quest mechanisms: items={config.EnableItemRewardStackNormalization}, xp={config.EnableQuestXpPressure}, " +
                   $"standing={config.EnableQuestStandingPressure}, restartable={config.EnableRestartableQuestPressure}.",
        };
    }

    public static IReadOnlyList<string> ReclassifyRewardPressureFlags(QuestAnalysisRow row, AuditPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(policy);

        var flags = row.ObservationalFlags
            .Where(flag => !RewardPressureFlags.Contains(flag))
            .ToList();
        var lowDepth = row.PrerequisiteDepthVsVanillaMedian is null
            || row.PrerequisiteDepthVsVanillaMedian <= policy.LowDepthMaxRelativeMultiple;
        var lowStructure = row.StructuredConstraintsVsVanillaMedian is null
            || row.StructuredConstraintsVsVanillaMedian <= policy.LowStructureMaxRelativeMultiple;

        if (row.HandbookValueVsVanillaMedian >= policy.HighItemValueLowStructureWarnMultiple && lowDepth && lowStructure)
            flags.Add("HIGH_ITEM_VALUE_LOW_STRUCTURE");
        if (row.XpVsVanillaMedian >= policy.HighXpLowDepthWarnMultiple && lowDepth)
            flags.Add("HIGH_XP_LOW_DEPTH");
        if (row.StandingVsVanillaMedian >= policy.HighStandingLowDepthWarnMultiple && lowDepth)
            flags.Add("HIGH_STANDING_LOW_DEPTH");
        if (row.Restartable && row.HandbookValueVsVanillaMedian >= policy.RestartableHighItemValueWarnMultiple)
            flags.Add("RESTARTABLE_HIGH_ITEM_VALUE");
        if (row.Restartable && row.XpVsVanillaMedian >= policy.RestartableHighXpWarnMultiple)
            flags.Add("RESTARTABLE_HIGH_XP");
        if (RestartableStandingPressureCore.ShouldFlag(
                row.Restartable,
                row.StandingVsVanillaMedian,
                policy.RestartableHighStandingWarnMultiple))
        {
            flags.Add(RestartableStandingPressureCore.Flag);
        }

        return flags.Distinct(StringComparer.Ordinal).ToList();
    }
}
