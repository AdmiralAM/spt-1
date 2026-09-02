using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Final preload semantic guard for the shared vanilla Dogtag host. The included
/// filter may preserve BEAR/USEC and the B&A&HB Dogtag Case while an ExcludedFilter
/// silently negates one of them; that shape must fail closed. Foreign exclusions
/// remain untouched and authoritative.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 4)]
public sealed class DogtagCaseHostExclusionGuard(TemplateTable templateTable) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DogtagCaseHostExclusionPolicy.RequireCurrentHost(templateTable);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

/// <summary>
/// Re-proves effective Dogtag-slot acceptance immediately before the Dogtag Case
/// trader offer is published. This closes the startup window between preload host
/// commit and TraderRegistration without mutating either included or excluded sets.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.TraderRegistration + 1)]
public sealed class DogtagCaseTraderHostExclusionGuard(TemplateTable templateTable) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DogtagCaseHostExclusionPolicy.RequireCurrentHost(templateTable);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

public static class DogtagCaseHostExclusionPolicy
{
    private static readonly MongoId DefaultInventoryTpl = RuntimeCandidateBeltItem.DefaultInventoryTpl;
    private const string DogtagSlotName = "Dogtag";

    public static void RequireCurrentHost(TemplateTable templateTable)
    {
        ArgumentNullException.ThrowIfNull(templateTable);
        if (!templateTable.Items.TryGetValue(DefaultInventoryTpl, out var inventory))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: DefaultInventory is missing.");

        var slots = inventory.Properties?.Slots?
            .Where(x => string.Equals(x.Name, DogtagSlotName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();
        if (slots == null || slots.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: Dogtag slot is missing or ambiguous.");

        var groups = slots[0].Properties?.Filters?.ToArray();
        if (groups == null || groups.Length != 1 || groups[0].Filter == null)
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: exact single Dogtag filter group is unavailable.");

        RequireEffectiveAcceptance(groups[0].Filter, groups[0].ExcludedFilter);
    }

    public static void RequireEffectiveAcceptance(IEnumerable<MongoId> included, IEnumerable<MongoId>? excluded)
    {
        ArgumentNullException.ThrowIfNull(included);
        var accepted = included.ToHashSet();
        var bear = new MongoId(DogtagCaseHostContract.BearDogtagTemplateId);
        var usec = new MongoId(DogtagCaseHostContract.UsecDogtagTemplateId);
        var dogtagCase = new MongoId(RuntimeIdentity.DogtagCaseItemId);

        if (!accepted.Contains(bear) || !accepted.Contains(usec) || !accepted.Contains(dogtagCase))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: included filter lost BEAR, USEC, or the exact Dogtag Case acceptance.");

        if (excluded == null) return;
        var denied = excluded.ToHashSet();
        if (denied.Contains(bear) || denied.Contains(usec) || denied.Contains(dogtagCase))
            throw new InvalidOperationException("B&A&HB Dogtag exclusion guard refused: ExcludedFilter negates BEAR, USEC, or exact Dogtag Case effective acceptance.");
    }
}
