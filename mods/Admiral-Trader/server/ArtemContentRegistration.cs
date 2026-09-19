using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;
using IOPath = System.IO.Path;

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
    TradersTable tradersTable,
    TemplateTable templateTable,
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

        TraderAssort assort = modHelper.GetJsonDataFromFile<TraderAssort>(modPath, "external/artem/db/assort.json");
        int roots = assort.Items.Count(item => item.ParentId?.ToString() == "hideout");
        if (roots != 281 || assort.Items.Count != 703)
            throw new InvalidDataException($"Embedded Artem 3.0.0 assort drift: roots={roots}, rows={assort.Items.Count}");
        MongoId[] missing = assort.Items.Select(item => item.Template)
            .Where(template => !templateTable.Items.ContainsKey(template)).Distinct().ToArray();
        if (missing.Length > 0)
            throw new InvalidDataException($"Embedded Artem content references {missing.Length} unavailable templates");
        if (!tradersTable.TryGetValue(new MongoId(LegacyTraderConsolidation.ArtemTraderId), out Trader? compatibility))
            throw new InvalidOperationException("Artem migration compatibility shell is unavailable");
        compatibility.Assort = assort;

        logger.Success("Admiral registered the complete embedded Artem content set; standalone Artem runtime is not required");
    }

    private static void ValidatePackagedContract(string modPath)
    {
        string bundlesPath = IOPath.Combine(modPath, "bundles");
        string customItemsPath = IOPath.Combine(modPath, "db", "CustomItems");
        string questPath = IOPath.Combine(modPath, "db", "CustomQuests", LegacyTraderConsolidation.ArtemTraderId);
        string clothingPath = IOPath.Combine(modPath, "db", "CustomClothing");
        string zonesPath = IOPath.Combine(modPath, "db", "CustomQuestZones");
        string assortPath = IOPath.Combine(modPath, "external", "artem", "db", "assort.json");
        string bundleInventoryPath = IOPath.Combine(modPath, "manifests", "artem-bundle-inventory.json");
        string[] requiredBundles = File.Exists(bundleInventoryPath)
            ? JsonDocument.Parse(File.ReadAllText(bundleInventoryPath)).RootElement.GetProperty("bundles")
                .EnumerateArray().Select(entry => entry.GetProperty("path").GetString()!).ToArray()
            : [];

        if (!File.Exists(IOPath.Combine(modPath, "bundles.json"))
            || !File.Exists(assortPath)
            || requiredBundles.Length != 262
            || requiredBundles.Any(path => !File.Exists(IOPath.Combine(bundlesPath, path)))
            || Directory.GetFiles(customItemsPath, "*.json", SearchOption.TopDirectoryOnly).Length != 6
            || Directory.GetFiles(IOPath.Combine(questPath, "Images"), "*", SearchOption.TopDirectoryOnly).Length != 24
            || Directory.GetFiles(IOPath.Combine(questPath, "Locales"), "*.json", SearchOption.TopDirectoryOnly).Length != 3
            || Directory.GetFiles(IOPath.Combine(questPath, "Quests"), "*.json", SearchOption.TopDirectoryOnly).Length != 1
            || Directory.GetFiles(IOPath.Combine(questPath, "QuestAssort"), "*.json", SearchOption.TopDirectoryOnly).Length != 1
            || Directory.GetFiles(clothingPath, "*.json", SearchOption.TopDirectoryOnly).Length != 1
            || Directory.GetFiles(zonesPath, "*.json", SearchOption.TopDirectoryOnly).Length != 1)
            throw new InvalidDataException("Embedded Artem resource contract is incomplete");
    }
}
