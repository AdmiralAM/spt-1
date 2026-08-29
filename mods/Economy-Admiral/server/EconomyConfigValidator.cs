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
            throw new InvalidOperationException("Economy Admiral config: RepeatedRaidLootDecay is not implemented and must remain false.");
        if (string.IsNullOrWhiteSpace(config.ReportRelativePath))
            throw new InvalidOperationException("Economy Admiral config: ReportRelativePath must not be empty.");
        if (Path.IsPathRooted(config.ReportRelativePath)
            || config.ReportRelativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Contains("..", StringComparer.Ordinal))
            throw new InvalidOperationException("Economy Admiral config: ReportRelativePath must remain inside the mod folder.");
        if (config.Rarity is null || config.CustomAuditPolicy is null || config.ManualOverrides is null || config.QuestRewardOverrides is null)
            throw new InvalidOperationException("Economy Admiral config: Rarity, CustomAuditPolicy, ManualOverrides and QuestRewardOverrides must be objects.");
        if (!double.IsFinite(config.CustomTraderPurchasePriceMultiplier)
            || config.CustomTraderPurchasePriceMultiplier < 1.0
            || config.CustomTraderPurchasePriceMultiplier > 2.0)
            throw new InvalidOperationException("Economy Admiral config: CustomTraderPurchasePriceMultiplier must be finite and within 1.0..2.0.");
        if (!double.IsFinite(config.CustomTraderSellPayoutMultiplier)
            || config.CustomTraderSellPayoutMultiplier < 0.50
            || config.CustomTraderSellPayoutMultiplier > 1.00)
            throw new InvalidOperationException("Economy Admiral config: CustomTraderSellPayoutMultiplier must be finite and within 0.50..1.00.");
        if (!double.IsFinite(config.CustomFleaBasePriceMultiplier)
            || config.CustomFleaBasePriceMultiplier < 1.0
            || config.CustomFleaBasePriceMultiplier > 2.5)
            throw new InvalidOperationException("Economy Admiral config: CustomFleaBasePriceMultiplier must be finite and within 1.0..2.5.");
        if (!double.IsFinite(config.CustomFleaMaxPriceDifferenceBelowHandbookPercent)
            || config.CustomFleaMaxPriceDifferenceBelowHandbookPercent < 0.0
            || config.CustomFleaMaxPriceDifferenceBelowHandbookPercent > 100.0)
            throw new InvalidOperationException("Economy Admiral config: CustomFleaMaxPriceDifferenceBelowHandbookPercent must be finite and within 0..100.");
        if (!double.IsFinite(config.CustomFleaHandbookPriceMultiplier)
            || config.CustomFleaHandbookPriceMultiplier < 1.0
            || config.CustomFleaHandbookPriceMultiplier > 2.0)
            throw new InvalidOperationException("Economy Admiral config: CustomFleaHandbookPriceMultiplier must be finite and within 1.0..2.0.");
        if (!double.IsFinite(config.CustomFleaListingFeeMultiplier)
            || config.CustomFleaListingFeeMultiplier < 1.0
            || config.CustomFleaListingFeeMultiplier > 2.0)
            throw new InvalidOperationException("Economy Admiral config: CustomFleaListingFeeMultiplier must be finite and within 1.0..2.0.");
        ValidateLootScale(config.CustomLooseLootScale, nameof(config.CustomLooseLootScale));
        ValidateLootScale(config.CustomStaticLootScale, nameof(config.CustomStaticLootScale));

        if (config.Rarity.CommonMinSources < 1 || config.Rarity.UncommonMinSources < 1 || config.Rarity.RareMinSources < 1
            || !(config.Rarity.CommonMinSources > config.Rarity.UncommonMinSources && config.Rarity.UncommonMinSources > config.Rarity.RareMinSources))
            throw new InvalidOperationException("Economy Admiral config: rarity thresholds must be positive and satisfy Common > Uncommon > Rare.");

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
        ValidatePositiveFinite(config.CustomAuditPolicy.RestartableHighStandingWarnMultiple, nameof(config.CustomAuditPolicy.RestartableHighStandingWarnMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.LowDepthMaxRelativeMultiple, nameof(config.CustomAuditPolicy.LowDepthMaxRelativeMultiple));
        ValidatePositiveFinite(config.CustomAuditPolicy.LowStructureMaxRelativeMultiple, nameof(config.CustomAuditPolicy.LowStructureMaxRelativeMultiple));
        if (config.CustomAuditPolicy.DuplicateTraderSourcesWarnCount < 1)
            throw new InvalidOperationException("Economy Admiral config: DuplicateTraderSourcesWarnCount must be positive.");

        foreach (var (templateId, itemOverride) in config.ManualOverrides)
        {
            if (string.IsNullOrWhiteSpace(templateId) || itemOverride is null)
                throw new InvalidOperationException("Economy Admiral config: manual overrides require non-empty template ids and object values.");
            if (itemOverride.Rarity is not null && !AllowedRarities.Contains(itemOverride.Rarity))
                throw new InvalidOperationException($"Economy Admiral config: unsupported manual rarity '{itemOverride.Rarity}' for template '{templateId}'.");
        }

        foreach (var (questId, questOverride) in config.QuestRewardOverrides)
        {
            if (string.IsNullOrWhiteSpace(questId) || questOverride is null)
                throw new InvalidOperationException("Economy Admiral config: quest reward overrides require non-empty quest ids and object values.");
            if (questOverride.ExperienceTarget is { } xp)
            {
                ValidateNonNegativeFinite(xp, $"QuestRewardOverrides[{questId}].ExperienceTarget");
                ValidateExactPrecision(xp, 0, $"QuestRewardOverrides[{questId}].ExperienceTarget", "an integer XP total");
            }
            if (questOverride.TraderStandingTarget is { } standing)
            {
                if (!double.IsFinite(standing))
                    throw new InvalidOperationException($"Economy Admiral config: QuestRewardOverrides[{questId}].TraderStandingTarget must be finite.");
                ValidateExactPrecision(standing, 4, $"QuestRewardOverrides[{questId}].TraderStandingTarget", "representable to at most 4 decimal places");
            }
            if (questOverride.ItemRewardStackCountTarget is { } stackTarget)
            {
                ValidatePositiveFinite(stackTarget, $"QuestRewardOverrides[{questId}].ItemRewardStackCountTarget");
                ValidateExactPrecision(stackTarget, 0, $"QuestRewardOverrides[{questId}].ItemRewardStackCountTarget", "an integer stack count");
            }
        }
    }

    private static void ValidateLootScale(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0.50 || value > 1.00)
            throw new InvalidOperationException($"Economy Admiral config: {name} must be finite and within 0.50..1.00.");
    }

    private static void ValidateExactPrecision(double value, int decimals, string name, string requirement)
    {
        var rounded = Math.Round(value, decimals);
        var tolerance = decimals == 0 ? 0.000001 : 0.000000001;
        if (Math.Abs(value - rounded) > tolerance)
            throw new InvalidOperationException($"Economy Admiral config: {name} must be {requirement}; exact targets are not silently rounded.");
    }

    private static void ValidatePositiveFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new InvalidOperationException($"Economy Admiral config: {name} must be finite and > 0.");
    }

    private static void ValidateNonNegativeFinite(double value, string name)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new InvalidOperationException($"Economy Admiral config: {name} must be finite and >= 0.");
    }
}