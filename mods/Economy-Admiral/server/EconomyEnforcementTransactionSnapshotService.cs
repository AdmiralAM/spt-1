using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTEconomy;

[Injectable]
public sealed class EconomyEnforcementTransactionSnapshotService(
    TemplateTable templates,
    TradersTable traders,
    RagfairConfig ragfair,
    GlobalTable globalTable,
    LocationConfig locationConfig)
{
    public EconomyEnforcementTransactionSnapshot Capture(EconomyConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var entries = new List<EconomyRollbackEntry>();

        if (config.EnableQuestEconomyCluster)
            CaptureQuestRewards(entries);
        if (config.EnableTraderPurchasePressure)
            CaptureTraderPurchaseCosts(entries);
        if (config.EnableTraderSellPressure)
            CaptureTraderSellCoefficients(entries);
        if (config.EnableFleaPurchasePressure)
            CaptureFleaPurchaseSettings(entries);
        if (config.EnableFleaListingFeePressure)
            CaptureFleaListingFees(entries);
        if (config.EnableLootPressure)
            CaptureLootMultipliers(entries, config);

        return new EconomyEnforcementTransactionSnapshot(entries);
    }

    private void CaptureQuestRewards(List<EconomyRollbackEntry> entries)
    {
        foreach (var questPair in templates.Quests)
        {
            var quest = questPair.Value;
            if (quest.Rewards is null)
                continue;

            foreach (var rewardGroup in quest.Rewards.Values)
            {
                foreach (var reward in rewardGroup)
                {
                    var rewardValue = reward.Value;
                    entries.Add(new(
                        $"quest:{questPair.Key}:reward-value",
                        () => reward.Value = rewardValue,
                        () => Nullable.Equals(reward.Value, rewardValue)));

                    if (reward.Items is null)
                        continue;
                    foreach (var item in reward.Items)
                    {
                        if (item.Upd is null)
                            continue;
                        var stackCount = item.Upd.StackObjectsCount;
                        entries.Add(new(
                            $"quest:{questPair.Key}:item-stack:{item.Template}",
                            () => item.Upd.StackObjectsCount = stackCount,
                            () => Nullable.Equals(item.Upd.StackObjectsCount, stackCount)));
                    }
                }
            }
        }
    }

    private void CaptureTraderPurchaseCosts(List<EconomyRollbackEntry> entries)
    {
        foreach (var traderPair in traders)
        {
            var barterScheme = traderPair.Value.Assort?.BarterScheme;
            if (barterScheme is null)
                continue;
            foreach (var offerPair in barterScheme)
            {
                if (offerPair.Value is null)
                    continue;
                foreach (var alternative in offerPair.Value)
                {
                    if (alternative is null)
                        continue;
                    foreach (var requirement in alternative)
                    {
                        var count = requirement.Count;
                        entries.Add(new(
                            $"trader:{traderPair.Key}:offer:{offerPair.Key}:requirement:{requirement.Template}",
                            () => requirement.Count = count,
                            () => Nullable.Equals(requirement.Count, count)));
                    }
                }
            }
        }
    }

    private void CaptureTraderSellCoefficients(List<EconomyRollbackEntry> entries)
    {
        foreach (var traderPair in traders)
        {
            var loyaltyLevels = traderPair.Value.Base.LoyaltyLevels;
            if (loyaltyLevels is null)
                continue;
            for (var index = 0; index < loyaltyLevels.Length; index++)
            {
                var loyalty = loyaltyLevels[index];
                var coefficient = loyalty.BuyPriceCoefficient;
                entries.Add(new(
                    $"trader:{traderPair.Key}:loyalty:{index}:buy-price-coefficient",
                    () => loyalty.BuyPriceCoefficient = coefficient,
                    () => Nullable.Equals(loyalty.BuyPriceCoefficient, coefficient)));
            }
        }
    }

    private void CaptureFleaPurchaseSettings(List<EconomyRollbackEntry> entries)
    {
        var generate = ragfair.Dynamic.GenerateBaseFleaPrices;
        var adjustment = ragfair.Dynamic.OfferAdjustment;

        var priceMultiplier = generate.PriceMultiplier;
        entries.Add(new("flea:generate:price-multiplier", () => generate.PriceMultiplier = priceMultiplier, () => generate.PriceMultiplier.Equals(priceMultiplier)));

        var preventBelowTrader = generate.PreventPriceBeingBelowTraderBuyPrice;
        entries.Add(new("flea:generate:prevent-below-trader", () => generate.PreventPriceBeingBelowTraderBuyPrice = preventBelowTrader, () => generate.PreventPriceBeingBelowTraderBuyPrice == preventBelowTrader));

        var adjustBelowHandbook = adjustment.AdjustPriceWhenBelowHandbookPrice;
        entries.Add(new("flea:adjust:below-handbook-enabled", () => adjustment.AdjustPriceWhenBelowHandbookPrice = adjustBelowHandbook, () => adjustment.AdjustPriceWhenBelowHandbookPrice == adjustBelowHandbook));

        var maxDifference = adjustment.MaxPriceDifferenceBelowHandbookPercent;
        entries.Add(new("flea:adjust:max-below-handbook", () => adjustment.MaxPriceDifferenceBelowHandbookPercent = maxDifference, () => adjustment.MaxPriceDifferenceBelowHandbookPercent.Equals(maxDifference)));

        var handbookMultiplier = adjustment.HandbookPriceMultiplier;
        entries.Add(new("flea:adjust:handbook-multiplier", () => adjustment.HandbookPriceMultiplier = handbookMultiplier, () => adjustment.HandbookPriceMultiplier.Equals(handbookMultiplier)));
    }

    private void CaptureFleaListingFees(List<EconomyRollbackEntry> entries)
    {
        var globalRagfair = globalTable.Configuration.RagFair;
        var itemTax = globalRagfair.CommunityItemTax;
        entries.Add(new("flea:tax:item", () => globalRagfair.CommunityItemTax = itemTax, () => globalRagfair.CommunityItemTax.Equals(itemTax)));

        var requirementTax = globalRagfair.CommunityRequirementTax;
        entries.Add(new("flea:tax:requirement", () => globalRagfair.CommunityRequirementTax = requirementTax, () => globalRagfair.CommunityRequirementTax.Equals(requirementTax)));
    }

    private void CaptureLootMultipliers(List<EconomyRollbackEntry> entries, EconomyConfig config)
    {
        if (config.EnableLooseLootPressure)
        {
            foreach (var pair in locationConfig.LooseLootMultiplier)
            {
                var key = pair.Key;
                var value = pair.Value;
                entries.Add(new(
                    $"loot:loose:{key}",
                    () => locationConfig.LooseLootMultiplier[key] = value,
                    () => locationConfig.LooseLootMultiplier.TryGetValue(key, out var current) && current.Equals(value)));
            }
        }

        if (config.EnableStaticLootPressure)
        {
            foreach (var pair in locationConfig.StaticLootMultiplier)
            {
                var key = pair.Key;
                var value = pair.Value;
                entries.Add(new(
                    $"loot:static:{key}",
                    () => locationConfig.StaticLootMultiplier[key] = value,
                    () => locationConfig.StaticLootMultiplier.TryGetValue(key, out var current) && current.Equals(value)));
            }
        }
    }
}

public sealed class EconomyEnforcementTransactionSnapshot
{
    private readonly IReadOnlyList<EconomyRollbackEntry> entries;
    private int rolledBack;

    internal EconomyEnforcementTransactionSnapshot(IReadOnlyList<EconomyRollbackEntry> entries)
    {
        this.entries = entries;
    }

    public int EntryCount => entries.Count;

    public void RollbackAndVerify()
    {
        if (Interlocked.Exchange(ref rolledBack, 1) != 0)
            return;

        var failures = new List<string>();
        for (var index = entries.Count - 1; index >= 0; index--)
        {
            var entry = entries[index];
            try
            {
                entry.Restore();
                if (!entry.Verify())
                    failures.Add($"{entry.Label}: verification mismatch");
            }
            catch (Exception exception)
            {
                failures.Add($"{entry.Label}: {exception.Message}");
            }
        }

        if (failures.Count > 0)
            throw new InvalidOperationException(
                $"Economy Admiral full Enforce rollback could not be proven for {failures.Count} snapshot entries: {string.Join("; ", failures.Take(8))}");
    }
}

internal sealed record EconomyRollbackEntry(string Label, Action Restore, Func<bool> Verify);
