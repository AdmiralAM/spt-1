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
    private static readonly MongoId SearchableItemBaseTpl = new("566162e44bdc2d3f298b4573");
    public static readonly MongoId CustomTemplateParentTpl = new(RuntimeIdentity.SearchableTemplateParentId);
    public static readonly MongoId CustomBeltParentTpl = new(RuntimeIdentity.BeltItemParentId);
    public const string RuntimeCandidateTpl = RuntimeIdentity.CandidateItemId;
    public const string RuntimeCandidateGridId = RuntimeIdentity.CandidateGridId;

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!templateTable.Items.ContainsKey(SourceArmbandTpl)) throw new InvalidOperationException("B&A&HB RC source armband missing.");
        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == SourceArmbandTpl) ?? throw new InvalidOperationException("B&A&HB RC source handbook entry missing.");

        EnsureCustomParents();
        EnsureArmBandAcceptsCustomBeltParent();
        if (templateTable.Items.TryGetValue(new MongoId(RuntimeCandidateTpl), out var existingCandidate))
        {
            ValidateExistingCandidate(existingCandidate);
            logger.Success($"B&A&HB RC retained existing validated item: tpl={RuntimeCandidateTpl}, parent={CustomBeltParentTpl}, grid={RuntimeIdentity.CandidateGridColumns}x{RuntimeIdentity.CandidateGridRows}, filter=MAGAZINE.");
            return Task.CompletedTask;
        }

        var details = new NewItemFromCloneDetails
        {
            NewItemName = "B&A&HB Runtime Candidate Magazine Belt",
            ItemTplToClone = SourceArmbandTpl,
            ParentId = CustomBeltParentTpl,
            NewId = RuntimeCandidateTpl,
            FleaPriceRoubles = 1000,
            HandbookPriceRoubles = 1000,
            HandbookParentId = handbookItem.ParentId,
            Locales = new Dictionary<string, LocaleDetails> { ["en"] = new LocaleDetails { Name = "B&A&HB Runtime Candidate Magazine Belt", ShortName = "B&A&HB Belt RC", Description = "Minimal 1x2 magazine belt runtime candidate." } },
            OverrideProperties = new TemplateItemProperties
            {
                BackgroundColor = "blue", ExaminedByDefault = true,
                Grids = [new Grid { Name = "main", Id = RuntimeCandidateGridId, Parent = RuntimeCandidateTpl, Prototype = "55d329c24bdc2d892f8b4567", Properties = new GridProperties { CellsH = RuntimeIdentity.CandidateGridColumns, CellsV = RuntimeIdentity.CandidateGridRows, MinCount = 0, MaxCount = 0, MaxWeight = 0, IsSortingTable = false, Filters = [new GridFilter { Filter = [BaseClasses.MAGAZINE], ExcludedFilter = [] }] } }]
            }
        };
        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success) throw new InvalidOperationException($"B&A&HB RC item creation failed: {string.Join("; ", result.Errors)}");
        logger.Success($"B&A&HB RC created: tpl={RuntimeCandidateTpl}, parent={CustomBeltParentTpl}, grid={RuntimeIdentity.CandidateGridColumns}x{RuntimeIdentity.CandidateGridRows}, filter=MAGAZINE.");
        return Task.CompletedTask;
    }

    private static void ValidateExistingCandidate(TemplateItem candidate)
    {
        if (!Equals(candidate.Parent, CustomBeltParentTpl))
            throw new InvalidOperationException("B&A&HB RC item ID collision: existing item uses a different parent.");

        var grids = candidate.Properties?.Grids;
        if (grids == null || grids.Count() != 1)
            throw new InvalidOperationException("B&A&HB RC item ID collision: existing item does not declare exactly one grid.");

        var grid = grids.Single();
        var properties = grid.Properties;
        if (!string.Equals(grid.Id.ToString(), RuntimeCandidateGridId, StringComparison.Ordinal)
            || properties == null
            || properties.CellsH != RuntimeIdentity.CandidateGridColumns
            || properties.CellsV != RuntimeIdentity.CandidateGridRows)
            throw new InvalidOperationException("B&A&HB RC item ID collision: existing grid identity or geometry differs from the shared runtime contract.");

        var filters = properties.Filters;
        if (filters == null || !filters.Any(x => x.Filter?.Contains(BaseClasses.MAGAZINE) == true))
            throw new InvalidOperationException("B&A&HB RC item ID collision: existing grid does not retain the MAGAZINE filter.");
    }

    private void EnsureCustomParents()
    {
        EnsureCustomParent(CustomTemplateParentTpl, "BAndHBSearchableContainerTemplate", SearchableItemBaseTpl);
        EnsureCustomParent(CustomBeltParentTpl, "BAndHBCustomBeltItem", CustomTemplateParentTpl);
    }

    private void EnsureCustomParent(MongoId id, string name, MongoId parent)
    {
        if (!templateTable.Items.TryGetValue(id, out var existing))
        {
            templateTable.Items[id] = new TemplateItem
            {
                Id = id,
                Name = name,
                Parent = parent,
                Type = "Node",
                Properties = new TemplateItemProperties()
            };
            return;
        }

        if (!Equals(existing.Id, id)
            || !Equals(existing.Parent, parent)
            || !string.Equals(existing.Name, name, StringComparison.Ordinal)
            || !string.Equals(existing.Type, "Node", StringComparison.Ordinal))
            throw new InvalidOperationException($"B&A&HB custom parent ID collision: {id} does not match the registered taxonomy contract.");
    }

    private void EnsureArmBandAcceptsCustomBeltParent()
    {
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB RC default inventory template missing.");

        var armBand = inventory.Properties?.Slots?.FirstOrDefault(x => string.Equals(x.Name, "ArmBand", StringComparison.Ordinal));
        var filter = armBand?.Properties?.Filters?.FirstOrDefault()?.Filter;
        if (filter == null) throw new InvalidOperationException("B&A&HB RC ArmBand slot filter missing.");
        if (!filter.Contains(CustomBeltParentTpl)) filter.Add(CustomBeltParentTpl);
    }
}
