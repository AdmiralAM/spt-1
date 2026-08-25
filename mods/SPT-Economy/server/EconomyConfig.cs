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
    public string ReportRelativePath { get; init; } = "reports/economy-audit.json";
    public bool RepeatedRaidLootDecay { get; init; } = false;
    public RarityThresholds Rarity { get; init; } = new();
    public AuditPolicy CustomAuditPolicy { get; init; } = new();
    public Dictionary<string, ManualItemOverride> ManualOverrides { get; init; } = new(StringComparer.Ordinal);
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
    public int DuplicateTraderSourcesWarnCount { get; init; } = 6;
}

public sealed record ManualItemOverride
{
    public string? Rarity { get; init; }
    public bool? Ignore { get; init; }
    public string? Note { get; init; }
}
