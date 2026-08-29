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
    internal const string HeadBandCurrencyGridName = "main";
    internal const string HeadBandCigarettesGridName = "cigarettes";

    private static readonly HashSet<MongoId> HeadBandCurrencyWalletWhitelist =
        HeadBandUtilityPolicy.CurrencyWalletTemplateIds.Select(id => new MongoId(id)).ToHashSet();
    private static readonly HashSet<MongoId> HeadBandCigaretteWhitelist =
        HeadBandUtilityPolicy.CigaretteTemplateIds.Select(id => new MongoId(id)).ToHashSet();

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == SourceArmbandTpl)
            ?? throw new InvalidOperationException("B&A&HB dedicated wearable source handbook entry missing.");

        EnsureSingleGridItem(
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

        EnsureHeadBand(handbookItem.ParentId);

        logger.Success("B&A&HB dedicated Magazine Belt and Utility HeadBand items registered; HeadBand uses native currency/wallet + cigarettes 1x1 grids.");
        return Task.CompletedTask;
    }

    private void EnsureHeadBand(MongoId handbookParent)
    {
        var id = new MongoId(RuntimeIdentity.EmergencyHeadBandItemId);
        if (templateTable.Items.TryGetValue(id, out var existing))
        {
            ValidateHeadBand(existing);
            return;
        }

        var details = new NewItemFromCloneDetails
        {
            NewItemName = "B&A&HB Utility HeadBand",
            ItemTplToClone = SourceArmbandTpl,
            ParentId = HeadBandParentTpl,
            NewId = RuntimeIdentity.EmergencyHeadBandItemId,
            FleaPriceRoubles = 25000,
            HandbookPriceRoubles = 25000,
            HandbookParentId = handbookParent,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = "B&A&HB Utility HeadBand",
                    ShortName = "Utility HB",
                    Description = "Compact HeadBand utility carrier with separate currency/wallet and cigarette pockets. Death protection follows the B&A&HB F12 setting."
                }
            },
            OverrideProperties = new TemplateItemProperties
            {
                BackgroundColor = "blue",
                ExaminedByDefault = true,
                Grids =
                [
                    CreateGrid(
                        HeadBandCurrencyGridName,
                        RuntimeIdentity.EmergencyHeadBandGridId,
                        RuntimeIdentity.EmergencyHeadBandItemId,
                        RuntimeIdentity.EmergencyHeadBandSplitGridColumns,
                        RuntimeIdentity.EmergencyHeadBandSplitGridRows,
                        HeadBandCurrencyWalletWhitelist),
                    CreateGrid(
                        HeadBandCigarettesGridName,
                        RuntimeIdentity.EmergencyHeadBandCigarettesGridId,
                        RuntimeIdentity.EmergencyHeadBandItemId,
                        RuntimeIdentity.EmergencyHeadBandSplitGridColumns,
                        RuntimeIdentity.EmergencyHeadBandSplitGridRows,
                        HeadBandCigaretteWhitelist)
                ]
            }
        };

        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success)
            throw new InvalidOperationException($"B&A&HB Utility HeadBand creation failed: {string.Join("; ", result.Errors)}");
    }

    private void EnsureSingleGridItem(
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
            ValidateSingleGrid(existing, itemId, gridId, parent, cellsH, cellsV, accepted);
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
                Grids = [CreateGrid("main", gridId, itemId, cellsH, cellsV, accepted)]
            }
        };

        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success)
            throw new InvalidOperationException($"B&A&HB dedicated wearable creation failed for {itemId}: {string.Join("; ", result.Errors)}");
    }

    private static Grid CreateGrid(string name, string gridId, string itemId, int cellsH, int cellsV, HashSet<MongoId> accepted)
    {
        return new Grid
        {
            Name = name,
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
                Filters = [new GridFilter { Filter = accepted, ExcludedFilter = [] }]
            }
        };
    }

    private static void ValidateHeadBand(TemplateItem item)
    {
        if (!Equals(item.Parent, HeadBandParentTpl))
            throw new InvalidOperationException("B&A&HB Utility HeadBand parent collision.");

        var grids = item.Properties?.Grids?.ToArray();
        if (grids == null || grids.Length != 2)
            throw new InvalidOperationException("B&A&HB Utility HeadBand requires exactly two native 1x1 grids.");

        ValidateGrid(grids, HeadBandCurrencyGridName, RuntimeIdentity.EmergencyHeadBandGridId, HeadBandCurrencyWalletWhitelist);
        ValidateGrid(grids, HeadBandCigarettesGridName, RuntimeIdentity.EmergencyHeadBandCigarettesGridId, HeadBandCigaretteWhitelist);
    }

    private static void ValidateGrid(Grid[] grids, string name, string gridId, HashSet<MongoId> accepted)
    {
        var grid = grids.SingleOrDefault(x => string.Equals(x.Name, name, StringComparison.Ordinal));
        var props = grid?.Properties;
        if (grid == null
            || !string.Equals(grid.Id, gridId, StringComparison.Ordinal)
            || !string.Equals(grid.Parent, RuntimeIdentity.EmergencyHeadBandItemId, StringComparison.Ordinal)
            || props?.CellsH != RuntimeIdentity.EmergencyHeadBandSplitGridColumns
            || props.CellsV != RuntimeIdentity.EmergencyHeadBandSplitGridRows)
            throw new InvalidOperationException($"B&A&HB Utility HeadBand {name} grid identity/geometry collision.");

        var filters = props.Filters?.ToArray();
        var actual = filters?.Length == 1 ? filters[0].Filter : null;
        if (actual == null || !actual.SetEquals(accepted))
            throw new InvalidOperationException($"B&A&HB Utility HeadBand {name} grid filter collision.");
    }

    private static void ValidateSingleGrid(
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
