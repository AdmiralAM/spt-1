using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>Commits cross-mod filters after every mod has published its templates.</summary>
[Injectable(TypePriority = OnLoadOrder.PostLoad + 900)]
public sealed class SecureContainerCompatibility(
    TemplateTable templateTable,
    ISptLogger<SecureContainerCompatibility> logger) : IOnLoad
{
    private static readonly MongoId SimpleContainerParent = new("5795f317245977243854e041");
    private static readonly MongoId PackNStrapContainerParent = new("680fd1dae5044e670a092e16");
    private static readonly MongoId MoneyClass = new("543be5dd4bdc2deb348b4569");
    private static readonly MongoId Rouble = new(HeadBandUtilityPolicy.Rouble);
    private static readonly MongoId Dollar = new(HeadBandUtilityPolicy.Dollar);
    private static readonly MongoId Euro = new(HeadBandUtilityPolicy.Euro);
    private static readonly MongoId BearDogtag = new("59f32bb586f774757e1e8442");
    private static readonly MongoId UsecDogtag = new("59f32c3b86f77472a31742f0");
    private static readonly MongoId VanillaDogtagCase = new("5c093e3486f77430cb02e593");
    private static readonly HashSet<MongoId> GammaFamily =
    [
        new("5857a8bc2459772bad15db29"),
        new("68f117b8121d878a2303eee0"),
        new("68f8e04eae031982b00e7aaf")
    ];

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<MongoId> simpleContainers = templateTable.Items
            .Where(pair => IsDescendantOf(pair.Value, SimpleContainerParent)
                || IsDescendantOf(pair.Value, PackNStrapContainerParent))
            .Select(pair => pair.Key)
            .ToHashSet();
        HashSet<MongoId> wallets = FindAndNormalizeWallets(out int walletMoneyFixes);
        HashSet<MongoId> dogtagCases = FindDogtagCases();

        int gammaAdmissions = ExtendGamma(simpleContainers);
        int walletAdmissions = ExtendHeadBand(DedicatedWearableItems.HeadBandCurrencyGridName, wallets);
        int dogtagAdmissions = ExtendHeadBand(DedicatedWearableItems.HeadBandDogtagCaseGridName, dogtagCases);

        RequireAdmission(RuntimeIdentity.EmergencyHeadBandItemId, DedicatedWearableItems.HeadBandCurrencyGridName,
            new MongoId("6937eccbfd921faceb0dfecd"), "Loui Peeton wallet");
        RequireAdmission(RuntimeIdentity.EmergencyHeadBandItemId, DedicatedWearableItems.HeadBandDogtagCaseGridName,
            VanillaDogtagCase, "vanilla Dogtag Case");
        foreach (MongoId gamma in GammaFamily.Where(templateTable.Items.ContainsKey))
            RequireAdmission(gamma.ToString(), null, new MongoId("669c10fa06c00c483c58537a"), "Pack 'n' Strap Small Cash Box");

        logger.Success($"B&A&HB final compatibility committed: Gamma simple-container admissions={gammaAdmissions}, "
            + $"wallets={wallets.Count}/HeadBand additions={walletAdmissions}/money-filter fixes={walletMoneyFixes}, "
            + $"dogtag cases={dogtagCases.Count}/HeadBand additions={dogtagAdmissions}; Loui Peeton RUB and Pack 'n' Strap Gamma contracts verified.");
        return Task.CompletedTask;
    }

    private bool IsDescendantOf(TemplateItem item, MongoId ancestor)
    {
        MongoId? parent = item.Parent;
        var seen = new HashSet<MongoId>();
        while (parent != null && seen.Add(parent.Value))
        {
            if (parent.Value == ancestor) return true;
            if (!templateTable.Items.TryGetValue(parent.Value, out TemplateItem? parentItem)) return false;
            parent = parentItem.Parent;
        }
        return false;
    }

    private HashSet<MongoId> FindAndNormalizeWallets(out int changes)
    {
        changes = 0;
        var money = new HashSet<MongoId> { MoneyClass, Rouble, Dollar, Euro };
        var wallets = new HashSet<MongoId>();
        foreach (var pair in templateTable.Items)
        {
            var filters = pair.Value.Properties?.Grids?.SelectMany(grid => grid.Properties?.Filters ?? []).ToArray();
            if (filters == null || !filters.Any(filter => filter.Filter?.Overlaps(money) == true)) continue;
            wallets.Add(pair.Key);
            foreach (var filter in filters)
            {
                filter.Filter ??= [];
                filter.ExcludedFilter ??= [];
                foreach (MongoId id in new[] { Rouble, Dollar, Euro })
                    if (filter.Filter.Add(id)) changes++;
                foreach (MongoId id in money)
                    if (filter.ExcludedFilter.Remove(id)) changes++;
            }
        }
        return wallets;
    }

    private HashSet<MongoId> FindDogtagCases()
    {
        var result = new HashSet<MongoId> { VanillaDogtagCase, new(RuntimeIdentity.DogtagCaseItemId) };
        foreach (var pair in templateTable.Items)
        {
            bool acceptsDogtags = pair.Value.Properties?.Grids?
                .SelectMany(grid => grid.Properties?.Filters ?? [])
                .Any(filter => filter.Filter?.Contains(BearDogtag) == true || filter.Filter?.Contains(UsecDogtag) == true) == true;
            if (acceptsDogtags && (IsDescendantOf(pair.Value, SimpleContainerParent)
                || IsDescendantOf(pair.Value, PackNStrapContainerParent))) result.Add(pair.Key);
        }
        return result;
    }

    private int ExtendGamma(HashSet<MongoId> compatible)
    {
        int changed = 0;
        foreach (MongoId gammaId in GammaFamily)
        {
            if (!templateTable.Items.TryGetValue(gammaId, out TemplateItem? gamma)) continue;
            foreach (var filter in gamma.Properties?.Grids?.SelectMany(grid => grid.Properties?.Filters ?? []) ?? [])
            {
                filter.Filter ??= [];
                foreach (MongoId id in compatible)
                    if (id != gammaId && filter.Filter.Add(id)) changed++;
            }
        }
        return changed;
    }

    private int ExtendHeadBand(string gridName, HashSet<MongoId> compatible)
    {
        if (!templateTable.Items.TryGetValue(new MongoId(RuntimeIdentity.EmergencyHeadBandItemId), out TemplateItem? headBand)) return 0;
        var target = headBand.Properties?.Grids?.SingleOrDefault(grid => grid.Name == gridName)?
            .Properties?.Filters?.SingleOrDefault()?.Filter;
        if (target == null) return 0;
        int changed = 0;
        foreach (MongoId id in compatible) if (target.Add(id)) changed++;
        return changed;
    }

    private void RequireAdmission(string hostId, string? gridName, MongoId expected, string label)
    {
        if (!templateTable.Items.TryGetValue(new MongoId(hostId), out TemplateItem? host))
            throw new InvalidOperationException($"B&A&HB final compatibility host missing for {label}: {hostId}");
        IEnumerable<Grid> grids = host.Properties?.Grids ?? [];
        if (gridName != null) grids = grids.Where(grid => grid.Name == gridName);
        bool admitted = grids.SelectMany(grid => grid.Properties?.Filters ?? [])
            .Any(filter => filter.Filter?.Contains(expected) == true && filter.ExcludedFilter?.Contains(expected) != true);
        if (!admitted)
            throw new InvalidOperationException($"B&A&HB final compatibility failed: {label} ({expected}) is not admitted by {hostId}/{gridName ?? "all grids"}.");
    }
}
