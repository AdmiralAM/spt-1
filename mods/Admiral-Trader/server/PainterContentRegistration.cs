using System.Reflection;
using System.Text.Json;
using JetBrains.Annotations;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Server;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;
using IOPath = System.IO.Path;

namespace AdmiralTrader.Server;

/// <summary>
/// Replaces the item-registration portion of the retired Painter DLL. The
/// official content files remain optional and external; no identity is
/// regenerated and Admiral remains fully usable when they are absent.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 11), UsedImplicitly]
public sealed class PainterContentRegistration(
    ModHelper modHelper,
    TemplateTable templateTable,
    InventoryConfig inventoryConfig,
    ItemConfig itemConfig,
    CustomItemService customItemService,
    ISptLogger<PainterContentRegistration> logger) : IOnLoad
{
    private static readonly PainterItem[] Items =
    [
        new("672e2e75d78fe9e90c8cb393", "655c67782a1356436041c9c5", "57864a3d24597754843f8721", "figurine_batman.bundle", "Batman figurine", "Batman", "Rare larger figurine of Batman.", 154000, 154000, "5b47574386f77428ca22b2f1", null, null, null),
        new("684db00229850b2f1f7832c1", "59e3647686f774176a362507", "57864a3d24597754843f8721", "dodo338383899.bundle", "Golden Turd figurine", "Turd", "Awarded to the master of annoying tasks.", 69, 69, "5b47574386f77428ca22b2f1", 69, null, null),
        new("685867727d49afb420c2b29e", "59e3647686f774176a362507", "57864a3d24597754843f8721", "mos115.bundle", "Moscovium (Material 115)", "MC-115", "Sealed compound made from Moscovium.", 69, 69, "5b47574386f77428ca22b2f1", 69, null, null),
        new("668ff5bde41a0cce3b142464", "6489b2b131a2135f0d7d0fcb", "62f109593b54472778797866", "mysterybox.bundle", "Painter's Special Delivery", "Painter Lootbox", "A sealed delivery containing valuable barter items.", 500000, 500000, "5b5f6fa186f77409407a7eb7", 25, 4, 4),
        new("6699546547ad52e0fccf6da9", "6489981f7063b903ff4b8565", "62f109593b54472778797866", "mysterybox_2.bundle", "Painter's War Box", "Painter Warbox", "A sealed delivery containing military equipment.", 500000, 500000, "5b5f6fa186f77409407a7eb7", 30, 4, 3)
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string modPath = modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
        if (!HasPainterContent(modPath))
        {
            logger.Info("Painter content pack not present; optional Painter item layer skipped");
            return Task.CompletedTask;
        }

        int created = 0;
        foreach (PainterItem item in Items)
        {
            MongoId id = new(item.Id);
            if (templateTable.Items.ContainsKey(id)) continue;
            if (!templateTable.Items.ContainsKey(new MongoId(item.CloneId)))
                throw new InvalidDataException($"Painter clone source is unavailable: {item.CloneId}");
            if (!File.Exists(IOPath.Combine(modPath, "bundles", item.Bundle)))
                throw new FileNotFoundException($"Painter bundle is missing: {item.Bundle}");

            customItemService.CreateItemFromClone(new NewItemFromCloneDetails
            {
                ItemTplToClone = new MongoId(item.CloneId),
                ParentId = new MongoId(item.ParentId),
                NewId = id,
                NewItemName = item.Id,
                FleaPriceRoubles = item.FleaPrice,
                HandbookPriceRoubles = item.HandbookPrice,
                HandbookParentId = item.HandbookParent,
                OverrideProperties = new TemplateItemProperties
                {
                    Name = item.Name,
                    ShortName = item.ShortName,
                    Description = item.Description,
                    Prefab = new Prefab { Path = item.Bundle, Rcid = string.Empty },
                    Weight = item.Weight,
                    Width = item.Width,
                    Height = item.Height,
                    BackgroundColor = item.Width is null ? "default" : "blue",
                    CanRequireOnRagfair = false,
                    CanSellOnRagfair = item.Id == "672e2e75d78fe9e90c8cb393"
                },
                Locales = new Dictionary<string, LocaleDetails>
                {
                    ["en"] = new() { Name = item.Name, ShortName = item.ShortName, Description = item.Description },
                    ["ru"] = PainterRussianLocales[item.Id]
                }
            }, Assembly.GetExecutingAssembly());
            created++;
        }

        ConfigurePainterRuntime(modPath);
        logger.Success($"Admiral registered {created} missing Painter content templates with their persistent IDs");
        return Task.CompletedTask;
    }

    private static bool HasPainterContent(string modPath) =>
        Items.All(item => File.Exists(IOPath.Combine(modPath, "bundles", item.Bundle)));

    private void ConfigurePainterRuntime(string modPath)
    {
        foreach ((string id, string cloneId) in Items.Take(3).Select(x => (x.Id, x.CloneId)))
            AddToHallOfFame(new MongoId(id), new MongoId(cloneId));

        itemConfig.LootableItemBlacklist.Add(new MongoId("684db00229850b2f1f7832c1"));
        itemConfig.LootableItemBlacklist.Add(new MongoId("685867727d49afb420c2b29e"));

        string poolPath = IOPath.Combine(modPath, "manifests", "painter-special-delivery-pool.json");
        PainterLootPool pool = JsonSerializer.Deserialize<PainterLootPool>(File.ReadAllText(poolPath), JsonOptions)
            ?? throw new InvalidDataException("Painter Special Delivery loot pool is empty");
        if (pool.RewardTplPool.Count == 0 || pool.RewardTplPool.Keys.Any(x => !templateTable.Items.ContainsKey(new MongoId(x))))
            throw new InvalidDataException("Painter Special Delivery loot pool contains no entries or unknown templates");

        MongoId containerId = new(pool.ContainerId);
        templateTable.Items[containerId].Name = pool.ContainerId;
        inventoryConfig.RandomLootContainers[containerId] = new RewardDetails
        {
            RewardCount = pool.RewardCount,
            FoundInRaid = pool.FoundInRaid,
            RewardTplPool = pool.RewardTplPool.ToDictionary(x => new MongoId(x.Key), x => x.Value)
        };
    }

    private void AddToHallOfFame(MongoId itemId, MongoId cloneId)
    {
        foreach (string hallId in HallOfFameIds)
        {
            if (!templateTable.Items.TryGetValue(new MongoId(hallId), out TemplateItem? hall)) continue;
            if (hall?.Properties?.Slots is null) continue;
            foreach (var slot in hall.Properties.Slots)
            foreach (var filter in slot.Properties?.Filters ?? [])
            {
                if (filter.Filter?.Contains(cloneId) == true) filter.Filter.Add(itemId);
            }
        }
    }

    private static readonly string[] HallOfFameIds =
    [
        "63dbd45917fff4dee40fe16e",
        "65424185a57eea37ed6562e9",
        "6542435ea57eea37ed6562f0"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly Dictionary<string, LocaleDetails> PainterRussianLocales = new()
    {
        ["672e2e75d78fe9e90c8cb393"] = new() { Name = "Фигурка Бэтмена", ShortName = "Бэтмен", Description = "Редкая крупная фигурка Бэтмена." },
        ["684db00229850b2f1f7832c1"] = new() { Name = "Фигурка «Золотая какашка»", ShortName = "Зол. какашка", Description = "Награда для мастера особенно раздражающих поручений." },
        ["685867727d49afb420c2b29e"] = new() { Name = "Московий («Материал 115»)", ShortName = "MC-115", Description = "Герметично упакованный состав на основе московия." },
        ["668ff5bde41a0cce3b142464"] = new() { Name = "Особая посылка Маляра", ShortName = "Посылка Маляра", Description = "Запечатанная посылка с ценными бартерными предметами." },
        ["6699546547ad52e0fccf6da9"] = new() { Name = "Боевой ящик Маляра", ShortName = "Боевой ящик", Description = "Запечатанный ящик с военным снаряжением." }
    };
}

public sealed record PainterLootPool(
    int SchemaVersion,
    string ContainerId,
    int RewardCount,
    bool FoundInRaid,
    Dictionary<string, double> RewardTplPool);

public sealed record PainterItem(
    string Id,
    string CloneId,
    string ParentId,
    string Bundle,
    string Name,
    string ShortName,
    string Description,
    double FleaPrice,
    double HandbookPrice,
    string HandbookParent,
    double? Weight,
    int? Width,
    int? Height);
