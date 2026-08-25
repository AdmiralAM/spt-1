using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils;
using Path = System.IO.Path;

namespace WTTArtem;

[Injectable(TypePriority = OnLoadOrder.Preload + 2), UsedImplicitly]
public class WTTArtem(
    ModHelper modHelper,
    ImageRouter imageRouter,
    TraderConfig traderConfig,
    RagfairConfig ragfairConfig,
    TimeUtil timeUtil,
    WTTArtemHelper wttArtemHelper,
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    ISptLogger<WTTArtem> logger
) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        var pathToMod = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        var assembly = Assembly.GetExecutingAssembly();

        logger.Info("[Artem Revival] startup begin");
        logger.Info($"[Artem Revival] mod path: {pathToMod}");

        logger.Info("[Artem Revival] loading custom items");
        await wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly);
        logger.Info("[Artem Revival] custom items loaded");

        logger.Info("[Artem Revival] loading custom quest zones");
        await wttCommon.CustomQuestZoneService.CreateCustomQuestZones(assembly);
        logger.Info("[Artem Revival] custom quest zones loaded");

        var traderImagePath = Path.Combine(pathToMod, "res/66bf757f27d0b097db0acea5.jpg");
        var traderBase = modHelper.GetJsonDataFromFile<TraderBase>(pathToMod, "db/base.json");

        logger.Info($"[Artem Revival] registering trader {traderBase.Id}");
        imageRouter.AddRoute(traderBase.Avatar!.Replace(".jpg", ""), traderImagePath);
        wttArtemHelper.SetTraderUpdateTime(traderConfig, traderBase, timeUtil.GetHoursAsSeconds(1), timeUtil.GetHoursAsSeconds(2));
        ragfairConfig.Traders.TryAdd(traderBase.Id, true);
        wttArtemHelper.AddTraderWithEmptyAssortToDb(traderBase);
        wttArtemHelper.AddTraderToLocales(traderBase, "Artem", "[REDACTED]");
        logger.Info("[Artem Revival] trader registered");

        logger.Info("[Artem Revival] loading custom quests");
        await wttCommon.CustomQuestService.CreateCustomQuests(assembly);
        logger.Info("[Artem Revival] custom quests loaded");

        logger.Info("[Artem Revival] loading custom clothing");
        await wttCommon.CustomClothingService.CreateCustomClothing(assembly);
        logger.Info("[Artem Revival] custom clothing loaded");

        logger.Info("[Artem Revival] loading trader assort");
        var assort = modHelper.GetJsonDataFromFile<TraderAssort>(pathToMod, "db/assort.json");
        wttArtemHelper.OverwriteTraderAssort(traderBase.Id, assort);
        logger.Info($"[Artem Revival] trader assort loaded: {assort.Items.Count} item records");

        logger.Info("[Artem Revival] startup complete");
    }
}
