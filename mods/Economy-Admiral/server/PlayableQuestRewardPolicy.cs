namespace SPTEconomy;

public static class PlayableQuestRewardPolicy
{
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
            LowDepthMaxRelativeMultiple = source.LowDepthMaxRelativeMultiple,
            LowStructureMaxRelativeMultiple = source.LowStructureMaxRelativeMultiple,
        };

        var quests = analysis.Quests
            .Select(row => row with
            {
                ObservationalFlags = row.ObservationalFlags
                    .Where(flag => AutomaticFlagEnabled(config, row, flag))
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
            Note = $"{analysis.Note} Enforcement uses {playable.PolicyId} caps independently of observational outlier thresholds. " +
                   $"Automatic quest mechanisms: items={config.EnableItemRewardStackNormalization}, xp={config.EnableQuestXpPressure}, " +
                   $"standing={config.EnableQuestStandingPressure}, restartable={config.EnableRestartableQuestPressure}.",
        };
    }

    internal static bool AutomaticFlagEnabled(EconomyConfig config, QuestAnalysisRow row, string flag)
    {
        if (row.Restartable && !config.EnableRestartableQuestPressure && IsRewardPressureFlag(flag))
            return false;

        return flag switch
        {
            "HIGH_ITEM_VALUE_LOW_STRUCTURE" or "RESTARTABLE_HIGH_ITEM_VALUE" => config.EnableItemRewardStackNormalization,
            "HIGH_XP_LOW_DEPTH" or "RESTARTABLE_HIGH_XP" => config.EnableQuestXpPressure,
            "HIGH_STANDING_LOW_DEPTH" => config.EnableQuestStandingPressure,
            _ => true,
        };
    }

    private static bool IsRewardPressureFlag(string flag) => flag is
        "HIGH_ITEM_VALUE_LOW_STRUCTURE" or
        "RESTARTABLE_HIGH_ITEM_VALUE" or
        "HIGH_XP_LOW_DEPTH" or
        "RESTARTABLE_HIGH_XP" or
        "HIGH_STANDING_LOW_DEPTH";
}
