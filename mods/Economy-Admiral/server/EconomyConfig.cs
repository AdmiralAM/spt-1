using System.Text.Json.Serialization;

namespace SPTEconomy;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EconomyMode
{
    Off,
    Audit,
    Enforce,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EconomyPreset
{
    Easy,
    Normal,
    Hard,
    Custom,
}

public sealed record EconomyConfig
{
    public EconomyMode Mode { get; init; } = EconomyMode.Audit;
    public EconomyPreset Preset { get; init; } = EconomyPreset.Normal;
    public string ReportRelativePath { get; init; } = "reports/economy-admiral-audit.json";
    public bool RepeatedRaidLootDecay { get; init; } = false;
    public bool EnableItemRewardStackNormalization { get; init; } = false;
    public bool EnableTraderPurchasePressure { get; init; } = false;
    public double CustomTraderPurchasePriceMultiplier { get; init; } = 1.15;
    public bool EnableTraderSellPressure { get; init; } = false;
    public double CustomTraderSellPayoutMultiplier { get; init; } = 0.85;
    public bool EnableFleaPurchasePressure { get; init; } = false;
    public double CustomFleaBasePriceMultiplier { get; init; } = 1.65;
    public bool EnableFleaListingFeePressure { get; init; } = false;
    public double CustomFleaListingFeeMultiplier { get; init; } = 1.25;
    public bool EnableLootPressure { get; init; } = false;
    public double CustomLooseLootScale { get; init; } = 0.85;
    public double CustomStaticLootScale { get; init; } = 0.85;
    public RarityThresholds Rarity { get; init; } = new();
    public AuditPolicy CustomAuditPolicy { get; init; } = new();
    public Dictionary<string, ManualItemOverride> ManualOverrides { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, ManualQuestRewardOverride> QuestRewardOverrides { get; init; } = new(StringComparer.Ordinal);
}

public sealed record RarityThresholds
{
    public int CommonMinSources { get; init; } = 8;
    public int UncommonMinSources { get; init; } = 4;
    public int RareMinSources { get; init; } = 2;
}

public sealed record AuditPolicy
{
    public double QuestRewardVsVanillaMedianWarnMultiple { get; init; } = 3.0;
    public double RestartableRewardVsVanillaMedianWarnMultiple { get; init; } = 1.5;
    public double NormalizedRewardVsVanillaMedianWarnMultiple { get; init; } = 2.5;
    public double RestartableNormalizedRewardVsVanillaMedianWarnMultiple { get; init; } = 1.25;
    public double LevelGateWeight { get; init; } = 0.05;
    public double ObjectiveConditionWeight { get; init; } = 0.35;
    public double MaxLevelGateContribution { get; init; } = 3.0;
    public double MaxObjectiveContribution { get; init; } = 5.0;
    public int DuplicateTraderSourcesWarnCount { get; init; } = 6;

    public double HighItemValueLowStructureWarnMultiple { get; init; } = 3.0;
    public double HighXpLowDepthWarnMultiple { get; init; } = 3.0;
    public double HighStandingLowDepthWarnMultiple { get; init; } = 3.0;
    public double RestartableHighItemValueWarnMultiple { get; init; } = 2.0;
    public double RestartableHighXpWarnMultiple { get; init; } = 2.0;
    public double LowDepthMaxRelativeMultiple { get; init; } = 1.0;
    public double LowStructureMaxRelativeMultiple { get; init; } = 1.0;
}

public sealed record ManualItemOverride
{
    public string? Rarity { get; init; }
    public bool? Ignore { get; init; }
    public string? Note { get; init; }
}

public sealed record ManualQuestRewardOverride
{
    public bool? AllowAutomaticMutation { get; init; }
    public double? ExperienceTarget { get; init; }
    public double? TraderStandingTarget { get; init; }
    public double? ItemRewardStackCountTarget { get; init; }
    public string? Note { get; init; }
}
