using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class TraderSellPressureService(
    TradersTable traders,
    ISptLogger<TraderSellPressureService> logger)
{
    private bool applied;

    public TraderSellPressureResult Apply(EconomyConfig config)
    {
        if (!config.EnableTraderSellPressure || config.Mode != EconomyMode.Enforce)
            return new TraderSellPressureResult(false, 1.0, 0, 0, 0, 0, null);
        if (applied)
            return new TraderSellPressureResult(true, TraderSellPressurePolicy.ResolvePayoutMultiplier(config), 0, 0, 0, 0, "already-applied");

        var multiplier = TraderSellPressurePolicy.ResolvePayoutMultiplier(config);
        var rollback = new List<Action>();
        var changedLevels = 0;
        var changedTraders = new HashSet<string>(StringComparer.Ordinal);
        double beforeTotal = 0;
        double afterTotal = 0;

        try
        {
            foreach (var traderPair in traders)
            {
                var trader = traderPair.Value;
                if (trader.Base.ItemsBuy is null || trader.Base.LoyaltyLevels is null)
                    continue;

                foreach (var loyalty in trader.Base.LoyaltyLevels)
                {
                    if (loyalty.BuyPriceCoefficient is not { } before
                        || !double.IsFinite(before)
                        || before < 0
                        || before > 100)
                        continue;

                    var target = TraderSellPressurePolicy.ApplyToBuyPriceCoefficient(before, config);
                    if (target <= before + 0.000001)
                        continue;

                    rollback.Add(() => loyalty.BuyPriceCoefficient = before);
                    loyalty.BuyPriceCoefficient = target;
                    if (loyalty.BuyPriceCoefficient is not { } actual || Math.Abs(actual - target) > 0.000001)
                        throw new InvalidOperationException($"Trader sell payout verification failed for trader={traderPair.Key}: targetCoef={target}, actual={loyalty.BuyPriceCoefficient}.");

                    changedLevels++;
                    changedTraders.Add(traderPair.Key.ToString());
                    beforeTotal += before;
                    afterTotal += target;
                }
            }

            applied = true;
            logger.Info($"[Economy Admiral] trader sell pressure applied: preset={config.Preset}, payoutMultiplier={multiplier:0.###}x, traders={changedTraders.Count}, loyaltyLevels={changedLevels}, aggregateCoef={beforeTotal:0.##}->{afterTotal:0.##}");
            return new TraderSellPressureResult(true, multiplier, changedTraders.Count, changedLevels, beforeTotal, afterTotal, null);
        }
        catch (Exception applyException)
        {
            for (var index = rollback.Count - 1; index >= 0; index--)
                rollback[index]();

            throw new InvalidOperationException($"Trader sell pressure transaction rolled back: {applyException.Message}", applyException);
        }
    }
}

public sealed record TraderSellPressureResult(
    bool Applied,
    double PayoutMultiplier,
    int TraderCount,
    int LoyaltyLevelCount,
    double AggregateCoefficientBefore,
    double AggregateCoefficientAfter,
    string? Note);
