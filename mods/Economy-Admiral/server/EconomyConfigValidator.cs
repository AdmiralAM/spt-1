namespace SPTEconomy;

public static class EconomyConfigValidator
{
    private static readonly HashSet<string> AllowedRarities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Common", "Uncommon", "Rare", "Exceptional",
    };

    public static void Validate(EconomyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.RepeatedRaidLootDecay)
        {
            throw new InvalidOperationException("Economy Admiral config: RepeatedRaidLootDecay is not implemented and must remain false.");
        }
        if (string.IsNullOrWhiteSpace(config.ReportRelativePath))
        {
            throw new InvalidOperationException("Economy Admiral config: ReportRelativePath must not be empty.");
        }
        if (Path.IsPathRooted(config.ReportRelativePath)
            || config.ReportRelativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Economy Admiral config: ReportRelativePath must remain inside the mod folder.");
        }
        if (config.Rarity is null || config.CustomAuditPolicy is null || config.ManualOverrides is null || config.QuestRewardOverrides is null)
        {
            throw new InvalidOperationException("Economy Admiral config: Rarity, CustomAuditPolicy, ManualOverrides and QuestRewardOverrides must be objects.");
        }

        if (config.Rarity.CommonMinSources < 1 || config.Rarity.UncommonMinSources < 1 || config.Rarity.RareMinSources < 1
            || !(config.Rarity.CommonMinSources > config.Rarity.UncommonMinSources && config.Rarity.UncommonMinSources > config.Rarity.RareMinSources))
        {
            throw new InvalidOperationException("Economy Admiral config: rarity thresholds must be positive and satisfy Common > Uncommon > Rare.");
        }

        ValidatePositiveFinite(config.CustomAuditPolicy.QuestRewardVsVanillaMedianWarnMultiple, nameof(config.CustomAuditPolicy.QuestRewardVsVanillaMedianWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.RestartableRewardVsVanillaMedianWarnMultiple, nameof(config.CustomAuditPolicy.RestartableRewardVsVanillaMedianWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.NormalizedRewardVsVanillaMedianWarnMultiple, nameof(config.CustomAuditPolicy.NormalizedRewardVsVanillaMedianWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.RestartableNormalizedRewardVsVanillaMedianWarnMultiple, nameof(config.CustomAuditPolicy.RestartableNormalizedRewardVsVanillaMedianWarnMultiple));
        ValidateNonNegativeFinite(config.CustomAuditPolicy.LevelGateWeight, nameof(config.CustomAuditPolicy.LevelGateWeight));
        ValidateNonNegativeFinite(config.CustomAuditPolicy.ObjectiveConditionWeight, nameof(config.CustomAuditPolicy.ObjectiveConditionWeight));
        ValidateNonNegativeFinite(config.CustomAuditPolicy.MaxLevelGateContribution, nameof(config.CustomAuditPolicy.MaxLevelGateContribution));
        ValidateNonNegativeFinite(config.CustomAuditPolicy.MaxObjectiveContribution, nameof(config.CustomAuditPolicy.MaxObjectiveContribution));
        ValidatePositiveFinite(config.CustomAuditPolicy.HighItemValueLowStructureWarnMultiple, nameof(config.CustomAuditPolicy.HighItemValueLowStructureWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.HighXpLowDepthWarnMultiple, nameof(config.CustomAuditPolicy.HighXpLowDepthWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.HighStandingLowDepthWarnMultiple, nameof(config.CustomAuditPolicy.HighStandingLowDepthWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.RestartableHighItemValueWarnMultiple, nameof(config.CustomAuditPolicy.RestartableHighItemValueWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.RestartableHighXpWarnMultiple, nameof(config.CustomAuditPolicy.RestartableHighXpWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.LowDepthMaxRelativeMultiple, nameof(config.CustomAuditPolicy.LowDepthMaxRelativeMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.LowStructureMaxRelativeMultiple, nameof(config.CustomAuditPolicy.LowStructureMaxRelativeMultiple));
        if (config.CustomAuditPolicy.DuplicateTraderSourcesWarnCount < 1)
        {
            throw new InvalidOperationException("Economy Admiral config: DuplicateTraderSourcesWarnCount must be positive.");
        }

        foreach (var (templateId, itemOverride) in config.ManualOverrides)
        {
            if (string.IsNullOrWhiteSpace(templateId) || itemOverride is null)
            {
                throw new InvalidOperationException("Economy Admiral config: manual overrides require non-empty template ids and object values.");
            }
            if (itemOverride.Rarity is not null && !AllowedRarities.Contains(itemOverride.Rarity))
            {
                throw new InvalidOperationException($"Economy Admiral config: unsupported manual rarity '{itemOverride.Rarity}' for template '{templateId}'.");
            }
        }

        foreach (var (questId, questOverride) in config.QuestRewardOverrides)
        {
            if (string.IsNullOrWhiteSpace(questId) || questOverride is null)
            {
                throw new InvalidOperationException("Economy Admiral config: quest reward overrides require non-empty quest ids and object values.");
            }
            if (questOverride.ExperienceTarget is { } xp)
            {
                ValidateNonNegativeFinite(xp, $"QuestRewardOverrides[{questId}].ExperienceTarget");
            }
            if (questOverride.TraderStandingTarget is { } standing && !double.IsFinite(standing))
            {
                throw new InvalidOperationException($"Economy Admiral config: QuestRewardOverrides[{questId}].TraderStandingTarget must be finite.");
            }
        }
    }

    private static void ValidatePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new InvalidOperationException($"Economy Admiral config: {name} must be finite and > 0.");
        }
    }

    private static void ValidateNonNegativeFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new InvalidOperationException($"Economy Admiral config: {name} must be finite and >= 0.");
        }
    }
}
