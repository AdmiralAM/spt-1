using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>Adds modded simple containers to every Gamma-family clone by exact template id.</summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 5)]
public sealed class SecureContainerCompatibility(
    TemplateTable templateTable,
    ISptLogger<SecureContainerCompatibility> logger) : IOnLoad
{
    private static readonly MongoId SimpleContainerParent = new("5795f317245977243854e041");
    private static readonly HashSet<MongoId> GammaFamily =
    [
        new("5857a8bc2459772bad15db29"),
        new("68f117b8121d878a2303eee0"),
        new("68f8e04eae031982b00e7aaf")
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var compatible = templateTable.Items
            .Where(x => x.Value.Parent == SimpleContainerParent)
            .Select(x => x.Key)
            .ToHashSet();

        int changed = 0;
        int walletsAdded = ExtendHeadBandWallets();
        foreach (var gammaId in GammaFamily)
        {
            if (!templateTable.Items.TryGetValue(gammaId, out var gamma)) continue;
            foreach (var filter in gamma.Properties?.Grids?.SelectMany(x => x.Properties?.Filters ?? []) ?? [])
            {
                if (filter.Filter == null) continue;
                foreach (var id in compatible)
                    if (filter.Filter.Add(id)) changed++;
            }
        }
        logger.Success($"B&A&HB compatibility added {changed} exact simple-container admissions across installed Gamma-family templates and {walletsAdded} wallet admissions to HeadBand.");
        return Task.CompletedTask;
    }

    private int ExtendHeadBandWallets()
    {
        if (!templateTable.Items.TryGetValue(new MongoId(RuntimeIdentity.EmergencyHeadBandItemId), out var headBand)) return 0;
        var moneyIds = new HashSet<MongoId>
        {
            new("543be5dd4bdc2deb348b4569"),
            new(HeadBandUtilityPolicy.Rouble),
            new(HeadBandUtilityPolicy.Dollar),
            new(HeadBandUtilityPolicy.Euro)
        };
        var wallets = templateTable.Items.Where(x => x.Value.Properties?.Grids?
            .SelectMany(g => g.Properties?.Filters ?? [])
            .Any(f => f.Filter?.Overlaps(moneyIds) == true) == true).Select(x => x.Key).ToArray();
        var target = headBand.Properties?.Grids?.SingleOrDefault(x => x.Name == DedicatedWearableItems.HeadBandCurrencyGridName)?
            .Properties?.Filters?.SingleOrDefault()?.Filter;
        if (target == null) return 0;
        int changed = 0;
        foreach (var id in wallets) if (target.Add(id)) changed++;
        return changed;
    }
}
