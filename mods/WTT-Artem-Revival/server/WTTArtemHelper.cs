using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils.Cloners;

namespace WTTArtem;

[Injectable(TypePriority = OnLoadOrder.Preload + 1), UsedImplicitly]
public class WTTArtemHelper(
    ISptLogger<WTTArtemHelper> logger,
    ICloner cloner,
    TradersTable tradersTable,
    LocaleTable localesTable)
{
    public void SetTraderUpdateTime(TraderConfig traderConfig, TraderBase baseJson, int refreshTimeSecondsMin, int refreshTimeSecondsMax)
    {
        var traderRefreshRecord = new UpdateTime
        {
            TraderId = baseJson.Id,
            Seconds = new MinMax<int>(refreshTimeSecondsMin, refreshTimeSecondsMax)
        };

        traderConfig.UpdateTime.Add(traderRefreshRecord);
    }

    public void AddTraderWithEmptyAssortToDb(TraderBase traderDetailsToAdd)
    {
        var emptyTraderItemAssortObject = new TraderAssort
        {
            Items = [],
            BarterScheme = new Dictionary<MongoId, List<List<BarterScheme>>>(),
            LoyalLevelItems = new Dictionary<MongoId, int>()
        };

        var traderDataToAdd = new Trader
        {
            Assort = emptyTraderItemAssortObject,
            Base = cloner.Clone(traderDetailsToAdd)!,
            QuestAssort = new Dictionary<string, Dictionary<MongoId, MongoId>>
            {
                { "Started", new Dictionary<MongoId, MongoId>() },
                { "Success", new Dictionary<MongoId, MongoId>() },
                { "Fail", new Dictionary<MongoId, MongoId>() }
            },
            Dialogue = []
        };

        if (!tradersTable.TryAdd(traderDetailsToAdd.Id, traderDataToAdd))
        {
            logger.Warning($"Unable to add trader {traderDetailsToAdd.Id}; an entry with that id already exists");
        }
    }

    public void AddTraderToLocales(TraderBase baseJson, string firstName, string description)
    {
        var locales = localesTable.Global;
        var newTraderId = baseJson.Id;
        var fullName = baseJson.Name;
        var nickName = baseJson.Nickname;
        var location = baseJson.Location;

        foreach (var (_, localeKvP) in locales)
        {
            localeKvP.AddTransformer(lazyloadedLocaleData =>
            {
                lazyloadedLocaleData!.Add($"{newTraderId} FullName", fullName);
                lazyloadedLocaleData.Add($"{newTraderId} FirstName", firstName);
                if (nickName != null) lazyloadedLocaleData.Add($"{newTraderId} Nickname", nickName);
                if (location != null) lazyloadedLocaleData.Add($"{newTraderId} Location", location);
                lazyloadedLocaleData.Add($"{newTraderId} Description", description);
                return lazyloadedLocaleData;
            });
        }
    }

    public void OverwriteTraderAssort(string traderId, TraderAssort newAssorts)
    {
        if (!tradersTable.TryGetValue(traderId, out var traderToEdit))
        {
            logger.Warning($"Unable to update assorts for trader: {traderId}, they couldn't be found on the server");
            return;
        }

        traderToEdit.Assort = newAssorts;
    }
}
