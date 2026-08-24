using SPTItemIntelligence;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace SPTItemIntelligence.Server;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.admiralam.spt.itemintelligence.server";
    public string Name { get; init; } = "SPT Item Intelligence Server";
    public string Author { get; init; } = "AdmiralAM";
    public List<string>? Contributors { get; init; }
    public SemanticVersioning.Version Version { get; init; } = new("0.10.1");
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1.0");
    public List<string>? Incompatibilities { get; init; }
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public string? Url { get; init; } = "https://github.com/AdmiralAM/spt-1";
    public string License { get; init; } = "MIT";
    public bool HasPrepatcher { get; init; } = false;
}

[Injectable]
public sealed class RequirementDataService(
    JsonUtil jsonUtil,
    ProfileHelper profileHelper,
    TemplateTable templateTable,
    TradersTable traderTable,
    HideoutTable hideoutTable,
    HandbookHelper handbookHelper,
    ItemHelper itemHelper,
    PresetHelper presetHelper)
{
    public ValueTask<string> BuildSnapshotAsync(MongoId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object? profile = profileHelper.GetPmcProfile(sessionId);
        List<ItemPriceSnapshotEntry> prices = BuildPrices(cancellationToken);
        RequirementDataEnvelope envelope = new(
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            profile!,
            templateTable.Quests,
            hideoutTable,
            prices);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(jsonUtil.Serialize(envelope)!);
    }

    private List<ItemPriceSnapshotEntry> BuildPrices(CancellationToken cancellationToken)
    {
        List<ItemPriceSnapshotEntry> result = new(templateTable.Prices.Count);
        foreach (var (templateId, item) in templateTable.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(item.Type, "Item", StringComparison.OrdinalIgnoreCase)) continue;

            templateTable.Prices.TryGetValue(templateId, out double fleaValue);
            double handbookValue = handbookHelper.GetTemplatePrice(templateId);
            double traderBasis = GetTraderValuationBasis(templateId, handbookValue);
            var trader = ResolveBestTrader(templateId, traderBasis);
            int width = Math.Max(1, item.Properties?.Width ?? 1);
            int height = Math.Max(1, item.Properties?.Height ?? 1);
            result.Add(new ItemPriceSnapshotEntry(
                templateId.ToString(),
                ToLong(trader.Price),
                trader.Name,
                ToLong(fleaValue),
                ToLong(handbookValue),
                width,
                height));
        }
        return result;
    }

    // Item Valuation prices default weapon/equipment presets as the sum of their children,
    // rather than valuing only the bare root template. Mirror that established behaviour.
    private double GetTraderValuationBasis(MongoId templateId, double handbookValue)
    {
        var preset = presetHelper.GetDefaultPreset(templateId);
        if (preset?.Items is null || preset.Items.Count == 0) return handbookValue;

        double total = 0;
        foreach (var presetItem in preset.Items)
            total += handbookHelper.GetTemplatePrice(presetItem.Template);
        return total > 0 ? total : handbookValue;
    }

    // Match Item Valuation: eligible trader buy categories + LL1 buy-back coefficient,
    // regular traders first, Fence only as fallback.
    private (double Price, string Name) ResolveBestTrader(MongoId templateId, double valuationBasis)
    {
        var regular = ResolveBestTrader(templateId, valuationBasis, includeFence: false);
        return regular.Price > 0 ? regular : ResolveBestTrader(templateId, valuationBasis, includeFence: true);
    }

    private (double Price, string Name) ResolveBestTrader(MongoId templateId, double valuationBasis, bool includeFence)
    {
        double highestPrice = 0;
        string highestTrader = "Trader";
        foreach (var (traderId, trader) in traderTable)
        {
            bool isFence = traderId == Traders.FENCE;
            if (isFence != includeFence) continue;

            var traderBase = trader.Base;
            var buy = traderBase.ItemsBuy;
            if (buy is null) continue;
            bool accepts = buy.IdList.Contains(templateId) || itemHelper.IsOfBaseclasses(templateId, buy.Category);
            if (!accepts) continue;

            double coefficient = traderBase.LoyaltyLevels?.FirstOrDefault()?.BuyPriceCoefficient ?? 100d;
            double price = Math.Round(Math.Max(0d, 100d - coefficient) * (valuationBasis / 100d), 0);
            if (price <= highestPrice) continue;

            highestPrice = price;
            string nickname = (traderBase.Nickname ?? string.Empty).Trim();
            highestTrader = nickname.Length == 0 ? traderBase.Name : nickname;
        }
        return (highestPrice, string.IsNullOrWhiteSpace(highestTrader) ? "Trader" : highestTrader);
    }

    private static long ToLong(double value)
    {
        if (double.IsNaN(value) || value <= 0) return 0;
        if (double.IsPositiveInfinity(value) || value >= long.MaxValue) return long.MaxValue;
        return (long)Math.Round(value);
    }
}

[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public sealed class ItemIntelligenceRouter(JsonUtil jsonUtil, RequirementDataService dataService)
    : StaticRouter(
        jsonUtil,
        [
            new RouteAction(
                RequirementDataContract.SnapshotRoute,
                async (url, info, sessionId, output, cancellationToken) =>
                    await dataService.BuildSnapshotAsync(sessionId, cancellationToken)
            )
        ])
{ }

[Injectable(TypePriority = OnLoadOrder.PostLoad)]
public sealed class ItemIntelligenceLoadNotice(ISptLogger<ItemIntelligenceLoadNotice> logger) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.Success("SPT Item Intelligence Server v0.10.1 loaded; named trader and requirement-detail snapshot ready");
        return Task.CompletedTask;
    }
}
