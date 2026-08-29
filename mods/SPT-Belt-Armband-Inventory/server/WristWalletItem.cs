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
    private const string GridName = "main";
    private const string GridPrototype = "55d329c24bdc2d892f8b4567";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!templateTable.Items.ContainsKey(RuntimeCandidateBeltItem.SourceArmbandTpl))
            throw new InvalidOperationException("B&A&HB Wrist Wallet source armband missing.");
        if (!templateTable.Items.ContainsKey(RuntimeCandidateBeltItem.CustomBeltParentTpl))
            throw new InvalidOperationException("B&A&HB Wrist Wallet searchable parent was not initialized.");

        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == RuntimeCandidateBeltItem.SourceArmbandTpl)
            ?? throw new InvalidOperationException("B&A&HB Wrist Wallet source handbook entry missing.");

        var id = new MongoId(TemplateId);
        if (templateTable.Items.TryGetValue(id, out var existing))
        {
            ValidateExisting(existing);
            logger.Success("B&A&HB Wrist Wallet retained existing validated 1x1 currency-only item.");
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

        logger.Success("B&A&HB Wrist Wallet created: host=ArmBand, grid=1x1, filter=RUB/USD/EUR.");
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
}
