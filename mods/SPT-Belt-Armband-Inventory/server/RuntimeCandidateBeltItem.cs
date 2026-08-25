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
    public static readonly MongoId CustomTemplateParentTpl = new("68ac00000000000000000004");
    public static readonly MongoId CustomBeltParentTpl = new("68ac00000000000000000005");
    public const string RuntimeCandidateTpl = "68ac00000000000000000001";
    public const string RuntimeCandidateGridId = "68ac00000000000000000002";

    // Discovery-gate artifact: custom taxonomy is intentionally disabled until
    // the SPT 4.1.3 client inventory contracts are proven load-safe.
    private const bool EnableCustomRuntimeTaxonomy = false;

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!templateTable.Items.TryGetValue(SourceArmbandTpl, out var sourceItem)) throw new InvalidOperationException("B&A&HB RC source armband missing.");
        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == SourceArmbandTpl) ?? throw new InvalidOperationException("B&A&HB RC source handbook entry missing.");

        MongoId parentId;
        if (EnableCustomRuntimeTaxonomy)
        {
            EnsureCustomParents();
            EnsureArmBandAcceptsCustomBeltParent();
            parentId = CustomBeltParentTpl;
        }
        else
        {
            parentId = sourceItem.Parent;
            logger.Warning("B&A&HB LOAD-SAFE FAIL-CLOSED: custom belt parent/template taxonomy is disabled for the discovery artifact; RC remains on the vanilla armband taxonomy so /client/items cannot be blocked by an unregistered custom C# type.");
        }

        var details = new NewItemFromCloneDetails
        {
            NewItemName = "B&A&HB Runtime Candidate Magazine Belt",
            ItemTplToClone = SourceArmbandTpl,
            ParentId = parentId,
            NewId = RuntimeCandidateTpl,
            FleaPriceRoubles = 1000,
            HandbookPriceRoubles = 1000,
            HandbookParentId = handbookItem.ParentId,
            Locales = new Dictionary<string, LocaleDetails> { ["en"] = new LocaleDetails { Name = "B&A&HB Runtime Candidate Magazine Belt", ShortName = "B&A&HB Belt RC", Description = "Minimal 1x2 magazine belt runtime candidate." } },
            OverrideProperties = new TemplateItemProperties
            {
                BackgroundColor = "blue", ExaminedByDefault = true,
                Grids = [new Grid { Name = "main", Id = RuntimeCandidateGridId, Parent = RuntimeCandidateTpl, Prototype = "55d329c24bdc2d892f8b4567", Properties = new GridProperties { CellsH = 1, CellsV = 2, MinCount = 0, MaxCount = 0, MaxWeight = 0, IsSortingTable = false, Filters = [new GridFilter { Filter = [BaseClasses.MAGAZINE], ExcludedFilter = [] }] } }]
            }
        };
        var result = customItemService.CreateItemFromClone(details);
        if (!result.Success) throw new InvalidOperationException($"B&A&HB RC item creation failed: {string.Join("; ", result.Errors)}");
        logger.Success($"B&A&HB RC created: tpl={RuntimeCandidateTpl}, parent={parentId}, grid=1x2, filter=MAGAZINE, customTaxonomy={EnableCustomRuntimeTaxonomy}.");
        return Task.CompletedTask;
    }

    private void EnsureCustomParents()
    {
        if (!templateTable.Items.ContainsKey(CustomTemplateParentTpl))
        {
            templateTable.Items[CustomTemplateParentTpl] = new TemplateItem
            {
                Id = CustomTemplateParentTpl,
                Name = "BAndHBSearchableContainerTemplate",
                Parent = new MongoId("566162e44bdc2d3f298b4573"),
                Type = "Node",
                Properties = new TemplateItemProperties()
            };
        }

        if (!templateTable.Items.ContainsKey(CustomBeltParentTpl))
        {
            templateTable.Items[CustomBeltParentTpl] = new TemplateItem
            {
                Id = CustomBeltParentTpl,
                Name = "BAndHBCustomBeltItem",
                Parent = CustomTemplateParentTpl,
                Type = "Node",
                Properties = new TemplateItemProperties()
            };
        }
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
