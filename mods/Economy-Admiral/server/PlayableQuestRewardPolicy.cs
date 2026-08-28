namespace SPTEconomy;

public sealed record PlayableQuestRewardPolicy(
    string PolicyId,
    double ItemBudgetMultiple,
    double RestartableItemBudgetMultiple,
    double XpMultiple,
    double RestartableXpMultiple,
    double StandingMultiple)
{
    public static PlayableQuestRewardPolicy Resolve(EconomyPreset preset, AuditPolicy custom)
    {
        ArgumentNullException.ThrowIfNull(custom);
        return preset switch
        {
            EconomyPreset.Easy => new("PlayableQuestRewardPolicyV1/Easy", 2.25, 1.75, 2.25, 1.75, 2.25),
            EconomyPreset.Normal => new("PlayableQuestRewardPolicyV1/Normal", 1.50, 1.15, 1.50, 1.15, 1.50),
            EconomyPreset.Hard => new("PlayableQuestRewardPolicyV1/Hard", 1.10, 1.00, 1.10, 1.00, 1.10),
            EconomyPreset.Custom => Validate(new(
                "PlayableQuestRewardPolicyV1/Custom",
                custom.HighItemValueLowStructureWarnMultiple,
                custom.RestartableHighItemValueWarnMultiple,
                custom.HighXpLowDepthWarnMultiple,
                custom.RestartableHighXpWarnMultiple,
                custom.HighStandingLowDepthWarnMultiple)),
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Unsupported Economy Admiral preset."),
        };
    }

    public static QuestAnalysisReport ApplyToEnforcement(EconomyConfig config, QuestAnalysisReport analysis)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(analysis);
        var playable = Resolve(config.Preset, config.CustomAuditPolicy);
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

        return analysis with
        {
            Policy = enforcementPolicy,
            Note = $"{analysis.Note} Enforcement uses {playable.PolicyId} caps independently of observational outlier thresholds.",
        };
    }

    public double ItemMultiple(bool restartable) => restartable ? RestartableItemBudgetMultiple : ItemBudgetMultiple;
    public double ExperienceMultiple(bool restartable) => restartable ? RestartableXpMultiple : XpMultiple;

    private static PlayableQuestRewardPolicy Validate(PlayableQuestRewardPolicy policy)
    {
        foreach (var value in new[]
                 {
                     policy.ItemBudgetMultiple,
                     policy.RestartableItemBudgetMultiple,
                     policy.XpMultiple,
                     policy.RestartableXpMultiple,
                     policy.StandingMultiple,
                 })
        {
            if (!double.IsFinite(value) || value <= 0)
                throw new InvalidOperationException("Economy Admiral playable quest reward multipliers must be finite and positive.");
        }
        return policy;
    }
}
