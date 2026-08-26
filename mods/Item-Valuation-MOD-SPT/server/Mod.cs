using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace ItemValuationModSpt.Server;

public static class RuntimeIdentity
{
    public const string ModGuid = "com.admiralam.spt.itemvaluation";
    public const string ProductName = "Item Valuation MOD SPT";
    public const string Version = "0.1.0";
}

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = RuntimeIdentity.ModGuid;
    public string Name { get; init; } = RuntimeIdentity.ProductName;
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new(RuntimeIdentity.Version);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; } = ["com.acidphantasm.itemvaluation"];
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/AdmiralAM/spt-1";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

public sealed record ValueColorConfig
{
    public double BadMaxValuePerSlot { get; init; } = 5000;
    public double PoorMaxValuePerSlot { get; init; } = 10000;
    public double FairMaxValuePerSlot { get; init; } = 15000;
    public double GoodMaxValuePerSlot { get; init; } = 25000;
    public double VeryGoodMaxValuePerSlot { get; init; } = 35000;
    public string BadColor { get; init; } = "#404040";
    public string PoorColor { get; init; } = "#a3a3a3";
    public string FairColor { get; init; } = "#0c3b08";
    public string GoodColor { get; init; } = "#08083b";
    public string VeryGoodColor { get; init; } = "#590b5e";
    public string ExceptionalColor { get; init; } = "#5e470b";

    public void Validate()
    {
        if (!(0 <= BadMaxValuePerSlot &&
              BadMaxValuePerSlot < PoorMaxValuePerSlot &&
              PoorMaxValuePerSlot < FairMaxValuePerSlot &&
              FairMaxValuePerSlot < GoodMaxValuePerSlot &&
              GoodMaxValuePerSlot < VeryGoodMaxValuePerSlot))
        {
            throw new InvalidDataException("Item Valuation value-per-slot thresholds must be non-negative and strictly ascending.");
        }

        string[] colors = [BadColor, PoorColor, FairColor, GoodColor, VeryGoodColor, ExceptionalColor];
        if (colors.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Item Valuation colors must not be empty.");
    }
}

public static class ValueTierClassifier
{
    public static string GetColor(double valuePerSlot, ValueColorConfig config)
    {
        if (valuePerSlot <= config.BadMaxValuePerSlot) return config.BadColor;
        if (valuePerSlot <= config.PoorMaxValuePerSlot) return config.PoorColor;
        if (valuePerSlot <= config.FairMaxValuePerSlot) return config.FairColor;
        if (valuePerSlot <= config.GoodMaxValuePerSlot) return config.GoodColor;
        if (valuePerSlot <= config.VeryGoodMaxValuePerSlot) return config.VeryGoodColor;
        return config.ExceptionalColor;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostLoad), UsedImplicitly]
public sealed class ItemValuationBackgroundLoader(
    ModHelper modHelper,
    TemplateTable templateTable,
    ISptLogger<ItemValuationBackgroundLoader> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValueColorConfig config = LoadConfig(modHelper);
        config.Validate();

        Dictionary<MongoId, double> handbookPrices = BuildHandbookPriceIndex(templateTable);
        int colored = 0;
        int fallbackPrices = 0;

        foreach ((MongoId templateId, var item) in templateTable.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var properties = item.Properties;
            if (properties?.Width is not > 0 || properties.Height is not > 0)
                continue;

            double price;
            if (!templateTable.Prices.TryGetValue(templateId, out price) || price <= 0)
            {
                if (!handbookPrices.TryGetValue(templateId, out price) || price <= 0)
                    continue;
                fallbackPrices++;
            }

            long slots = (long)properties.Width.Value * properties.Height.Value;
            if (slots <= 0)
                continue;

            double valuePerSlot = Math.Round(price / slots, MidpointRounding.AwayFromZero);
            properties.BackgroundColor = ValueTierClassifier.GetColor(valuePerSlot, config);
            colored++;
        }

        logger.Success($"{RuntimeIdentity.ProductName} {RuntimeIdentity.Version}: colored {colored} item templates once at server load ({fallbackPrices} handbook fallbacks); no client patches or runtime polling");
        return Task.CompletedTask;
    }

    private static Dictionary<MongoId, double> BuildHandbookPriceIndex(TemplateTable templateTable)
    {
        Dictionary<MongoId, double> index = new(templateTable.Handbook.Items.Count);
        foreach (var handbookItem in templateTable.Handbook.Items)
        {
            if (handbookItem.Price is > 0)
                index[handbookItem.Id] = handbookItem.Price.Value;
        }
        return index;
    }

    private static ValueColorConfig LoadConfig(ModHelper modHelper)
    {
        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        string configPath = Path.Combine(modPath, "config", "config.json");
        if (!File.Exists(configPath))
            throw new FileNotFoundException("Item Valuation MOD SPT config is missing.", configPath);

        ValueColorConfig? config = JsonSerializer.Deserialize<ValueColorConfig>(
            File.ReadAllText(configPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return config ?? throw new InvalidDataException("Item Valuation MOD SPT config could not be parsed.");
    }
}
