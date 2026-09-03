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
/// source/product drift cannot broaden the dogtag-only grid or replace canonical
/// geometry/presentation authority while still satisfying later parity checks.
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
        object?[] excludedFilters,
        HashSet<MongoId>[] includedValues,
        HashSet<MongoId>?[] excludedValues,
        object? sourceParent,
        object? backgroundColor,
        object? examinedByDefault,
        object? width,
        object? height,
        object? stackMaxSize,
        object? gridName,
        object? gridId,
        object? gridParent,
        object? gridPrototype,
        object? cellsH,
        object? cellsV,
        object? minCount,
        object? maxCount,
        object? maxWeight,
        object? isSortingTable)
    {
        public object Properties { get; } = properties;
        public object GridsCollection { get; } = gridsCollection;
        public object Grid { get; } = grid;
        public object GridProperties { get; } = gridProperties;
        public object FiltersCollection { get; } = filtersCollection;
        public object[] FilterGroups { get; } = filterGroups;
        public object[] IncludedFilters { get; } = includedFilters;
        public object?[] ExcludedFilters { get; } = excludedFilters;
        public HashSet<MongoId>[] IncludedValues { get; } = includedValues;
        public HashSet<MongoId>?[] ExcludedValues { get; } = excludedValues;
        public object? SourceParent { get; } = sourceParent;
        public object? BackgroundColor { get; } = backgroundColor;
        public object? ExaminedByDefault { get; } = examinedByDefault;
        public object? Width { get; } = width;
        public object? Height { get; } = height;
        public object? StackMaxSize { get; } = stackMaxSize;
        public object? GridName { get; } = gridName;
        public object? GridId { get; } = gridId;
        public object? GridParent { get; } = gridParent;
        public object? GridPrototype { get; } = gridPrototype;
        public object? CellsH { get; } = cellsH;
        public object? CellsV { get; } = cellsV;
        public object? MinCount { get; } = minCount;
        public object? MaxCount { get; } = maxCount;
        public object? MaxWeight { get; } = maxWeight;
        public object? IsSortingTable { get; } = isSortingTable;
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

        DogtagCaseCanonicalIdentityLease.Publish(source);
        try
        {
            // Publish() captures its own +3 lease. Re-prove the full original +2
            // identity, taxonomy AND scalar source values afterwards so neither a
            // wrapper mutation nor a same-reference geometry/presentation mutation
            // can become fresh lease authority in the proof -> Publish window.
            RequireCanonicalIdentity(source, identity);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch
        {
            // Pending +2 authority is metadata only and must not survive any failed
            // post-Publish proof. Cancellation, identity drift, scalar drift, or any
            // other exception all abandon the exact-source lease without mutating the
            // canonical EFT/SPT source. A later retry must establish fresh +2 authority.
            DogtagCaseCanonicalIdentityLease.CancelPending(source);
            throw;
        }

        logger.Success("B&A&HB Dogtag Case canonical preflight passed: exact source/root/grid/filter identity, taxonomy and scalar geometry/presentation values are stable across lease publication and leased to Preload +3.");
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
                    throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical dogtag grid was broadened to a B&A&HB-owned product template.");
            }

            // ExcludedFilter remains live EFT/SPT authority. We pin identity/content;
            // we never rewrite canonical exclusions.
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
        if (groups.Length == 0 || groups.Any(x => x.Filter == null || x.Filter.Count == 0))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filters drifted during identity capture.");

        return new CanonicalIdentitySnapshot(
            properties,
            gridsCollection,
            grid,
            gridProperties,
            filtersCollection,
            groups.Cast<object>().ToArray(),
            groups.Select(x => (object)x.Filter!).ToArray(),
            groups.Select(x => (object?)x.ExcludedFilter).ToArray(),
            groups.Select(x => new HashSet<MongoId>(x.Filter!)).ToArray(),
            groups.Select(x => x.ExcludedFilter == null ? null : new HashSet<MongoId>(x.ExcludedFilter)).ToArray(),
            source.Parent,
            properties.BackgroundColor,
            properties.ExaminedByDefault,
            properties.Width,
            properties.Height,
            properties.StackMaxSize,
            grid.Name,
            grid.Id,
            grid.Parent,
            grid.Prototype,
            gridProperties.CellsH,
            gridProperties.CellsV,
            gridProperties.MinCount,
            gridProperties.MaxCount,
            gridProperties.MaxWeight,
            gridProperties.IsSortingTable);
    }

    private static void RequireCanonicalIdentity(TemplateItem source, CanonicalIdentitySnapshot expected)
    {
        if (!ReferenceEquals(source.Properties, expected.Properties))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical root properties were replaced during validation.");
        if (!ReferenceEquals(source.Properties?.Grids, expected.GridsCollection))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grids collection was replaced during validation.");
        if (!Equals(source.Parent, expected.SourceParent)
            || !Equals(source.Properties?.BackgroundColor, expected.BackgroundColor)
            || !Equals(source.Properties?.ExaminedByDefault, expected.ExaminedByDefault)
            || !Equals(source.Properties?.Width, expected.Width)
            || !Equals(source.Properties?.Height, expected.Height)
            || !Equals(source.Properties?.StackMaxSize, expected.StackMaxSize))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical root parent/presentation/geometry values changed during validation.");

        var grids = source.Properties?.Grids?.ToArray();
        if (grids == null || grids.Length != 1 || !ReferenceEquals(grids[0], expected.Grid))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid object was replaced during validation.");
        if (!ReferenceEquals(grids[0].Properties, expected.GridProperties))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid properties were replaced during validation.");
        if (!ReferenceEquals(grids[0].Properties?.Filters, expected.FiltersCollection))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filters collection was replaced during validation.");
        if (!Equals(grids[0].Name, expected.GridName)
            || !Equals(grids[0].Id, expected.GridId)
            || !Equals(grids[0].Parent, expected.GridParent)
            || !Equals(grids[0].Prototype, expected.GridPrototype)
            || !Equals(grids[0].Properties?.CellsH, expected.CellsH)
            || !Equals(grids[0].Properties?.CellsV, expected.CellsV)
            || !Equals(grids[0].Properties?.MinCount, expected.MinCount)
            || !Equals(grids[0].Properties?.MaxCount, expected.MaxCount)
            || !Equals(grids[0].Properties?.MaxWeight, expected.MaxWeight)
            || !Equals(grids[0].Properties?.IsSortingTable, expected.IsSortingTable))
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical grid identity/geometry/sorting values changed during validation.");

        var groups = grids[0].Properties?.Filters?.ToArray();
        if (groups == null || groups.Length != expected.FilterGroups.Length)
            throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filter-group cardinality changed during validation.");

        for (int i = 0; i < groups.Length; i++)
        {
            if (!ReferenceEquals(groups[i], expected.FilterGroups[i])
                || !ReferenceEquals(groups[i].Filter, expected.IncludedFilters[i])
                || !ReferenceEquals(groups[i].ExcludedFilter, expected.ExcludedFilters[i]))
                throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical filter group/include/exclude identity was replaced during validation.");

            HashSet<MongoId>? included = groups[i].Filter;
            HashSet<MongoId>? excluded = groups[i].ExcludedFilter;
            HashSet<MongoId>? expectedExcluded = expected.ExcludedValues[i];
            if (included == null || included.Count == 0 || !included.SetEquals(expected.IncludedValues[i])
                || included.Any(id => PersistentIdentityManifest.IsOwnedTemplate(id.ToString())))
                throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical included-filter content changed during validation.");
            if ((excluded == null) != (expectedExcluded == null)
                || (excluded != null && expectedExcluded != null && !excluded.SetEquals(expectedExcluded)))
                throw new InvalidOperationException("B&A&HB Dogtag Case preflight refused: canonical excluded-filter content changed during validation.");
        }
    }
}
