using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTBeltArmbandInventory.Server.Patches;

namespace SPTBeltArmbandInventory.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.spt.belt-armband-inventory.server";
    public string Name { get; init; } = "SPT Belt Armband Inventory Server";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(0, 1, 0);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}

[Injectable(InjectionType = InjectionType.Singleton)]
public sealed class BeltServerRegistration : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        new IsItemKeptAfterDeathPatch().Enable();
        new HandleInsuredItemLostEventPatch().Enable();
        return Task.CompletedTask;
    }
}
