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
    private static readonly MongoId PackNStrapContainerParent = new(SecureContainerCompatibilityPolicy.PackNStrapContainerParent);
    private static readonly MongoId PackNStrapPlateContainer = new(SecureContainerCompatibilityPolicy.PackNStrapPlateContainer);
    private static readonly MongoId MoneyClass = new("543be5dd4bdc2deb348b4569");
    private static readonly MongoId Rouble = new(HeadBandUtilityPolicy.Rouble);
    private static readonly MongoId Dollar = new(HeadBandUtilityPolicy.Dollar);
    private static readonly MongoId Euro = new(HeadBandUtilityPolicy.Euro);
    private static readonly MongoId BearDogtag = new("59f32bb586f774757e1e8442");
    private static readonly MongoId UsecDogtag = new("59f32c3b86f77472a31742f0");
    private static readonly MongoId VanillaDogtagCase = new("5c093e3486f77430cb02e593");
    private static readonly HashSet<MongoId> GammaFamily =
        SecureContainerCompatibilityPolicy.GammaTemplateIds.Select(id => new MongoId(id)).ToHashSet();
    private static readonly HashSet<MongoId> TgcBelts =
        TgcCompatibilityPolicy.BeltTemplateIds.Select(id => new MongoId(id)).ToHashSet();
    private static readonly HashSet<MongoId> TgcSecurePouches =
        TgcCompatibilityPolicy.SecureContainerPouchAllowlist.Select(id => new MongoId(id)).ToHashSet();
    private static readonly MongoId TgcToolBox = new(TgcCompatibilityPolicy.ToolBoxTemplateId);

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<MongoId> packNStrapContainers = templateTable.Items
            .Where(pair => IsDescendantOf(pair.Value, PackNStrapContainerParent)
                || pair.Key == PackNStrapPlateContainer)
            .Select(pair => pair.Key)
            .ToHashSet();
        HashSet<MongoId> wallets = FindAndNormalizeWallets(out int walletMoneyFixes);
        HashSet<MongoId> dogtagCases = FindDogtagCases();

        TgcCommitResult tgc = ExternalCompatibilityApi.IsTgc300Claimed ? CommitTgcOwnership() : default;
        HashSet<MongoId> gammaContainers = packNStrapContainers
            .Concat(ExternalCompatibilityApi.IsTgc300Claimed ? TgcSecurePouches.Where(templateTable.Items.ContainsKey) : [])
            .ToHashSet();
        int gammaAdmissions = ExtendGamma(gammaContainers);
        int walletAdmissions = ExtendHeadBand(DedicatedWearableItems.HeadBandCurrencyGridName, wallets);
        int dogtagAdmissions = ExtendHeadBand(DedicatedWearableItems.HeadBandDogtagCaseGridName, dogtagCases);

        RequireAdmission(RuntimeIdentity.EmergencyHeadBandItemId, DedicatedWearableItems.HeadBandCurrencyGridName,
            new MongoId("6937eccbfd921faceb0dfecd"), "Loui Peeton wallet");
        RequireAdmission(RuntimeIdentity.EmergencyHeadBandItemId, DedicatedWearableItems.HeadBandDogtagCaseGridName,
            VanillaDogtagCase, "vanilla Dogtag Case");
        MongoId packNStrapCashBox = new("669c10fa06c00c483c58537a");
        if (packNStrapContainers.Contains(packNStrapCashBox))
            foreach (MongoId gamma in GammaFamily.Where(templateTable.Items.ContainsKey))
                RequireAdmission(gamma.ToString(), null, packNStrapCashBox, "Pack 'n' Strap Small Cash Box");

        logger.Debug($"B&A&HB final compatibility committed: Gamma Pack 'n' Strap admissions={gammaAdmissions}, "
            + $"containers verified={packNStrapContainers.Count}, Gamma hosts verified={GammaFamily.Count(templateTable.Items.ContainsKey)}, "
            + $"wallets={wallets.Count}/HeadBand additions={walletAdmissions}/money-filter fixes={walletMoneyFixes}, "
            + $"dogtag cases={dogtagCases.Count}/HeadBand additions={dogtagAdmissions}; "
            + $"TGC {TgcCompatibilityPolicy.Release} belts={tgc.BeltsPublished}/ArmBand removals={tgc.ArmBandRemovals}/Belt additions={tgc.BeltAdmissions}, "
            + $"secure removals={tgc.SecureRemovals}/explicit pouch allowlist={tgc.SecurePouchesPublished}; "
            + "Loui Peeton RUB, Pack 'n' Strap and exact TGC Gamma contracts verified.");
        return Task.CompletedTask;
    }

    private TgcCommitResult CommitTgcOwnership()
    {
        HashSet<MongoId> publishedBelts = TgcBelts.Where(templateTable.Items.ContainsKey).ToHashSet();
        HashSet<MongoId> publishedPouches = TgcSecurePouches.Where(templateTable.Items.ContainsKey).ToHashSet();
        bool tgcPresent = publishedBelts.Count != 0 || publishedPouches.Count != 0 || templateTable.Items.ContainsKey(TgcToolBox);
        if (!tgcPresent) return default;

        if (publishedBelts.Count != TgcBelts.Count)
            throw new InvalidOperationException($"B&A&HB TGC {TgcCompatibilityPolicy.Release} compatibility refused: partial Belt template set ({publishedBelts.Count}/{TgcBelts.Count}).");
        if (publishedPouches.Count != TgcSecurePouches.Count || !templateTable.Items.ContainsKey(TgcToolBox))
            throw new InvalidOperationException($"B&A&HB TGC {TgcCompatibilityPolicy.Release} compatibility refused: container template set is incomplete.");

        if (!templateTable.Items.TryGetValue(new MongoId(RuntimeCandidateBeltItem.DefaultInventoryTpl), out TemplateItem? inventory))
            throw new InvalidOperationException("B&A&HB TGC compatibility requires the canonical default inventory template.");
        Slot[] armBand = inventory.Properties?.Slots?.Where(slot => string.Equals(slot.Name, "ArmBand", StringComparison.Ordinal)).Take(2).ToArray() ?? [];
        Slot[] belt = inventory.Properties?.Slots?.Where(slot => string.Equals(slot.Name, RuntimeIdentity.DedicatedBeltWireSlotId, StringComparison.Ordinal)).Take(2).ToArray() ?? [];
        if (armBand.Length != 1 || belt.Length != 1)
            throw new InvalidOperationException("B&A&HB TGC compatibility requires unique ArmBand and dedicated Belt slot hosts.");
        HashSet<MongoId>? armBandFilter = armBand[0].Properties?.Filters?.SingleOrDefault()?.Filter;
        HashSet<MongoId>? beltFilter = belt[0].Properties?.Filters?.SingleOrDefault()?.Filter;
        if (armBandFilter == null || beltFilter == null)
            throw new InvalidOperationException("B&A&HB TGC compatibility requires exact mutable slot filters.");

        int armBandRemovals = 0;
        int beltAdmissions = 0;
        foreach (MongoId id in publishedBelts)
        {
            if (armBandFilter.Remove(id)) armBandRemovals++;
            if (beltFilter.Add(id)) beltAdmissions++;
        }

        // First erase TGC's broad PouchesInSecureContainer side effect from all
        // secure-container hosts. ExtendGamma then republishes only our exact two-ID
        // allowlist into the supported Gamma family.
        int secureRemovals = 0;
        foreach (TemplateItem host in templateTable.Items.Values.Where(IsSecureContainerHost))
        {
            bool supportedGamma = GammaFamily.Contains(host.Id);
            foreach (GridFilter filter in host.Properties?.Grids?.SelectMany(grid => grid.Properties?.Filters ?? []) ?? [])
            {
                if (filter.Filter == null) continue;
                if (!supportedGamma)
                    foreach (MongoId id in TgcSecurePouches)
                        if (filter.Filter.Remove(id)) secureRemovals++;
                if (filter.Filter.Remove(TgcToolBox)) secureRemovals++;
            }
        }

        foreach (MongoId id in publishedBelts)
        {
            if (armBandFilter.Contains(id) || !beltFilter.Contains(id))
                throw new InvalidOperationException($"B&A&HB TGC Belt ownership commit failed for {id}.");
        }
        return new TgcCommitResult(publishedBelts.Count, publishedPouches.Count, armBandRemovals, beltAdmissions, secureRemovals);
    }

    private static bool IsSecureContainerHost(TemplateItem item)
    {
        string id = item.Id.ToString();
        return string.Equals(id, "5857a8bc2459772bad15db29", StringComparison.Ordinal)
            || string.Equals(id, "5857a8b324597729ab0a0e7d", StringComparison.Ordinal)
            || string.Equals(id, "59db794186f77448bc595262", StringComparison.Ordinal)
            || string.Equals(id, "5857a8bd2459772bad15db2a", StringComparison.Ordinal)
            || string.Equals(id, "665ee77ccf2d642e98220bca", StringComparison.Ordinal)
            || string.Equals(id, "68f117b8121d878a2303eee0", StringComparison.Ordinal)
            || string.Equals(id, "5c093ca986f7740a1867ab12", StringComparison.Ordinal)
            || string.Equals(id, "674e52f6c9d3a56bdb5c04f2", StringComparison.Ordinal)
            || string.Equals(id, "664a55d84a90fc2c8a6305c9", StringComparison.Ordinal)
            || string.Equals(id, "68f8e04eae031982b00e7aaf", StringComparison.Ordinal);
    }

    private readonly record struct TgcCommitResult(
        int BeltsPublished,
        int SecurePouchesPublished,
        int ArmBandRemovals,
        int BeltAdmissions,
        int SecureRemovals);

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
                filter.ExcludedFilter ??= [];
                foreach (MongoId id in compatible)
                {
                    if (id != gammaId && filter.Filter.Add(id)) changed++;
                    if (filter.ExcludedFilter.Remove(id)) changed++;
                }
            }

            foreach (MongoId id in compatible.Where(id => id != gammaId))
                RequireAdmission(gammaId.ToString(), null, id, "compatible simple container");
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
            .Any(filter => filter.Filter?.Contains(expected) == true && !IsExcludedByAncestry(filter.ExcludedFilter, expected));
        if (!admitted)
            throw new InvalidOperationException($"B&A&HB final compatibility failed: {label} ({expected}) is not admitted by {hostId}/{gridName ?? "all grids"}.");
    }

    private bool IsExcludedByAncestry(HashSet<MongoId>? excluded, MongoId itemId)
    {
        if (excluded == null || excluded.Count == 0) return false;
        MongoId? current = itemId;
        var seen = new HashSet<MongoId>();
        while (current != null && seen.Add(current.Value))
        {
            if (excluded.Contains(current.Value)) return true;
            if (!templateTable.Items.TryGetValue(current.Value, out TemplateItem? item)) return false;
            current = item.Parent;
        }
        return false;
    }
}
