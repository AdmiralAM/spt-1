using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Ragfair;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace ItemValuationModSpt.Server;

public static class RuntimeIdentity
{
    public const string ModGuid = "com.admiralam.spt.itemvaluation";
    public const string ProductName = "Item Valuation MOD SPT";
    public const string Version = "1.0.0";
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

    public double AmmoTintStartPen { get; init; } = 10;
    public double AmmoLightGreenMaxPen { get; init; } = 20;
    public double AmmoNavyMaxPen { get; init; } = 40;
    public double AmmoVioletMaxPen { get; init; } = 50;
    public double AmmoRedMaxPen { get; init; } = 70;

    // Restored from AcidPhantasm's dedicated key scale.
    public double KeyBadMaxValue { get; init; } = 10000;
    public double KeyPoorMaxValue { get; init; } = 20000;
    public double KeyFairMaxValue { get; init; } = 30000;
    public double KeyGoodMaxValue { get; init; } = 50000;
    public double KeyVeryGoodMaxValue { get; init; } = 75000;
    public string KeyBadColor { get; init; } = "#404040";
    public string KeyPoorColor { get; init; } = "#a3a3a3";
    public string KeyFairColor { get; init; } = "#0c3b08";
    public string KeyGoodColor { get; init; } = "#08083b";
    public string KeyVeryGoodColor { get; init; } = "#590b5e";
    public string KeyExceptionalColor { get; init; } = "#5e470b";
    public string KeyFleaBannedColor { get; init; } = "#660415";

    public string LightGreenColor { get; init; } = "#526B3F";
    public string GreenColor { get; init; } = "#294F31";
    public string NavyColor { get; init; } = "#253552";
    public string VioletColor { get; init; } = "#4A3854";
    public string RedColor { get; init; } = "#5A2C31";
    public string GoldColor { get; init; } = "#5C4825";

    public void Validate()
    {
        if (!(0 <= TintStartValue && TintStartValue < LightGreenMaxValue &&
              LightGreenMaxValue < GreenMaxValue && GreenMaxValue < NavyMaxValue &&
              NavyMaxValue < VioletMaxValue && VioletMaxValue < RedMaxValue))
            throw new InvalidDataException("Item Valuation money thresholds must be non-negative and strictly ascending.");

        if (!(0 <= AmmoTintStartPen && AmmoTintStartPen < AmmoLightGreenMaxPen &&
              AmmoLightGreenMaxPen < AmmoNavyMaxPen && AmmoNavyMaxPen < AmmoVioletMaxPen &&
              AmmoVioletMaxPen < AmmoRedMaxPen))
            throw new InvalidDataException("Item Valuation ammo penetration thresholds must be non-negative and strictly ascending.");

        if (!(0 <= KeyBadMaxValue && KeyBadMaxValue < KeyPoorMaxValue &&
              KeyPoorMaxValue < KeyFairMaxValue && KeyFairMaxValue < KeyGoodMaxValue &&
              KeyGoodMaxValue < KeyVeryGoodMaxValue))
            throw new InvalidDataException("Item Valuation key thresholds must be non-negative and strictly ascending.");

        string[] colors =
        [
            LightGreenColor, GreenColor, NavyColor, VioletColor, RedColor, GoldColor,
            KeyBadColor, KeyPoorColor, KeyFairColor, KeyGoodColor, KeyVeryGoodColor,
            KeyExceptionalColor, KeyFleaBannedColor
        ];
        if (colors.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("Item Valuation colors must not be empty.");
    }
}

public static class TierClassifier
{
    public static string? GetMoneyColor(double value, ValueColorConfig config)
    {
        if (value < config.TintStartValue) return null;
        if (value < config.LightGreenMaxValue) return config.LightGreenColor;
        if (value < config.GreenMaxValue) return config.GreenColor;
        if (value < config.NavyMaxValue) return config.NavyColor;
        if (value < config.VioletMaxValue) return config.VioletColor;
        if (value < config.RedMaxValue) return config.RedColor;
        return config.GoldColor;
    }

    public static string? GetAmmoColor(double penetration, ValueColorConfig config)
    {
        if (penetration < config.AmmoTintStartPen) return null;
        if (penetration <= config.AmmoLightGreenMaxPen) return config.LightGreenColor;
        if (penetration <= config.AmmoNavyMaxPen) return config.NavyColor;
        if (penetration <= config.AmmoVioletMaxPen) return config.VioletColor;
        if (penetration <= config.AmmoRedMaxPen) return config.RedColor;
        return config.GoldColor;
    }

    public static string GetKeyColor(double price, bool validFleaItem, ValueColorConfig config)
    {
        // Original key behavior: flea-banned keys override the monetary tier.
        if (!validFleaItem) return config.KeyFleaBannedColor;
        if (price < config.KeyBadMaxValue) return config.KeyBadColor;
        if (price < config.KeyPoorMaxValue) return config.KeyPoorColor;
        if (price < config.KeyFairMaxValue) return config.KeyFairColor;
        if (price < config.KeyGoodMaxValue) return config.KeyGoodColor;
        if (price < config.KeyVeryGoodMaxValue) return config.KeyVeryGoodColor;
        return config.KeyExceptionalColor;
    }
}

[Injectable(TypePriority = OnLoadOrder.PostLoad), UsedImplicitly]
public sealed class ItemValuationBackgroundLoader(
    ModHelper modHelper,
    TemplateTable templateTable,
    TradersTable tradersTable,
    ItemHelper itemHelper,
    PresetHelper presetHelper,
    RagfairServerHelper ragfairServerHelper,
    ISptLogger<ItemValuationBackgroundLoader> logger) : IOnLoad
{
    private static readonly MongoId[] TotalValueBaseClasses =
    [
        BaseClasses.WEAPON,
        BaseClasses.ARMORED_EQUIPMENT,
        BaseClasses.VEST
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValueColorConfig config = LoadConfig(modHelper);
        config.Validate();
        Dictionary<MongoId, double> handbookPrices = BuildHandbookPriceIndex(templateTable);

        int coloredMoney = 0;
        int coloredAmmo = 0;
        int coloredKeys = 0;
        int preserved = 0;
        int traderWon = 0;
        int fleaWon = 0;
        int handbookFallback = 0;

        foreach ((MongoId templateId, var item) in templateTable.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var properties = item.Properties;
            if (properties is null)
                continue;

            if (itemHelper.IsOfBaseclass(templateId, BaseClasses.AMMO))
            {
                double penetration = properties.PenetrationPower ?? 0;
                string? ammoColor = TierClassifier.GetAmmoColor(penetration, config);
                if (ammoColor is null)
                {
                    preserved++;
                    continue;
                }
                properties.BackgroundColor = ammoColor;
                coloredAmmo++;
                continue;
            }

            if (itemHelper.IsOfBaseclass(templateId, BaseClasses.KEY))
            {
                double keyPrice;
                if (!templateTable.Prices.TryGetValue(templateId, out keyPrice) || keyPrice <= 0)
                {
                    if (!handbookPrices.TryGetValue(templateId, out keyPrice) || keyPrice <= 0)
                        continue;
                }

                bool validFleaItem = ragfairServerHelper.IsItemValidRagfairItem(itemHelper.GetItem(templateId));
                properties.BackgroundColor = TierClassifier.GetKeyColor(keyPrice, validFleaItem, config);
                coloredKeys++;
                continue;
            }

            if (properties.Width is not > 0 || properties.Height is not > 0)
                continue;

            (double value, ValueSource source) = ResolveEconomicValue(templateId, handbookPrices);
            if (value <= 0)
                continue;

            switch (source)
            {
                case ValueSource.Trader: traderWon++; break;
                case ValueSource.Flea: fleaWon++; break;
                case ValueSource.Handbook: handbookFallback++; break;
            }

            double valuation;
            if (itemHelper.IsOfBaseclasses(templateId, TotalValueBaseClasses))
            {
                valuation = value;
            }
            else
            {
                long slots = (long)properties.Width.Value * properties.Height.Value;
                if (slots <= 0)
                    continue;
                valuation = Math.Round(value / slots, MidpointRounding.AwayFromZero);
            }

            string? color = TierClassifier.GetMoneyColor(valuation, config);
            if (color is null)
            {
                preserved++;
                continue;
            }

            properties.BackgroundColor = color;
            coloredMoney++;
        }

        logger.Success(
            $"{RuntimeIdentity.ProductName} {RuntimeIdentity.Version}: money-colored {coloredMoney}, ammo-colored {coloredAmmo}, key-colored {coloredKeys}, " +
            $"preserved {preserved} below money/ammo tint thresholds; value source wins trader={traderWon}, flea={fleaWon}, handbook={handbookFallback}; " +
            "single PostLoad pass, BackgroundColor only, no client patches or runtime polling");
        return Task.CompletedTask;
    }

    private (double Value, ValueSource Source) ResolveEconomicValue(
        MongoId templateId,
        IReadOnlyDictionary<MongoId, double> handbookPrices)
    {
        handbookPrices.TryGetValue(templateId, out double handbookValue);
        double traderBasis = GetTraderValuationBasis(templateId, handbookValue, handbookPrices);
        double traderValue = ResolveBestTraderPrice(templateId, traderBasis);

        double fleaValue = 0;
        if (templateTable.Prices.TryGetValue(templateId, out double tablePrice) && tablePrice > 0 &&
            ragfairServerHelper.IsItemValidRagfairItem(itemHelper.GetItem(templateId)))
        {
            fleaValue = tablePrice;
        }

        if (traderValue > 0 && traderValue >= fleaValue)
            return (traderValue, ValueSource.Trader);
        if (fleaValue > 0)
            return (fleaValue, ValueSource.Flea);
        if (handbookValue > 0)
            return (handbookValue, ValueSource.Handbook);
        return (0, ValueSource.None);
    }

    private double GetTraderValuationBasis(
        MongoId templateId,
        double handbookValue,
        IReadOnlyDictionary<MongoId, double> handbookPrices)
    {
        var preset = presetHelper.GetDefaultPreset(templateId);
        if (preset?.Items is null || preset.Items.Count == 0)
            return handbookValue;

        double total = 0;
        foreach (var presetItem in preset.Items)
            if (handbookPrices.TryGetValue(presetItem.Template, out double childValue))
                total += childValue;
        return total > 0 ? total : handbookValue;
    }

    private double ResolveBestTraderPrice(MongoId templateId, double valuationBasis)
    {
        double regular = ResolveBestTraderPrice(templateId, valuationBasis, includeFence: false);
        return regular > 0 ? regular : ResolveBestTraderPrice(templateId, valuationBasis, includeFence: true);
    }

    private double ResolveBestTraderPrice(MongoId templateId, double valuationBasis, bool includeFence)
    {
        double highestPrice = 0;
        foreach (var (traderId, trader) in tradersTable)
        {
            bool isFence = traderId == Traders.FENCE;
            if (isFence != includeFence)
                continue;

            var traderBase = trader.Base;
            var buy = traderBase.ItemsBuy;
            if (buy is null)
                continue;
            if (!buy.IdList.Contains(templateId) && !itemHelper.IsOfBaseclasses(templateId, buy.Category))
                continue;

            double coefficient = traderBase.LoyaltyLevels?.FirstOrDefault()?.BuyPriceCoefficient ?? 100d;
            double price = Math.Round(Math.Max(0d, 100d - coefficient) * (valuationBasis / 100d), 0);
            if (price > highestPrice)
                highestPrice = price;
        }
        return highestPrice;
    }

    private static Dictionary<MongoId, double> BuildHandbookPriceIndex(TemplateTable templateTable)
    {
        Dictionary<MongoId, double> index = new(templateTable.Handbook.Items.Count);
        foreach (var handbookItem in templateTable.Handbook.Items)
            if (handbookItem.Price is > 0)
                index[handbookItem.Id] = handbookItem.Price.Value;
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

    private enum ValueSource
    {
        None,
        Trader,
        Flea,
        Handbook
    }
}
