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

[Injectable(TypePriority = OnLoadOrder.Preload + 1)]
public sealed class RuntimeCandidateBeltItem(TemplateTable templateTable, CustomItemService customItemService, ISptLogger<RuntimeCandidateBeltItem> logger) : IOnLoad
{
    public static readonly MongoId SourceArmbandTpl = new("5b3f3af486f774679e752c1f");
    public static readonly MongoId DefaultInventoryTpl = new("55d7217a4bdc2d86028b456d");
    public static readonly MongoId CustomTemplateParentTpl = new(RuntimeIdentity.SearchableTemplateParentId);
    public static readonly MongoId CustomBeltParentTpl = new(RuntimeIdentity.BeltItemParentId);
    private static readonly MongoId MagazineArmbandTpl = new(RuntimeIdentity.CandidateItemId);
    private static readonly MongoId WristWalletTpl = new(RuntimeIdentity.WristWalletItemId);
    public const string RuntimeCandidateTpl = RuntimeIdentity.CandidateItemId;
    public const string RuntimeCandidateGridId = RuntimeIdentity.CandidateGridId;
    private const string RuntimeCandidateGridName = "main";
    private const string RuntimeCandidateGridPrototype = "55d329c24bdc2d892f8b4567";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!templateTable.Items.ContainsKey(SourceArmbandTpl)) throw new InvalidOperationException("B&A&HB Magazine Armband source armband missing.");
        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == SourceArmbandTpl) ?? throw new InvalidOperationException("B&A&HB Magazine Armband source handbook entry missing.");

        ValidateTaxonomyParents();
        if (templateTable.Items.TryGetValue(MagazineArmbandTpl, out var existingCandidate))
        {
            ValidateExistingCandidate(existingCandidate);
            EnsureArmBandAcceptsExactProducts();
            logger.Success($"B&A&HB Magazine Armband retained existing validated item: tpl={RuntimeCandidateTpl}, parent={CustomBeltParentTpl}, grid={RuntimeIdentity.CandidateGridColumns}x{RuntimeIdentity.CandidateGridRows}, filter=MAGAZINE.");
            return Task.CompletedTask;
        }

        var details = new NewItemFromCloneDetails
        {
            NewItemName = "B&A&HB Magazine Armband",
            ItemTplToClone = SourceArmbandTpl,
            ParentId = CustomBeltParentTpl,
            NewId = RuntimeCandidateTpl,
            FleaPriceRoubles = RuntimeCandidateOfferContract.PriceRoubles,
            HandbookPriceRoubles = RuntimeCandidateOfferContract.PriceRoubles,
            HandbookParentId = handbookItem.ParentId,
            Locales = new Dictionary<string, LocaleDetails>
            {
                ["en"] = new LocaleDetails
                {
                    Name = "B&A&HB Magazine Armband",
                    ShortName = "Mag Armband",
                    Description = "Compact 1x2 magazine carrier worn in the ArmBand equipment location."
                },
                ["ru"] = new LocaleDetails
                {
                    Name = "Повязка под магазины B&A&HB",
                    ShortName = "Маг. повязка",
                    Description = "Компактная повязка 1x2 для магазинов, устанавливаемая в слот ArmBand."
                }
            },
            OverrideProperties = new TemplateItemProperties
            {
                BackgroundColor = "blue", ExaminedByDefault = true,
                Grids = [new Grid { Name = RuntimeCandidateGridName, Id = RuntimeCandidateGridId, Parent = RuntimeCandidateTpl, Prototype = RuntimeCandidateGridPrototype, Properties = new GridProperties { CellsH = RuntimeIdentity.CandidateGridColumns, CellsV = RuntimeIdentity.CandidateGridRows, MinCount = 0, MaxCount = 0, MaxWeight = 0, IsSortingTable = false, Filters = [new GridFilter { Filter = [BaseClasses.MAGAZINE], ExcludedFilter = [] }] } }]
            }
        };
        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success) throw new InvalidOperationException($"B&A&HB Magazine Armband creation failed: {string.Join("; ", result.Errors)}");

        // Only expose exact ArmBand products after the Magazine Armband item exists.
        // The broader BeltItemParentId is shared by dedicated Magazine Belt, so adding
        // that parent to ArmBand would incorrectly make the slot15 product cross-equip.
        EnsureArmBandAcceptsExactProducts();
        logger.Success($"B&A&HB Magazine Armband created: tpl={RuntimeCandidateTpl}, parent={CustomBeltParentTpl}, grid={RuntimeIdentity.CandidateGridColumns}x{RuntimeIdentity.CandidateGridRows}, filter=MAGAZINE.");
        return Task.CompletedTask;
    }

    private static void ValidateExistingCandidate(TemplateItem candidate)
    {
        if (!Equals(candidate.Parent, CustomBeltParentTpl))
            throw new InvalidOperationException("B&A&HB Magazine Armband ID collision: existing item uses a different parent.");

        var grids = candidate.Properties?.Grids?.ToArray();
        if (grids == null || grids.Length != 1)
            throw new InvalidOperationException("B&A&HB Magazine Armband ID collision: existing item does not declare exactly one grid.");

        var grid = grids[0];
        var properties = grid.Properties;
        if (!string.Equals(grid.Name, RuntimeCandidateGridName, StringComparison.Ordinal)
            || !string.Equals(grid.Id.ToString(), RuntimeCandidateGridId, StringComparison.Ordinal)
            || !string.Equals(grid.Parent?.ToString(), RuntimeCandidateTpl, StringComparison.Ordinal)
            || !string.Equals(grid.Prototype?.ToString(), RuntimeCandidateGridPrototype, StringComparison.Ordinal)
            || properties == null
            || properties.CellsH != RuntimeIdentity.CandidateGridColumns
            || properties.CellsV != RuntimeIdentity.CandidateGridRows
            || properties.MinCount != 0
            || properties.MaxCount != 0
            || properties.MaxWeight != 0
            || properties.IsSortingTable == true)
            throw new InvalidOperationException("B&A&HB Magazine Armband ID collision: existing grid identity, geometry, or limits differ from the shared product contract.");

        var filters = properties.Filters?.ToArray();
        if (filters == null || filters.Length != 1)
            throw new InvalidOperationException("B&A&HB Magazine Armband ID collision: existing grid does not declare exactly one filter group.");

        var filter = filters[0];
        var included = filter.Filter?.ToArray();
        var excluded = filter.ExcludedFilter?.ToArray();
        if (included == null
            || included.Length != 1
            || !included.Contains(BaseClasses.MAGAZINE)
            || (excluded != null && excluded.Length != 0))
            throw new InvalidOperationException("B&A&HB Magazine Armband ID collision: existing grid does not retain the exact MAGAZINE-only filter.");
    }

    private void ValidateTaxonomyParents()
    {
        ValidateTaxonomyParent(CustomTemplateParentTpl, "BAndHBSearchableContainerTemplate", new MongoId("566162e44bdc2d3f298b4573"));
        ValidateTaxonomyParent(CustomBeltParentTpl, "BAndHBCustomBeltItem", CustomTemplateParentTpl);
    }

    private void ValidateTaxonomyParent(MongoId id, string name, MongoId parent)
    {
        if (!templateTable.Items.TryGetValue(id, out var existing))
            throw new InvalidOperationException($"B&A&HB taxonomy parent {id} was not registered by the Preload taxonomy owner.");

        if (!Equals(existing.Id, id)
            || !Equals(existing.Parent, parent)
            || !string.Equals(existing.Name, name, StringComparison.Ordinal)
            || !string.Equals(existing.Type, "Node", StringComparison.Ordinal))
            throw new InvalidOperationException($"B&A&HB taxonomy parent collision: {id} does not match the registered taxonomy contract.");
    }

    private void EnsureArmBandAcceptsExactProducts()
    {
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var inventory))
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

        var filter = filterGroups[0].Filter;
        if (!filter.Contains(MagazineArmbandTpl)) filter.Add(MagazineArmbandTpl);
        if (!filter.Contains(WristWalletTpl)) filter.Add(WristWalletTpl);
        if (filter.Contains(CustomBeltParentTpl))
            throw new InvalidOperationException("B&A&HB ArmBand filter already contains the broad Belt parent; refusing host overlap that would admit dedicated Magazine Belt.");
    }
}
