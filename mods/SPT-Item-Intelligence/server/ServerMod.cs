using SPTItemIntelligence;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
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
    public SemanticVersioning.Version Version { get; init; } = new("0.5.0");
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
    TraderHelper traderHelper,
    ISptLogger<RequirementDataService> logger)
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
        string snapshot = jsonUtil.Serialize(envelope)!;
        logger.Info("[II TRACE] server profileTplHits=" + Count(jsonUtil.Serialize(profile)!, RequirementDataContract.RuntimeTraceTemplateId)
            + " hideoutTplHits=" + Count(jsonUtil.Serialize(hideoutTable)!, RequirementDataContract.RuntimeTraceTemplateId)
            + " snapshotTplHits=" + Count(snapshot, RequirementDataContract.RuntimeTraceTemplateId)
            + " snapshotChars=" + snapshot.Length);
        return ValueTask.FromResult(snapshot);
    }

    private List<ItemPriceSnapshotEntry> BuildPrices(CancellationToken cancellationToken)
    {
        List<ItemPriceSnapshotEntry> result = new(templateTable.Prices.Count);
        foreach (var (templateId, item) in templateTable.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(item.Type, "Item", StringComparison.OrdinalIgnoreCase)) continue;

            templateTable.Prices.TryGetValue(templateId, out double fleaValue);
            double fallbackValue = handbookHelper.GetTemplatePrice(templateId);
            double traderValue = traderHelper.GetHighestSellToTraderPrice(templateId);
            int width = Math.Max(1, item.Properties?.Width ?? 1);
            int height = Math.Max(1, item.Properties?.Height ?? 1);
            result.Add(new ItemPriceSnapshotEntry(
                templateId.ToString(),
                ToLong(traderValue),
                ResolveBestTraderName(fallbackValue),
                ToLong(fleaValue),
                ToLong(fallbackValue),
                width,
                height));
        }
        return result;
    }

    private string ResolveBestTraderName(double handbookValue)
    {
        double highestPrice = 1d;
        string highestTrader = "Trader";
        foreach (var (_, trader) in traderTable)
        {
            var traderBase = trader.Base;
            var buyBackPercent = 100 - traderBase.LoyaltyLevels?.FirstOrDefault()?.BuyPriceCoefficient;
            double price = Math.Round((buyBackPercent ?? 0) * (handbookValue / 100d), 0);
            if (price <= highestPrice) continue;
            highestPrice = price;
            string nickname = (traderBase.Nickname ?? string.Empty).Trim();
            highestTrader = nickname.Length == 0 ? traderBase.Name : nickname;
        }
        return string.IsNullOrWhiteSpace(highestTrader) ? "Trader" : highestTrader;
    }

    private static long ToLong(double value)
    {
        if (double.IsNaN(value) || value <= 0) return 0;
        if (double.IsPositiveInfinity(value) || value >= long.MaxValue) return long.MaxValue;
        return (long)Math.Round(value);
    }

    private static int Count(string? text, string value)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value)) return 0;
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
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
        logger.Success("SPT Item Intelligence Server v0.5.0 loaded; named trader and requirement-detail snapshot ready");
        return Task.CompletedTask;
    }
}
