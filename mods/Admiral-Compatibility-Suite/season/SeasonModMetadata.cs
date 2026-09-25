using SPTarkov.Server.Core.Models.Spt.Mod;

namespace AdmiralCompatibilitySuite.Seasons;

public sealed record SeasonModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.compatibility-suite.seasons";
    public string Name { get; init; } = "Admiral Compatibility Suite - Seasons";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(0, 1, 0);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}
