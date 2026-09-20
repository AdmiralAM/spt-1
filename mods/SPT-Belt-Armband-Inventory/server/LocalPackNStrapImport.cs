#if PACKNSTRAP_LOCAL_IMPORT
using System.Reflection;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Private-build bridge. The repository contains no Pack 'n' Strap assets or
/// item database. The local import tool supplies those resources at build and
/// deployment time from the user's own Pack 'n' Strap installation.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public sealed class LocalPackNStrapImport(
    WTTServerCommonLib.WTTServerCommonLib wttCommon,
    TemplateTable templateTable,
    ISptLogger<LocalPackNStrapImport> logger) : IOnLoad
{
    public async Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Assembly assembly = typeof(ModMetadata).Assembly;

        await wttCommon.CustomItemParentService.CreateCustomParents(assembly);
        await wttCommon.CustomItemServiceExtended.CreateCustomItems(assembly);
        wttCommon.CustomRigLayoutService.CreateRigLayouts(assembly);
        await wttCommon.CustomLocaleService.CreateCustomLocales(assembly);

        PreventImportedBeltsFromBeingNested(templateTable);
        LocalPackNStrapImportState.Enabled = true;
        logger.Success("B&A&HB private Pack 'n' Strap content import loaded; imported belts are routed to dedicated slot15 and no Pack 'n' Strap runtime owner is required.");
    }

    private static void PreventImportedBeltsFromBeingNested(TemplateTable templateTable)
    {
        MongoId parent = new(LocalPackNStrapImportState.BeltParentId);
        MongoId[] beltIds = templateTable.Items.Values
            .Where(item => item.Parent == parent)
            .Select(item => item.Id)
            .ToArray();

        foreach (MongoId beltId in beltIds)
        {
            if (!templateTable.Items.TryGetValue(beltId, out TemplateItem? belt)) continue;
            foreach (var grid in belt.Properties?.Grids ?? [])
            foreach (var filter in grid.Properties?.Filters ?? [])
            {
                filter.ExcludedFilter ??= [];
                foreach (MongoId excluded in beltIds)
                    if (!filter.ExcludedFilter.Contains(excluded)) filter.ExcludedFilter.Add(excluded);
            }
        }
    }
}
#endif
