using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTBeltArmbandInventory.Server.Patches;

namespace SPTBeltArmbandInventory.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.spt.belt-armband-inventory.server";
    public string Name { get; init; } = "B&A&HB #2 MOD SPT Server";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(0, 2, 0);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class BeltServerRegistration(
    IEnumerable<IRuntimePatch> runtimePatches,
    ISptLogger<BeltServerRegistration> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IRuntimePatch[] patches = runtimePatches.ToArray();
        IRuntimePatch? deathPatch = patches.SingleOrDefault(patch => patch is IsItemKeptAfterDeathPatch);
        IRuntimePatch? insurancePatch = patches.SingleOrDefault(patch => patch is HandleInsuredItemLostEventPatch);
        if (deathPatch is null || insurancePatch is null)
        {
            logger.Warning("B&A&HB #2 server protection patches were not both resolved by SPT 4.1 DI; death/insurance protection remains disabled as one atomic feature.");
            return Task.CompletedTask;
        }

        var enabled = new List<IRuntimePatch>(2);
        try
        {
            deathPatch.Enable();
            enabled.Add(deathPatch);
            insurancePatch.Enable();
            enabled.Add(insurancePatch);
            logger.Success("B&A&HB #2 server death + insurance protection patches installed atomically through SPT 4.1 DI.");
        }
        catch (Exception exception)
        {
            for (int i = enabled.Count - 1; i >= 0; i--)
            {
                try
                {
                    enabled[i].Disable();
                }
                catch (Exception rollbackException)
                {
                    logger.Warning($"B&A&HB #2 server protection rollback failed for {enabled[i].GetType().Name}: {rollbackException.GetType().Name}: {rollbackException.Message}");
                }
            }

            logger.Warning($"B&A&HB #2 server death/insurance protection failed atomically and was rolled back: {exception.GetType().Name}: {exception.Message}");
        }

        return Task.CompletedTask;
    }
}
