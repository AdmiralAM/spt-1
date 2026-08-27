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
    // false = explicit deny; true = explicitly allow when provenance + flagged-dimension gates also pass.
    public bool? AllowAutomaticMutation { get; init; }

    // Exact total Success reward targets. Null keeps preset-derived target.
    public double? ExperienceTarget { get; init; }
    public double? TraderStandingTarget { get; init; }

    // Exact count for the one safe Success Item reward stack. This never replaces templates or reward records.
    public double? ItemRewardStackCountTarget { get; init; }

    public string? Note { get; init; }
}
