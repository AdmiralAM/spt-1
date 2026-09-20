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
    public string Name { get; init; } = "Admiral Armor Plate Field Repair";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(0, 2, 0);
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
public sealed class StackableArmorPlateRegistration(TemplateTable templateTable) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        int plates = 0;

        foreach (TemplateItem template in templateTable.Items.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TemplateItemProperties? properties = template.Properties;
            if (properties is null
                || !PlateStackPolicy.IsStandalonePlate(
                    template.Parent.ToString(),
                    properties.Width.GetValueOrDefault(),
                    properties.Height.GetValueOrDefault(),
                    properties.Weight.GetValueOrDefault()))
            {
                continue;
            }

            properties.StackMaxSize = 1;
            plates++;
        }

        if (plates == 0)
        {
            throw new InvalidDataException("No armor plate templates were resolved; refusing to load a silent no-op.");
        }

        return Task.CompletedTask;
    }
}
