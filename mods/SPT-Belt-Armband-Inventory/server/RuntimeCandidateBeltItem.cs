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
    public const string RuntimeCandidateTpl = "68ac00000000000000000001";
    public const string RuntimeCandidateGridId = "68ac00000000000000000002";

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        if (!templateTable.Items.TryGetValue(SourceArmbandTpl, out var sourceItem)) throw new InvalidOperationException("B&A&HB RC source armband missing.");
        var handbookItem = templateTable.Handbook.Items.FirstOrDefault(x => x.Id == SourceArmbandTpl) ?? throw new InvalidOperationException("B&A&HB RC source handbook entry missing.");
        var details = new NewItemFromCloneDetails
        {
            NewItemName = "B&A&HB Runtime Candidate Magazine Belt",
            ItemTplToClone = SourceArmbandTpl,
            ParentId = sourceItem.Parent,
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
        logger.Success($"B&A&HB RC created: tpl={RuntimeCandidateTpl}, grid=1x2, filter=MAGAZINE.");
        return Task.CompletedTask;
    }
}
