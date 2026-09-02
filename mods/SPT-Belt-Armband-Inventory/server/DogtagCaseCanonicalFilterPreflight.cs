using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
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

    private sealed class CanonicalIdentitySnapshot(
        object properties,
        object gridsCollection,
        object grid,
        object gridProperties,
        object filtersCollection,
        object[] filterGroups,
        object[] includedFilters,
        object?[] excludedFilters)
    {
        public object Properties { get; } = properties;
        public object GridsCollection { get; } = gridsCollection;
        public object Grid { get; } = grid;
        public object GridProperties { get; } = gridProperties;
        public object FiltersCollection { get; } = filtersCollection;
        public object[] FilterGroups { get; } = filterGroups;
        public object[] IncludedFilters { get; } = includedFilters;
        public object?[] ExcludedFilters { get; } = excludedFilters;
    }

    public Task OnLoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        TemplateItem source = RequireCanonicalSourceContract(cancellationToken);
        CanonicalIdentitySnapshot identity = CaptureCanonicalIdentity(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (!templateTable.Items.TryGetValue(SourceDogtagCaseTpl, out var liveSource)
            || !ReferenceEquals(liveSource, source))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical EFT/SPT Dogtag Case template was replaced during validation.");
        RequireCanonicalSourceContract(cancellationToken, source);
        RequireCanonicalIdentity(source, identity);

        // Carry this exact mutable source graph into the immediately following
        // DogtagCaseItem transaction. +3 must consume and re-prove this lease before
        // it clones/copies any canonical geometry or taxonomy.
        DogtagCaseCanonicalIdentityLease.Publish(source);

        logger.Success("B&A&HB Dogtag Case canonical filter preflight passed: exact canonical source/root/grid/filter identity is intact and leased to Preload +3; non-empty EFT/SPT taxonomy contains no B&A&HB-owned product admissions.");
        return Task.CompletedTask;
    }

    private TemplateItem RequireCanonicalSourceContract(CancellationToken cancellationToken, TemplateItem? expectedReference = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!templateTable.Items.TryGetValue(SourceDogtagCaseTpl, out var source))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical EFT/SPT Dogtag Case template is missing.");
        if (expectedReference != null && !ReferenceEquals(source, expectedReference))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical EFT/SPT Dogtag Case template identity drifted during validation.");

        var grids = source.Properties?.Grids?.ToArray();
        if (grids == null || grids.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid boundary is missing or ambiguous.");

        var grid = grids[0];
        if (!Equals(grid.Parent, SourceDogtagCaseTpl))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid parent no longer owns the EFT/SPT Dogtag Case template.");

        var filters = grid.Properties?.Filters?.ToArray();
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

        return source;
    }

    private static CanonicalIdentitySnapshot CaptureCanonicalIdentity(TemplateItem source)
    {
        var properties = source.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical root properties disappeared during identity capture.");
        var gridsCollection = properties.Grids
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grids collection disappeared during identity capture.");
        var grids = gridsCollection.ToArray();
        if (grids.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid boundary drifted during identity capture.");
        var grid = grids[0];
        var gridProperties = grid.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid properties disappeared during identity capture.");
        var filtersCollection = gridProperties.Filters
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filters collection disappeared during identity capture.");
        var groups = filtersCollection.ToArray();
        if (groups.Length == 0 || groups.Any(x => x.Filter == null))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filters drifted during identity capture.");

        return new CanonicalIdentitySnapshot(
            properties,
            gridsCollection,
            grid,
            gridProperties,
            filtersCollection,
            groups.Cast<object>().ToArray(),
            groups.Select(x => (object)x.Filter!).ToArray(),
            groups.Select(x => (object?)x.ExcludedFilter).ToArray());
    }

    private static void RequireCanonicalIdentity(TemplateItem source, CanonicalIdentitySnapshot expected)
    {
        if (!ReferenceEquals(source.Properties, expected.Properties))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical root properties were replaced during validation.");
        if (!ReferenceEquals(source.Properties?.Grids, expected.GridsCollection))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grids collection was replaced during validation.");

        var grids = source.Properties?.Grids?.ToArray();
        if (grids == null || grids.Length != 1 || !ReferenceEquals(grids[0], expected.Grid))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid object was replaced during validation.");
        if (!ReferenceEquals(grids[0].Properties, expected.GridProperties))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid properties were replaced during validation.");
        if (!ReferenceEquals(grids[0].Properties?.Filters, expected.FiltersCollection))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filters collection was replaced during validation.");

        var groups = grids[0].Properties?.Filters?.ToArray();
        if (groups == null || groups.Length != expected.FilterGroups.Length)
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filter-group cardinality changed during validation.");

        for (int i = 0; i < groups.Length; i++)
        {
            if (!ReferenceEquals(groups[i], expected.FilterGroups[i])
                || !ReferenceEquals(groups[i].Filter, expected.IncludedFilters[i])
                || !ReferenceEquals(groups[i].ExcludedFilter, expected.ExcludedFilters[i]))
                throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filter group/include/exclude identity was replaced during validation.");
        }
    }
}
