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
    private bool enableItemRewardStackNormalization;
    private bool enableQuestXpPressure;
    private bool enableQuestStandingPressure;
    private bool enableRestartableQuestPressure;
    private bool enableTraderPurchasePressure;
    private bool enableTraderSellPressure;
    private bool enableFleaPurchasePressure;
    private bool enableFleaListingFeePressure;
    private bool enableLootPressure;
    private bool enableLooseLootPressure;
    private bool enableStaticLootPressure;
    private Dictionary<string, ManualQuestRewardOverride> questRewardOverrides = new(StringComparer.Ordinal);

    public EconomyMode Mode { get; init; } = EconomyMode.Enforce;
    public EconomyPreset Preset { get; init; } = EconomyPreset.Normal;
    public string ReportRelativePath { get; init; } = "reports/economy-admiral-audit.json";

    // Retired compatibility key. Older configs may still contain true, but the unimplemented mechanic is inert.
    public bool RepeatedRaidLootDecay { get => false; init { } }

    public bool EnablePlayableEconomyBundle { get; init; } = true;

    public bool EnableQuestEconomyCluster { get; init; } = true;
    public bool EnableTraderEconomyCluster { get; init; } = true;
    public bool EnableFleaEconomyCluster { get; init; } = true;
    public bool EnableLootEconomyCluster { get; init; } = true;

    public bool EnableItemRewardStackNormalization
    {
        get => EnableQuestEconomyCluster && (enableItemRewardStackNormalization || BundleEnforceActive);
        init => enableItemRewardStackNormalization = value;
    }

    public bool EnableQuestXpPressure
    {
        get => EnableQuestEconomyCluster && (enableQuestXpPressure || BundleEnforceActive);
        init => enableQuestXpPressure = value;
    }

    public bool EnableQuestStandingPressure
    {
        get => EnableQuestEconomyCluster && (enableQuestStandingPressure || BundleEnforceActive);
        init => enableQuestStandingPressure = value;
    }

    public bool EnableRestartableQuestPressure
    {
        get => EnableQuestEconomyCluster && (enableRestartableQuestPressure || BundleEnforceActive);
        init => enableRestartableQuestPressure = value;
    }

    public bool EnableTraderPurchasePressure
    {
        get => EnableTraderEconomyCluster && (enableTraderPurchasePressure || BundleEnforceActive);
        init => enableTraderPurchasePressure = value;
    }

    public double CustomTraderPurchasePriceMultiplier { get; init; } = 1.15;

    public bool EnableTraderSellPressure
    {
        get => EnableTraderEconomyCluster && (enableTraderSellPressure || BundleEnforceActive);
        init => enableTraderSellPressure = value;
    }

    public double CustomTraderSellPayoutMultiplier { get; init; } = 0.85;

    public bool EnableFleaPurchasePressure
    {
        get => EnableFleaEconomyCluster && (enableFleaPurchasePressure || BundleEnforceActive);
        init => enableFleaPurchasePressure = value;
    }

    public double CustomFleaBasePriceMultiplier { get; init; } = 1.65;
    public double CustomFleaMaxPriceDifferenceBelowHandbookPercent { get; init; } = 45.0;
    public double CustomFleaHandbookPriceMultiplier { get; init; } = 1.10;

    public bool EnableFleaListingFeePressure
    {
        get => EnableFleaEconomyCluster && (enableFleaListingFeePressure || BundleEnforceActive);
        init => enableFleaListingFeePressure = value;
    }

    public double CustomFleaListingFeeMultiplier { get; init; } = 1.25;

    // Legacy/manual master remains accepted for old configs. New UI exposes loose/static mechanisms separately.
    public bool EnableLootPressure
    {
        get => EnableLooseLootPressure || EnableStaticLootPressure;
        init => enableLootPressure = value;
    }

    public bool EnableLooseLootPressure
    {
        get => EnableLootEconomyCluster && (enableLootPressure || enableLooseLootPressure || BundleEnforceActive);
        init => enableLooseLootPressure = value;
    }

    public bool EnableStaticLootPressure
    {
        get => EnableLootEconomyCluster && (enableLootPressure || enableStaticLootPressure || BundleEnforceActive);
        init => enableStaticLootPressure = value;
    }

    public double CustomLooseLootScale { get; init; } = 0.85;
    public double CustomStaticLootScale { get; init; } = 0.85;

    // Custom gameplay enforcement targets are deliberately separate from CustomAuditPolicy.
    // Changing Custom difficulty must not redefine the observational/detection thresholds.
    public double CustomQuestItemBudgetMultiple { get; init; } = 1.50;
    public double CustomRestartableQuestItemBudgetMultiple { get; init; } = 1.15;
    public double CustomQuestXpMultiple { get; init; } = 1.50;
    public double CustomRestartableQuestXpMultiple { get; init; } = 1.15;
    public double CustomQuestStandingMultiple { get; init; } = 1.50;

    public RarityThresholds Rarity { get; init; } = new();
    public AuditPolicy CustomAuditPolicy { get; init; } = new();
    public Dictionary<string, ManualItemOverride> ManualOverrides { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, ManualQuestRewardOverride> QuestRewardOverrides
    {
        get => EnableQuestEconomyCluster ? questRewardOverrides : EmptyQuestRewardOverrides;
        init => questRewardOverrides = value;
    }

    [JsonIgnore] public bool ConfiguredEnableItemRewardStackNormalization => enableItemRewardStackNormalization;
    [JsonIgnore] public bool ConfiguredEnableQuestXpPressure => enableQuestXpPressure;
    [JsonIgnore] public bool ConfiguredEnableQuestStandingPressure => enableQuestStandingPressure;
    [JsonIgnore] public bool ConfiguredEnableRestartableQuestPressure => enableRestartableQuestPressure;
    [JsonIgnore] public bool ConfiguredEnableTraderPurchasePressure => enableTraderPurchasePressure;
    [JsonIgnore] public bool ConfiguredEnableTraderSellPressure => enableTraderSellPressure;
    [JsonIgnore] public bool ConfiguredEnableFleaPurchasePressure => enableFleaPurchasePressure;
    [JsonIgnore] public bool ConfiguredEnableFleaListingFeePressure => enableFleaListingFeePressure;
    [JsonIgnore] public bool ConfiguredEnableLootPressure => enableLootPressure;
    [JsonIgnore] public bool ConfiguredEnableLooseLootPressure => enableLooseLootPressure;
    [JsonIgnore] public bool ConfiguredEnableStaticLootPressure => enableStaticLootPressure;
    [JsonIgnore] public Dictionary<string, ManualQuestRewardOverride> ConfiguredQuestRewardOverrides => questRewardOverrides;

    [JsonIgnore] private bool BundleEnforceActive => EnablePlayableEconomyBundle && Mode == EconomyMode.Enforce;

    private static readonly Dictionary<string, ManualQuestRewardOverride> EmptyQuestRewardOverrides = new(StringComparer.Ordinal);
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
