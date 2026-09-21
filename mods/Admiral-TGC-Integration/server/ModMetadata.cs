using JetBrains.Annotations;
using SPTarkov.Server.Core.Models.Spt.Mod;
using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;

namespace TGC;

[UsedImplicitly]
public record ModMetadata : IModMetadata
{
    // Preserve the upstream identity: this DLL replaces, rather than co-loads
    // with, the stock TGC server DLL.
    public string ModGuid { get; init; } = "com.emilanderss0n.tgc";
    public string Name { get; init; } = "Tactical Gear Component — Admiral Integration";
    public string Author { get; init; } = "MonoPixel";
    public List<string>? Contributors { get; init; } = ["AdmiralAM"];
    public Version Version { get; init; } = new(typeof(ModMetadata).Assembly.GetName().Version!.ToString(3));
    public Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; } = [];
    public Dictionary<string, Range>? ModDependencies { get; init; } = new()
    {
        { "com.wtt.commonlib", new Range("~3.0.0") }
    };
    public string? Url { get; init; } = "https://github.com/thuynguyentrungdang/TGC";
    public string License { get; init; } = "MIT";
}

