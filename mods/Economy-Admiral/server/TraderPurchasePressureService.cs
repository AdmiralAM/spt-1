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
        "5d235b4d86f7742e017bc88a", // GP coin
        "6656560053eaaa7a23349c86", // Lega medal
    };

    private bool applied;

    public TraderPurchasePressureResult Apply(EconomyConfig config)
    {
        if (!config.EnableTraderPurchasePressure || config.Mode != EconomyMode.Enforce)
            return new TraderPurchasePressureResult(false, 1.0, 0, 0, 0, null);
        if (applied)
            return new TraderPurchasePressureResult(true, ResolveMultiplier(config), 0, 0, 0, "already-applied");

        var multiplier = ResolveMultiplier(config);
        if (!double.IsFinite(multiplier) || multiplier < 1.0 || multiplier > 2.0)
            throw new InvalidOperationException($"Trader purchase pressure multiplier must be finite and within 1.0..2.0, got {multiplier}.");

        var journal = new List<(object Scheme, double Before)>();
        var changedOffers = 0;
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
                    if (alternatives is null || alternatives.Count != 1)
                        continue;
                    var requirements = alternatives[0];
                    if (requirements is null || requirements.Count != 1)
                        continue;

                    var requirement = requirements[0];
                    var template = requirement.Template.ToString();
                    if (!CurrencyTemplates.Contains(template) || requirement.Count is null)
                        continue;

                    var before = requirement.Count.Value;
                    if (!double.IsFinite(before) || before <= 0)
                        continue;

                    var target = Math.Ceiling(before * multiplier);
                    if (target <= before)
                        continue;

                    journal.Add((requirement, before));
                    requirement.Count = target;
                    if (Math.Abs(requirement.Count.Value - target) > 0.000001)
                        throw new InvalidOperationException($"Trader price verification failed for trader={traderPair.Key}, offer={offerPair.Key}: target={target}, actual={requirement.Count}.");

                    changedOffers++;
                    changedTraders.Add(traderPair.Key.ToString());
                    beforeTotal += before;
                    afterTotal += target;
                }
            }

            applied = true;
            logger.Info($"[Economy Admiral] trader purchase pressure applied: preset={config.Preset}, multiplier={multiplier:0.###}x, traders={changedTraders.Count}, offers={changedOffers}, aggregate={beforeTotal:0.##}->{afterTotal:0.##}");
            return new TraderPurchasePressureResult(true, multiplier, changedTraders.Count, changedOffers, beforeTotal, afterTotal, null);
        }
        catch (Exception applyException)
        {
            for (var index = journal.Count - 1; index >= 0; index--)
            {
                dynamic requirement = journal[index].Scheme;
                requirement.Count = journal[index].Before;
            }

            throw new InvalidOperationException($"Trader purchase pressure transaction rolled back: {applyException.Message}", applyException);
        }
    }

    public static double ResolveMultiplier(EconomyConfig config) => config.Preset switch
    {
        EconomyPreset.Easy => 1.05,
        EconomyPreset.Normal => 1.15,
        EconomyPreset.Hard => 1.30,
        EconomyPreset.Custom => config.CustomTraderPurchasePriceMultiplier,
        _ => throw new ArgumentOutOfRangeException(nameof(config.Preset), config.Preset, "Unsupported economy preset."),
    };
}

public sealed record TraderPurchasePressureResult(
    bool Applied,
    double Multiplier,
    int TraderCount,
    int OfferCount,
    double AggregateBefore,
    double? AggregateAfter,
    string? Note);
