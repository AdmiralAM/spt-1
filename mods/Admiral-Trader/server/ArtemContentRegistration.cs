using System.Reflection;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;

namespace AdmiralTrader.Server;

/// <summary>
/// Publishes the preserved Artem item, quest-zone, quest and clothing records
/// from the Admiral package. The legacy trader identity is only a temporary
/// profile-migration shell and is never exposed as a standalone storefront.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 3), UsedImplicitly]
public sealed class ArtemContentRegistration(
    ModHelper modHelper,
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    ISptLogger<ArtemContentRegistration> logger) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assembly assembly = Assembly.GetExecutingAssembly();
        string modPath = modHelper.GetAbsolutePathToModFolder(assembly);
        ValidatePackagedContract(modPath);

        await wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly);
        await wttCommon.CustomQuestZoneService.CreateCustomQuestZones(assembly);
        await wttCommon.CustomQuestService.CreateCustomQuests(assembly);
        await wttCommon.CustomClothingService.CreateCustomClothing(assembly);

        logger.Success("Admiral registered the complete embedded Artem content set; standalone Artem runtime is not required");
    }

    private static void ValidatePackagedContract(string modPath)
    {
        string bundlesPath = Path.Combine(modPath, "bundles");
        string customItemsPath = Path.Combine(modPath, "db", "CustomItems");
        string questPath = Path.Combine(modPath, "db", "CustomQuests", LegacyTraderConsolidation.ArtemTraderId);
        string clothingPath = Path.Combine(modPath, "db", "CustomClothing");
        string zonesPath = Path.Combine(modPath, "db", "CustomQuestZones");
        string assortPath = Path.Combine(modPath, "external", "artem", "db", "assort.json");

        if (!File.Exists(Path.Combine(modPath, "bundles.json"))
            || !File.Exists(assortPath)
            || Directory.GetFiles(bundlesPath, "*.bundle", SearchOption.AllDirectories).Length != 262
            || Directory.GetFiles(customItemsPath, "*.json", SearchOption.TopDirectoryOnly).Length != 6
            || Directory.GetFiles(Path.Combine(questPath, "Images"), "*", SearchOption.TopDirectoryOnly).Length != 24
            || Directory.GetFiles(Path.Combine(questPath, "Locales"), "*.json", SearchOption.TopDirectoryOnly).Length != 3
            || Directory.GetFiles(Path.Combine(questPath, "Quests"), "*.json", SearchOption.TopDirectoryOnly).Length != 1
            || Directory.GetFiles(Path.Combine(questPath, "QuestAssort"), "*.json", SearchOption.TopDirectoryOnly).Length != 1
            || Directory.GetFiles(clothingPath, "*.json", SearchOption.TopDirectoryOnly).Length != 1
            || Directory.GetFiles(zonesPath, "*.json", SearchOption.TopDirectoryOnly).Length != 1)
            throw new InvalidDataException("Embedded Artem resource contract is incomplete");
    }
}
