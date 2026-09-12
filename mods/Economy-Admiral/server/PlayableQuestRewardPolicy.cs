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
            RestartableHighStandingWarnMultiple = RestartableStandingPressureCore.ResolveThreshold(playable),
            LowDepthMaxRelativeMultiple = source.LowDepthMaxRelativeMultiple,
            LowStructureMaxRelativeMultiple = source.LowStructureMaxRelativeMultiple,
        };

        var quests = analysis.Quests
            .Select(row => row with
            {
                ObservationalFlags = QuestRewardPressureClassifier.Reclassify(new QuestRewardPressureSignals
                    {
                        Restartable = row.Restartable,
                        HandbookValueVsVanillaMedian = row.HandbookValueVsVanillaMedian,
                        XpVsVanillaMedian = row.XpVsVanillaMedian,
                        StandingVsVanillaMedian = row.StandingVsVanillaMedian,
                        PrerequisiteDepthVsVanillaMedian = row.PrerequisiteDepthVsVanillaMedian,
                        StructuredConstraintsVsVanillaMedian = row.StructuredConstraintsVsVanillaMedian,
                        ExistingFlags = row.ObservationalFlags,
                    }, enforcementPolicy)
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
}
