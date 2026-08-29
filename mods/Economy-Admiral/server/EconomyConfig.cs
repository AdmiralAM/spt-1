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
    private bool enableTraderPurchasePressure;
    private bool enableTraderSellPressure;
    private bool enableFleaPurchasePressure;
    private bool enableFleaListingFeePressure;
    private bool enableLootPressure;
    private Dictionary<string, ManualQuestRewardOverride> questRewardOverrides = new(StringComparer.Ordinal);

    public EconomyMode Mode { get; init; } = EconomyMode.Audit;
    public EconomyPreset Preset { get; init; } = EconomyPreset.Normal;
    public string ReportRelativePath { get; init; } = "reports/economy-admiral-audit.json";
    public bool RepeatedRaidLootDecay { get; init; } = false;

    /// <summary>
    /// High-level product switch. Safe by default because committed Mode remains Audit.
    /// When Mode is Enforce, this activates every accepted Playable Economy surface through the selected preset.
    /// Cluster switches remain hard gates so Advanced mode can keep selected areas vanilla.
    /// </summary>
    public bool EnablePlayableEconomyBundle { get; init; } = true;

    public bool EnableQuestEconomyCluster { get; init; } = true;
    public bool EnableTraderEconomyCluster { get; init; } = true;
    public bool EnableFleaEconomyCluster { get; init; } = true;
    public bool EnableLootEconomyCluster { get; init; } = true;

    public bool EnableItemRewardStackNormalization
    {
        get => EnableQuestEconomyCluster && (enableItemRewardStackNormalization || (EnablePlayableEconomyBundle && Mode == EconomyMode.Enforce));
        init => enableItemRewardStackNormalization = value;
    }

    public bool EnableTraderPurchasePressure
    {
        get => EnableTraderEconomyCluster && (enableTraderPurchasePressure || (EnablePlayableEconomyBundle && Mode == EconomyMode.Enforce));
        init => enableTraderPurchasePressure = value;
    }

    public double CustomTraderPurchasePriceMultiplier { get; init; } = 1.15;

    public bool EnableTraderSellPressure
    {
        get => EnableTraderEconomyCluster && (enableTraderSellPressure || (EnablePlayableEconomyBundle && Mode == EconomyMode.Enforce));
        init => enableTraderSellPressure = value;
    }

    public double CustomTraderSellPayoutMultiplier { get; init; } = 0.85;

    public bool EnableFleaPurchasePressure
    {
        get => EnableFleaEconomyCluster && (enableFleaPurchasePressure || (EnablePlayableEconomyBundle && Mode == EconomyMode.Enforce));
        init => enableFleaPurchasePressure = value;
    }

    public double CustomFleaBasePriceMultiplier { get; init; } = 1.65;

    public bool EnableFleaListingFeePressure
    {
        get => EnableFleaEconomyCluster && (enableFleaListingFeePressure || (EnablePlayableEconomyBundle && Mode == EconomyMode.Enforce));
        init => enableFleaListingFeePressure = value;
    }

    public double CustomFleaListingFeeMultiplier { get; init; } = 1.25;

    public bool EnableLootPressure
    {
        get => EnableLootEconomyCluster && (enableLootPressure || (EnablePlayableEconomyBundle && Mode == EconomyMode.Enforce));
        init => enableLootPressure = value;
    }

    public double CustomLooseLootScale { get; init; } = 0.85;
    public double CustomStaticLootScale { get; init; } = 0.85;
    public RarityThresholds Rarity { get; init; } = new();
    public AuditPolicy CustomAuditPolicy { get; init; } = new();
    public Dictionary<string, ManualItemOverride> ManualOverrides { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, ManualQuestRewardOverride> QuestRewardOverrides
    {
        get => EnableQuestEconomyCluster ? questRewardOverrides : EmptyQuestRewardOverrides;
        init => questRewardOverrides = value;
    }

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
