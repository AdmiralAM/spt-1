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

[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public sealed class WristWalletItem(TemplateTable templateTable, CustomItemService customItemService, ISptLogger<WristWalletItem> logger) : IOnLoad
{
    public const string TemplateId = RuntimeIdentity.WristWalletItemId;
    public const string GridId = RuntimeIdentity.WristWalletGridId;
    private static readonly MongoId WristWalletTpl = new(TemplateId);
    private static readonly MongoId MagazineArmbandTpl = new(RuntimeIdentity.CandidateItemId);
    private static readonly MongoId BroadBeltParentTpl = new(RuntimeIdentity.BeltItemParentId);
    private const string GridName = "main";
    private const string GridPrototype = "55d329c24bdc2d892f8b4567";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!templateTable.Items.ContainsKey(RuntimeCandidateBeltItem.SourceArmbandTpl))
            throw new InvalidOperationException("B&A&HB Wrist Wallet source armband missing.");
        if (!templateTable.Items.ContainsKey(RuntimeCandidateBeltItem.CustomBeltParentTpl))
            throw new InvalidOperationException("B&A&HB Wrist Wallet searchable parent was not initialized.");
        if (!templateTable.Items.ContainsKey(MagazineArmbandTpl))
            throw new InvalidOperationException("B&A&HB Magazine Armband was not initialized before ArmBand host exposure.");

        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == RuntimeCandidateBeltItem.SourceArmbandTpl)
            ?? throw new InvalidOperationException("B&A&HB Wrist Wallet source handbook entry missing.");
        HashSet<MongoId> armBandFilter = PrepareArmBandExactProductFilter();

        if (templateTable.Items.TryGetValue(WristWalletTpl, out var existing))
        {
            ValidateExisting(existing);
            CommitArmBandExactProducts(armBandFilter);
            logger.Success("B&A&HB Wrist Wallet retained existing validated 1x1 currency-only item; exact ArmBand products exposed atomically.");
            return Task.CompletedTask;
        }

        var details = new NewItemFromCloneDetails
        {
            NewItemName = "B&A&HB Wrist Wallet",
            ItemTplToClone = RuntimeCandidateBeltItem.SourceArmbandTpl,
            ParentId = RuntimeCandidateBeltItem.CustomBeltParentTpl,
            NewId = TemplateId,
            FleaPriceRoubles = 12500,
            HandbookPriceRoubles = 12500,
            HandbookParentId = handbookItem.ParentId,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = "B&A&HB Wrist Wallet",
                    ShortName = "Wrist Wallet",
                    Description = "Compact 1x1 wrist wallet for RUB, USD and EUR."
                },
                ["ru"] = new LocaleDetails
                {
                    Name = "Наручный кошелёк B&A&HB",
                    ShortName = "Наруч. кошелёк",
                    Description = "Компактный наручный кошелёк 1x1 для рублей, долларов и евро."
                }
            },
            OverrideProperties = new TemplateItemProperties
            {
                BackgroundColor = "blue",
                ExaminedByDefault = true,
                Grids =
                [
                    new Grid
                    {
                        Name = GridName,
                        Id = GridId,
                        Parent = TemplateId,
                        Prototype = GridPrototype,
                        Properties = new GridProperties
                        {
                            CellsH = RuntimeIdentity.WristWalletGridColumns,
                            CellsV = RuntimeIdentity.WristWalletGridRows,
                            MinCount = 0,
                            MaxCount = 0,
                            MaxWeight = 0,
                            IsSortingTable = false,
                            Filters =
                            [
                                new GridFilter
                                {
                                    Filter = [Money.ROUBLES, Money.DOLLARS, Money.EUROS],
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
            throw new InvalidOperationException($"B&A&HB Wrist Wallet creation failed: {string.Join("; ", result.Errors)}");

        // Host boundary was validated before item creation. Both exact ArmBand
        // products now exist, so the final commit has no discovery/collision step.
        CommitArmBandExactProducts(armBandFilter);
        logger.Success("B&A&HB Wrist Wallet created: host=ArmBand, grid=1x1, filter=RUB/USD/EUR; exact ArmBand products exposed atomically.");
        return Task.CompletedTask;
    }

    private static void ValidateExisting(TemplateItem candidate)
    {
        if (!Equals(candidate.Parent, RuntimeCandidateBeltItem.CustomBeltParentTpl))
            throw new InvalidOperationException("B&A&HB Wrist Wallet ID collision: existing item uses a different parent.");

        var grids = candidate.Properties?.Grids?.ToArray();
        if (grids == null || grids.Length != 1)
            throw new InvalidOperationException("B&A&HB Wrist Wallet ID collision: expected exactly one grid.");

        var grid = grids[0];
        var properties = grid.Properties;
        if (!string.Equals(grid.Name, GridName, StringComparison.Ordinal)
            || !string.Equals(grid.Id.ToString(), GridId, StringComparison.Ordinal)
            || !string.Equals(grid.Parent?.ToString(), TemplateId, StringComparison.Ordinal)
            || !string.Equals(grid.Prototype?.ToString(), GridPrototype, StringComparison.Ordinal)
            || properties == null
            || properties.CellsH != RuntimeIdentity.WristWalletGridColumns
            || properties.CellsV != RuntimeIdentity.WristWalletGridRows
            || properties.MinCount != 0
            || properties.MaxCount != 0
            || properties.MaxWeight != 0
            || properties.IsSortingTable == true)
            throw new InvalidOperationException("B&A&HB Wrist Wallet ID collision: grid identity, geometry, or limits differ from the exact 1x1 contract.");

        var filters = properties.Filters?.ToArray();
        if (filters == null || filters.Length != 1)
            throw new InvalidOperationException("B&A&HB Wrist Wallet ID collision: expected one currency filter group.");

        var included = filters[0].Filter?.ToArray();
        var excluded = filters[0].ExcludedFilter?.ToArray();
        if (included == null
            || included.Length != 3
            || !included.Contains(Money.ROUBLES)
            || !included.Contains(Money.DOLLARS)
            || !included.Contains(Money.EUROS)
            || (excluded != null && excluded.Length != 0))
            throw new InvalidOperationException("B&A&HB Wrist Wallet ID collision: filter differs from exact RUB/USD/EUR-only contract.");
    }

    private HashSet<MongoId> PrepareArmBandExactProductFilter()
    {
        if (!templateTable.Items.TryGetValue(RuntimeCandidateBeltItem.DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB default inventory template missing.");

        var armBands = inventory.Properties?.Slots?
            .Where(x => string.Equals(x.Name, "ArmBand", StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (armBands == null || armBands.Length != 1)
            throw new InvalidOperationException("B&A&HB ArmBand slot boundary is missing or ambiguous; refusing to mutate an unproven inventory slot.");

        var filterGroups = armBands[0].Properties?.Filters?.ToArray();
        if (filterGroups == null || filterGroups.Length != 1 || filterGroups[0].Filter == null)
            throw new InvalidOperationException("B&A&HB ArmBand slot filter boundary is missing or ambiguous; exactly one filter group is required.");

        HashSet<MongoId> filter = filterGroups[0].Filter;
        if (filter.Contains(BroadBeltParentTpl))
            throw new InvalidOperationException("B&A&HB ArmBand filter contains the broad Belt parent; refusing host overlap that would admit dedicated Magazine Belt.");
        return filter;
    }

    private void CommitArmBandExactProducts(HashSet<MongoId> filter)
    {
        if (!templateTable.Items.ContainsKey(MagazineArmbandTpl) || !templateTable.Items.ContainsKey(WristWalletTpl))
            throw new InvalidOperationException("B&A&HB ArmBand host exposure requires both exact product templates to exist.");

        if (!filter.Contains(MagazineArmbandTpl)) filter.Add(MagazineArmbandTpl);
        if (!filter.Contains(WristWalletTpl)) filter.Add(WristWalletTpl);
    }
}
