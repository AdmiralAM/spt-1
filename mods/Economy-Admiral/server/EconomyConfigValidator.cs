namespace SPTEconomy;

public static class EconomyConfigValidator
{
    private static readonly HashSet<string> AllowedRarities = new(StringComparer.OrdinalIgnoreCase)
    {
        "Common",
        "Uncommon",
        "Rare",
        "VeryRare",
    };

    public static void Validate(EconomyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.ReportRelativePath))
        {
            throw new InvalidOperationException("Economy Admiral config: ReportRelativePath must not be empty.");
        }

        if (System.IO.Path.IsPathRooted(config.ReportRelativePath)
            || config.ReportRelativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Economy Admiral config: ReportRelativePath must remain inside the mod folder.");
        }

        ValidateRarity(config.Rarity);
        ValidatePolicy(config.CustomAuditPolicy);

        foreach (var (templateId, itemOverride) in config.ManualOverrides)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                throw new InvalidOperationException("Economy Admiral config: ManualOverrides cannot contain an empty template id.");
            }

            if (itemOverride.Rarity is not null && !AllowedRarities.Contains(itemOverride.Rarity))
            {
                throw new InvalidOperationException($"Economy Admiral config: unsupported manual rarity '{itemOverride.Rarity}' for template '{templateId}'.");
            }
        }
    }

    private static void ValidateRarity(RarityThresholds rarity)
    {
        if (rarity.CommonMinSources < 1 || rarity.UncommonMinSources < 1 || rarity.RareMinSources < 1)
        {
            throw new InvalidOperationException("Economy Admiral config: rarity source thresholds must be positive.");
        }

        if (!(rarity.CommonMinSources > rarity.UncommonMinSources && rarity.UncommonMinSources > rarity.RareMinSources))
        {
            throw new InvalidOperationException("Economy Admiral config: rarity thresholds must satisfy Common > Uncommon > Rare.");
        }
    }

    private static void ValidatePolicy(AuditPolicy policy)
    {
        ValidatePositiveFinite(policy.QuestRewardVsVanillaMedianWarnMultiple, nameof(policy.QuestRewardVsVanillaMedianWarnMultiple));
        ValidatePositiveFinite(policy.RestartableRewardVsVanillaMedianWarnMultiple, nameof(policy.RestartableRewardVsVanillaMedianWarnMultiple));
        ValidatePositiveFinite(policy.NormalizedRewardVsVanillaMedianWarnMultiple, nameof(policy.NormalizedRewardVsVanillaMedianWarnMultiple));
        ValidatePositiveFinite(policy.RestartableNormalizedRewardVsVanillaMedianWarnMultiple, nameof(policy.RestartableNormalizedRewardVsVanillaMedianWarnMultiple));
        ValidateNonNegativeFinite(policy.LevelGateWeight, nameof(policy.LevelGateWeight));
        ValidateNonNegativeFinite(policy.ObjectiveConditionWeight, nameof(policy.ObjectiveConditionWeight));
        ValidateNonNegativeFinite(policy.MaxLevelGateContribution, nameof(policy.MaxLevelGateContribution));
        ValidateNonNegativeFinite(policy.MaxObjectiveContribution, nameof(policy.MaxObjectiveContribution));
        ValidatePositiveFinite(policy.HighItemValueLowStructureWarnMultiple, nameof(policy.HighItemValueLowStructureWarnMultiple));
        ValidatePositiveFinite(policy.HighXpLowDepthWarnMultiple, nameof(policy.HighXpLowDepthWarnMultiple));
        ValidatePositiveFinite(policy.HighStandingLowDepthWarnMultiple, nameof(policy.HighStandingLowDepthWarnMultiple));
        ValidatePositiveFinite(policy.RestartableHighItemValueWarnMultiple, nameof(policy.RestartableHighItemValueWarnMultiple));
        ValidatePositiveFinite(policy.RestartableHighXpWarnMultiple, nameof(policy.RestartableHighXpWarnMultiple));
        ValidatePositiveFinite(policy.LowDepthMaxRelativeMultiple, nameof(policy.LowDepthMaxRelativeMultiple));
        ValidatePositiveFinite(policy.LowStructureMaxRelativeMultiple, nameof(policy.LowStructureMaxRelativeMultiple));

        if (policy.DuplicateTraderSourcesWarnCount < 1)
        {
            throw new InvalidOperationException("Economy Admiral config: DuplicateTraderSourcesWarnCount must be positive.");
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
