using JetBrains.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace SPTEconomy;

[UsedImplicitly]
public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.spt-economy";
    public string Name { get; init; } = "SPT Economy";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; } = ["Economy MOD SPT"];
    public Version Version { get; init; } = new(typeof(ModMetadata).Assembly.GetName().Version!.ToString(3));
    public Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, Range>? ModDependencies { get; init; } = [];
    public string? Url { get; init; } = "https://github.com/AdmiralAM/spt-1";
    public string License { get; init; } = "MIT";
}
