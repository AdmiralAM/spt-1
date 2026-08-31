using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class TraderPurchasePressureService(
    TradersTable traders,
    ISptLogger<TraderPurchasePressureService> logger)
{
    private static readonly HashSet<string> CurrencyTemplates = new(StringComparer.Ordinal)
    {
        "5449016a4bdc2d6f028b456f", // RUB
        "569668774bdc2da2298b4568", // USD
        "5696686a4bdc2da3298b456a", // EUR
    };

    private bool applied;

    public TraderPurchasePressureResult Apply(EconomyConfig config)
    {
        if (!config.EnableTraderPurchasePressure || config.Mode != EconomyMode.Enforce)
            return new TraderPurchasePressureResult(false, 1.0, 0, 0, 0, 0, null);
        if (applied)
            return new TraderPurchasePressureResult(true, TraderPurchasePressurePolicy.ResolveMultiplier(config), 0, 0, 0, 0, "already-applied");

        var multiplier = TraderPurchasePressurePolicy.ResolveMultiplier(config);
        var rollback = new List<Action>();
        var changedOffers = new HashSet<string>(StringComparer.Ordinal);
        var changedAlternatives = 0;
        var changedTraders = new HashSet<string>(StringComparer.Ordinal);
        double beforeTotal = 0;
        double afterTotal = 0;

        try
        {
            foreach (var traderPair in traders)
            {
                var trader = traderPair.Value;
                if (trader.Assort?.BarterScheme is null)
                    continue;

                foreach (var offerPair in trader.Assort.BarterScheme)
                {
                    var alternatives = offerPair.Value;
                    if (alternatives is null)
                        continue;

                    // Each barter-scheme entry is an independent way to pay for the same offer.
                    // Pressure every structurally pure fiat alternative while preserving authored
                    // barter/token/mixed alternatives byte-for-byte. Previously the entire offer
                    // was skipped whenever more than one payment alternative existed.
                    foreach (var requirements in alternatives)
                    {
                        if (requirements is null || requirements.Count != 1)
                            continue;

                        var requirement = requirements[0];
                        var template = requirement.Template.ToString();
                        if (!CurrencyTemplates.Contains(template) || requirement.Count is null)
                            continue;

                        var before = requirement.Count.Value;
                        if (!double.IsFinite(before) || before <= 0)
                            continue;

                        var target = TraderPurchasePressurePolicy.ApplyToCurrencyCost(before, config);
                        if (target <= before)
                            continue;

                        rollback.Add(() => requirement.Count = before);
                        requirement.Count = target;
                        if (Math.Abs(requirement.Count.Value - target) > 0.000001)
                            throw new InvalidOperationException($"Trader price verification failed for trader={traderPair.Key}, offer={offerPair.Key}: target={target}, actual={requirement.Count}.");

                        changedAlternatives++;
                        changedOffers.Add($"{traderPair.Key}:{offerPair.Key}");
                        changedTraders.Add(traderPair.Key.ToString());
                        beforeTotal += before;
                        afterTotal += target;
                    }
                }
            }

            applied = true;
            logger.Info($"[Economy Admiral] trader fiat purchase pressure applied: preset={config.Preset}, multiplier={multiplier:0.###}x, traders={changedTraders.Count}, offers={changedOffers.Count}, fiatAlternatives={changedAlternatives}, aggregate={beforeTotal:0.##}->{afterTotal:0.##}");
            return new TraderPurchasePressureResult(true, multiplier, changedTraders.Count, changedOffers.Count, beforeTotal, afterTotal, null);
        }
        catch (Exception applyException)
        {
            for (var index = rollback.Count - 1; index >= 0; index--)
                rollback[index]();

            throw new InvalidOperationException($"Trader purchase pressure transaction rolled back: {applyException.Message}", applyException);
        }
    }
}

public sealed record TraderPurchasePressureResult(
    bool Applied,
    double Multiplier,
    int TraderCount,
    int OfferCount,
    double AggregateBefore,
    double AggregateAfter,
    string? Note);
