using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Modding.Custom;

namespace SPTBeltArmbandInventory.Server;

[Injectable(TypePriority = OnLoadOrder.Preload + 3)]
public sealed class DedicatedWearableItems(
    TemplateTable templateTable,
    CustomItemService customItemService,
    ISptLogger<DedicatedWearableItems> logger) : IOnLoad
{
    private static readonly MongoId SourceArmbandTpl = new("5b3f3af486f774679e752c1f");
    private static readonly MongoId BeltParentTpl = new(RuntimeIdentity.BeltItemParentId);
    private static readonly MongoId HeadBandParentTpl = new(RuntimeIdentity.HeadBandItemParentId);
    private const string GridPrototype = "55d329c24bdc2d892f8b4567";

    // HeadBand is a compact personal-utility carrier rather than a second medical pouch.
    // Exact item IDs are deliberate here: accepting broad barter/money parents would turn
    // this slot into a generic secure container. Stack limits remain owned by the items.
    private static readonly HashSet<MongoId> HeadBandUtilityWhitelist =
    [
        // Currency
        new("5449016a4bdc2d6f028b456f"), // RUB
        new("5696686a4bdc2da3298b456a"), // USD
        new("569668774bdc2da2298b4568"), // EUR

        // Cigarettes
        new("573475fb24597737fb1379e1"), // Apollo Soyuz
        new("573476d324597737da2adc13"), // Malboro
        new("573476f124597737e04bf328"), // Wilston
        new("5734770f24597738025ee254"), // Strike

        // Compact vanilla wallet; its own internal filter/capacity remains authoritative.
        new("5783c43d2459774bbe137486")
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == SourceArmbandTpl)
            ?? throw new InvalidOperationException("B&A&HB dedicated wearable source handbook entry missing.");

        EnsureItem(
            RuntimeIdentity.DedicatedMagazineBeltItemId,
            RuntimeIdentity.DedicatedMagazineBeltGridId,
            BeltParentTpl,
            "B&A&HB Magazine Belt",
            "Magazine Belt",
            "Dedicated 2x2 tactical magazine belt worn in the B&A&HB Belt equipment location.",
            RuntimeIdentity.DedicatedMagazineBeltGridColumns,
            RuntimeIdentity.DedicatedMagazineBeltGridRows,
            [BaseClasses.MAGAZINE],
            handbookItem.ParentId,
            45000);

        EnsureItem(
            RuntimeIdentity.EmergencyHeadBandItemId,
            RuntimeIdentity.EmergencyHeadBandGridId,
            HeadBandParentTpl,
            "B&A&HB Utility HeadBand",
            "Utility HB",
            "Protected compact 1x2 HeadBand utility carrier for currency, cigarettes and a compact wallet.",
            RuntimeIdentity.EmergencyHeadBandGridColumns,
            RuntimeIdentity.EmergencyHeadBandGridRows,
            HeadBandUtilityWhitelist,
            handbookItem.ParentId,
            25000);

        logger.Success("B&A&HB #2 dedicated Belt and HeadBand container items registered.");
        return Task.CompletedTask;
    }

    private void EnsureItem(
        string itemId,
        string gridId,
        MongoId parent,
        string name,
        string shortName,
        string description,
        int cellsH,
        int cellsV,
        HashSet<MongoId> accepted,
        MongoId handbookParent,
        double price)
    {
        var id = new MongoId(itemId);
        if (templateTable.Items.TryGetValue(id, out var existing))
        {
            Validate(existing, itemId, gridId, parent, cellsH, cellsV, accepted);
            return;
        }

        var details = new NewItemFromCloneDetails
        {
            NewItemName = name,
            ItemTplToClone = SourceArmbandTpl,
            ParentId = parent,
            NewId = itemId,
            FleaPriceRoubles = price,
            HandbookPriceRoubles = price,
            HandbookParentId = handbookParent,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails { Name = name, ShortName = shortName, Description = description }
            },
            OverrideProperties = new TemplateItemProperties
            {
                BackgroundColor = "blue",
                ExaminedByDefault = true,
                Grids =
                [
                    new Grid
                    {
                        Name = "main",
                        Id = gridId,
                        Parent = itemId,
                        Prototype = GridPrototype,
                        Properties = new GridProperties
                        {
                            CellsH = cellsH,
                            CellsV = cellsV,
                            MinCount = 0,
                            MaxCount = 0,
                            MaxWeight = 0,
                            IsSortingTable = false,
                            Filters =
                            [
                                new GridFilter
                                {
                                    Filter = accepted,
                                    ExcludedFilter = []
                                }
                            ]
                        }
                    }
                ]
            }
        };

        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success)
            throw new InvalidOperationException($"B&A&HB dedicated wearable creation failed for {itemId}: {string.Join("; ", result.Errors)}");
    }

    private static void Validate(
        TemplateItem item,
        string itemId,
        string gridId,
        MongoId parent,
        int cellsH,
        int cellsV,
        HashSet<MongoId> accepted)
    {
        if (!Equals(item.Parent, parent))
            throw new InvalidOperationException($"B&A&HB dedicated wearable parent collision for {itemId}.");

        var grids = item.Properties?.Grids?.ToArray();
        if (grids == null || grids.Length != 1)
            throw new InvalidOperationException($"B&A&HB dedicated wearable grid collision for {itemId}.");

        var grid = grids[0];
        var props = grid.Properties;
        if (!string.Equals(grid.Id, gridId, StringComparison.Ordinal)
            || !string.Equals(grid.Parent, itemId, StringComparison.Ordinal)
            || props?.CellsH != cellsH
            || props.CellsV != cellsV)
            throw new InvalidOperationException($"B&A&HB dedicated wearable geometry collision for {itemId}.");

        var filters = props.Filters?.ToArray();
        var actual = filters?.Length == 1 ? filters[0].Filter : null;
        if (actual == null || !actual.SetEquals(accepted))
            throw new InvalidOperationException($"B&A&HB dedicated wearable filter collision for {itemId}.");
    }
}
