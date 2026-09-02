using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Fail-closed preflight for the live canonical EFT/SPT Dogtag Case filter.
/// This runs immediately before <see cref="DogtagCaseItem"/> so a synchronized
/// source/product drift cannot broaden the dogtag-only grid to any B&A&HB-owned
/// wearable/container template while still satisfying later value parity.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 2)]
public sealed class DogtagCaseCanonicalFilterPreflight(
    TemplateTable templateTable,
    ISptLogger<DogtagCaseCanonicalFilterPreflight> logger) : IOnLoad
{
    private static readonly MongoId SourceDogtagCaseTpl = new("5c093e3486f77430cb02e593");

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!templateTable.Items.TryGetValue(SourceDogtagCaseTpl, out var source))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical EFT/SPT Dogtag Case template is missing.");

        var grids = source.Properties?.Grids?.ToArray();
        if (grids == null || grids.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid boundary is missing or ambiguous.");

        var filters = grids[0].Properties?.Filters?.ToArray();
        if (filters == null || filters.Length == 0)
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical dogtag filter-group contract is empty.");

        foreach (var group in filters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var included = group.Filter;
            if (included == null || included.Count == 0)
                throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical included dogtag filter is empty.");

            foreach (MongoId accepted in included)
            {
                if (PersistentIdentityManifest.IsOwnedTemplate(accepted.ToString()))
                    throw new InvalidOperationException(
                        "B&A&HB Dogtag Case preflight refused: canonical dogtag grid was broadened to a B&A&HB-owned product template.");
            }
        }

        // ExcludedFilter is deliberately not constrained here: the canonical EFT/SPT
        // taxonomy remains authoritative and may legitimately exclude arbitrary IDs.
        // Only positive admission of an owned B&A&HB template is forbidden.
        logger.Success("B&A&HB Dogtag Case canonical filter preflight passed: non-empty EFT/SPT taxonomy contains no B&A&HB-owned product admissions.");
        return Task.CompletedTask;
    }
}
