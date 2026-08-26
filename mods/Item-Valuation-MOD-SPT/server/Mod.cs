using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
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
    public double TintStartValue { get; init; } = 10000;
    public double LightGreenMaxValue { get; init; } = 25000;
    public double GreenMaxValue { get; init; } = 50000;
    public double NavyMaxValue { get; init; } = 75000;
    public double VioletMaxValue { get; init; } = 100000;
    public double RedMaxValue { get; init; } = 250000;

    // Deliberately subdued colours: readable tier separation without bright inventory tiles.
    public string LightGreenColor { get; init; } = "#526B3F";
    public string GreenColor { get; init; } = "#294F31";
    public string NavyColor { get; init; } = "#253552";
    public string VioletColor { get; init; } = "#4A3854";
    public string RedColor { get; init; } = "#5A2C31";
    public string GoldColor { get; init; } = "#5C4825";

    public void Validate()
    {
        if (!(0 <= TintStartValue &&
              TintStartValue < LightGreenMaxValue &&
              LightGreenMaxValue < GreenMaxValue &&
              GreenMaxValue < NavyMaxValue &&
              NavyMaxValue < VioletMaxValue &&
              VioletMaxValue < RedMaxValue))
        {
            throw new InvalidDataException("Item Valuation thresholds must be non-negative and strictly ascending.");
        }

        string[] colors = [LightGreenColor, GreenColor, NavyColor, VioletColor, RedColor, GoldColor];
        if (colors.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Item Valuation colors must not be empty.");
    }
}

public static class ValueTierClassifier
{
    public static string? GetColor(double value, ValueColorConfig config)
    {
        if (value < config.TintStartValue) return null;
        if (value < config.LightGreenMaxValue) return config.LightGreenColor;
        if (value < config.GreenMaxValue) return config.GreenColor;
        if (value < config.NavyMaxValue) return config.NavyColor;
        if (value < config.VioletMaxValue) return config.VioletColor;
        if (value < config.RedMaxValue) return config.RedColor;
        return config.GoldColor;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostLoad), UsedImplicitly]
public sealed class ItemValuationBackgroundLoader(
    ModHelper modHelper,
    TemplateTable templateTable,
    ItemHelper itemHelper,
    ISptLogger<ItemValuationBackgroundLoader> logger) : IOnLoad
{
    private static readonly MongoId[] TotalValueBaseClasses =
    [
        BaseClasses.WEAPON,
        BaseClasses.KEY,
        BaseClasses.ARMORED_EQUIPMENT,
        BaseClasses.VEST
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValueColorConfig config = LoadConfig(modHelper);
        config.Validate();

        Dictionary<MongoId, double> handbookPrices = BuildHandbookPriceIndex(templateTable);
        int colored = 0;
        int preserved = 0;
        int totalValueItems = 0;
        int perSlotItems = 0;
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

            double valuation;
            if (itemHelper.IsOfBaseclasses(templateId, TotalValueBaseClasses))
            {
                valuation = price;
                totalValueItems++;
            }
            else
            {
                long slots = (long)properties.Width.Value * properties.Height.Value;
                if (slots <= 0)
                    continue;

                valuation = Math.Round(price / slots, MidpointRounding.AwayFromZero);
                perSlotItems++;
            }

            string? color = ValueTierClassifier.GetColor(valuation, config);
            if (color is null)
            {
                // Under 10k: preserve the template's original/default background exactly.
                preserved++;
                continue;
            }

            properties.BackgroundColor = color;
            colored++;
        }

        logger.Success(
            $"{RuntimeIdentity.ProductName} {RuntimeIdentity.Version}: colored {colored} templates once at server load; " +
            $"preserved {preserved} below tint threshold; {totalValueItems} total-value / {perSlotItems} per-slot valuations; " +
            $"{fallbackPrices} handbook fallbacks; no client patches or runtime polling");
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
