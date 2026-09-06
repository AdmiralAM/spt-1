using SPTarkov.Server.Core.Models.Spt.Mod;

namespace AdmiralTrader.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = RuntimeIdentity.ModGuid;
    public string Name { get; init; } = "Admiral Trader";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("0.1.0");

    // Intentionally not pinned to one exact SPT patch.
    // The current development/validation baseline is tracked separately; compatible 4.1.x updates remain loadable.
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");

    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/AdmiralAM/spt-1";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}
