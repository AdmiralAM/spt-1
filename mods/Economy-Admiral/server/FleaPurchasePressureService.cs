using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Servers;

namespace SPTEconomy;

[Injectable]
public sealed class FleaPurchasePressureService(
    ConfigServer configServer,
    ISptLogger<FleaPurchasePressureService> logger)
{
    public FleaPurchasePressureResult Apply(EconomyConfig config)
    {
        if (!config.EnableFleaPurchasePressure || config.Mode != EconomyMode.Enforce)
            return new(false, 0, 0, false, false);

        var ragfair = configServer.GetConfig<RagfairConfig>();
        var generate = ragfair.Dynamic.GenerateBaseFleaPrices;
        var adjustment = ragfair.Dynamic.OfferAdjustment;

        var beforeBaseMultiplier = generate.PriceMultiplier;
        var beforePreventBelowTrader = generate.PreventPriceBeingBelowTraderBuyPrice;
        var beforeAdjustBelowHandbook = adjustment.AdjustPriceWhenBelowHandbookPrice;
        var beforeDifference = adjustment.MaxPriceDifferenceBelowHandbookPercent;
        var beforeHandbookMultiplier = adjustment.HandbookPriceMultiplier;

        try
        {
            generate.PriceMultiplier = FleaPurchasePressurePolicy.StrongerBasePriceMultiplier(beforeBaseMultiplier, config);
            generate.PreventPriceBeingBelowTraderBuyPrice = true;
            adjustment.AdjustPriceWhenBelowHandbookPrice = true;
            adjustment.MaxPriceDifferenceBelowHandbookPercent = FleaPurchasePressurePolicy.StrongerBelowHandbookDifference(beforeDifference, config);
            adjustment.HandbookPriceMultiplier = FleaPurchasePressurePolicy.StrongerHandbookPriceMultiplier(beforeHandbookMultiplier, config);

            var targets = FleaPurchasePressurePolicy.Resolve(config);
            logger.Info(
                $"[Economy Admiral] flea purchase pressure applied: preset={config.Preset}, " +
                $"basePriceMultiplier={beforeBaseMultiplier:0.###}->{generate.PriceMultiplier:0.###}, " +
                $"belowHandbookDifference={beforeDifference:0.###}%->{adjustment.MaxPriceDifferenceBelowHandbookPercent:0.###}%, " +
                $"handbookMultiplier={beforeHandbookMultiplier:0.###}->{adjustment.HandbookPriceMultiplier:0.###}, " +
                $"antiArbitrageFloor={generate.PreventPriceBeingBelowTraderBuyPrice}, targetBase={targets.BasePriceMultiplier:0.###}");

            return new(
                true,
                beforeBaseMultiplier,
                generate.PriceMultiplier,
                beforePreventBelowTrader,
                generate.PreventPriceBeingBelowTraderBuyPrice);
        }
        catch
        {
            generate.PriceMultiplier = beforeBaseMultiplier;
            generate.PreventPriceBeingBelowTraderBuyPrice = beforePreventBelowTrader;
            adjustment.AdjustPriceWhenBelowHandbookPrice = beforeAdjustBelowHandbook;
            adjustment.MaxPriceDifferenceBelowHandbookPercent = beforeDifference;
            adjustment.HandbookPriceMultiplier = beforeHandbookMultiplier;
            throw;
        }
    }
}

public sealed record FleaPurchasePressureResult(
    bool Applied,
    double BaseMultiplierBefore,
    double BaseMultiplierAfter,
    bool AntiArbitrageFloorBefore,
    bool AntiArbitrageFloorAfter);
