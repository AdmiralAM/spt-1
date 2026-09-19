using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTStackableArmorPlates.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.stackable-armor-plates";
    public string Name { get; init; } = "Admiral Stackable Armor Plates";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(0, 1, 0);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public bool HasPrepatcher { get; init; }
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new()
    {
        ["com.lacyway.mc"] = new(">=1.6.2")
    };
    public string? Url { get; init; }
    public string License { get; init; } = "MIT";
}

[Injectable(TypePriority = OnLoadOrder.PostLoad + 100200)]
public sealed class StackableArmorPlateRegistration(
    TemplateTable templateTable,
    ISptLogger<StackableArmorPlateRegistration> logger) : IOnLoad
{
    private static readonly MongoId ArmorPlateBase = new("644120aa86ffbe10ee032b6f");
    private const int PlateStackSize = 4;

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        int changed = 0;
        int alreadyLarger = 0;

        foreach (TemplateItem template in templateTable.Items.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TemplateItemProperties? properties = template.Properties;
            if (template.Parent != ArmorPlateBase
                || properties?.Width is not > 0
                || properties.Height is not > 0
                || properties.Weight is not > 0)
            {
                continue;
            }

            int current = properties.StackMaxSize.GetValueOrDefault(1);
            if (current >= PlateStackSize)
            {
                alreadyLarger++;
                continue;
            }

            properties.StackMaxSize = PlateStackSize;
            changed++;
        }

        if (changed == 0 && alreadyLarger == 0)
        {
            throw new InvalidDataException("No armor plate templates were resolved; refusing to load a silent no-op.");
        }

        logger.Success($"Stackable Armor Plates enabled {PlateStackSize}-item stacks for {changed} plate templates; {alreadyLarger} templates already allowed at least that many.");
        return Task.CompletedTask;
    }
}
