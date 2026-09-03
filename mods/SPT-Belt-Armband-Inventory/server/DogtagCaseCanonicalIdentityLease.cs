using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Carries the exact canonical Dogtag Case source authority proven at Preload +2
/// into the Preload +3 product transaction. The lease is single-consumer and
/// fail-closed: wrapper replacement, in-place filter-content drift, and scalar
/// canonical geometry/presentation drift cannot inherit preflight authority.
/// </summary>
internal static class DogtagCaseCanonicalIdentityLease
{
    private static readonly object Sync = new();
    private static Lease? pending;

    internal sealed class Lease
    {
        internal Lease(
            TemplateItem source,
            object properties,
            object gridsCollection,
            object grid,
            object gridProperties,
            object filtersCollection,
            object[] groups,
            object[] included,
            object?[] excluded,
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
            Source = source;
            Properties = properties;
            GridsCollection = gridsCollection;
            Grid = grid;
            GridProperties = gridProperties;
            FiltersCollection = filtersCollection;
            Groups = groups;
            Included = included;
            Excluded = excluded;
            IncludedValues = includedValues;
            ExcludedValues = excludedValues;
            SourceParent = sourceParent;
            BackgroundColor = backgroundColor;
            ExaminedByDefault = examinedByDefault;
            Width = width;
            Height = height;
            StackMaxSize = stackMaxSize;
            GridName = gridName;
            GridId = gridId;
            GridParent = gridParent;
            GridPrototype = gridPrototype;
            CellsH = cellsH;
            CellsV = cellsV;
            MinCount = minCount;
            MaxCount = maxCount;
            MaxWeight = maxWeight;
            IsSortingTable = isSortingTable;
        }

        internal TemplateItem Source { get; }
        private object Properties { get; }
        private object GridsCollection { get; }
        private object Grid { get; }
        private object GridProperties { get; }
        private object FiltersCollection { get; }
        private object[] Groups { get; }
        private object[] Included { get; }
        private object?[] Excluded { get; }
        private HashSet<MongoId>[] IncludedValues { get; }
        private HashSet<MongoId>?[] ExcludedValues { get; }
        private object? SourceParent { get; }
        private object? BackgroundColor { get; }
        private object? ExaminedByDefault { get; }
        private object? Width { get; }
        private object? Height { get; }
        private object? StackMaxSize { get; }
        private object? GridName { get; }
        private object? GridId { get; }
        private object? GridParent { get; }
        private object? GridPrototype { get; }
        private object? CellsH { get; }
        private object? CellsV { get; }
        private object? MinCount { get; }
        private object? MaxCount { get; }
        private object? MaxWeight { get; }
        private object? IsSortingTable { get; }

        internal void RequireCurrent(TemplateTable templates, TemplateItem expectedSource)
        {
            ArgumentNullException.ThrowIfNull(templates);
            ArgumentNullException.ThrowIfNull(expectedSource);

            if (!ReferenceEquals(Source, expectedSource))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: Preload +3 source differs from the Preload +2 source reference.");
            if (!templates.Items.TryGetValue(DogtagCaseCanonicalIdentityLease.SourceDogtagCaseTpl, out var liveSource)
                || !ReferenceEquals(liveSource, Source))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical source root was replaced after Preload +2.");
            if (!ReferenceEquals(liveSource.Properties, Properties)
                || !ReferenceEquals(liveSource.Properties?.Grids, GridsCollection))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical source properties/grids wrapper was replaced after Preload +2.");
            if (!Equals(liveSource.Parent, SourceParent)
                || !Equals(liveSource.Properties?.BackgroundColor, BackgroundColor)
                || !Equals(liveSource.Properties?.ExaminedByDefault, ExaminedByDefault)
                || !Equals(liveSource.Properties?.Width, Width)
                || !Equals(liveSource.Properties?.Height, Height)
                || !Equals(liveSource.Properties?.StackMaxSize, StackMaxSize))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical root parent/presentation/geometry values drifted after Preload +2.");

            var grids = liveSource.Properties?.Grids?.ToArray();
            if (grids == null || grids.Length != 1 || !ReferenceEquals(grids[0], Grid))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical grid identity/cardinality drifted after Preload +2.");
            if (!ReferenceEquals(grids[0].Properties, GridProperties)
                || !ReferenceEquals(grids[0].Properties?.Filters, FiltersCollection))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical grid properties/filters wrapper was replaced after Preload +2.");
            if (!Equals(grids[0].Name, GridName)
                || !Equals(grids[0].Id, GridId)
                || !Equals(grids[0].Parent, GridParent)
                || !Equals(grids[0].Prototype, GridPrototype)
                || !Equals(grids[0].Properties?.CellsH, CellsH)
                || !Equals(grids[0].Properties?.CellsV, CellsV)
                || !Equals(grids[0].Properties?.MinCount, MinCount)
                || !Equals(grids[0].Properties?.MaxCount, MaxCount)
                || !Equals(grids[0].Properties?.MaxWeight, MaxWeight)
                || !Equals(grids[0].Properties?.IsSortingTable, IsSortingTable))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical grid identity/geometry/sorting values drifted after Preload +2.");

            var groups = grids[0].Properties?.Filters?.ToArray();
            if (groups == null || groups.Length != Groups.Length)
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical filter-group cardinality drifted after Preload +2.");

            for (int i = 0; i < groups.Length; i++)
            {
                if (!ReferenceEquals(groups[i], Groups[i])
                    || !ReferenceEquals(groups[i].Filter, Included[i])
                    || !ReferenceEquals(groups[i].ExcludedFilter, Excluded[i]))
                    throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical filter group/include/exclude identity drifted after Preload +2.");

                HashSet<MongoId>? included = groups[i].Filter;
                HashSet<MongoId>? excluded = groups[i].ExcludedFilter;
                HashSet<MongoId>? expectedExcluded = ExcludedValues[i];
                if (included == null || included.Count == 0 || !included.SetEquals(IncludedValues[i])
                    || included.Any(id => PersistentIdentityManifest.IsOwnedTemplate(id.ToString())))
                    throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical included-filter content drifted after Preload +2.");
                if ((excluded == null) != (expectedExcluded == null)
                    || (excluded != null && expectedExcluded != null && !excluded.SetEquals(expectedExcluded)))
                    throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical excluded-filter content drifted after Preload +2.");
            }
        }
    }

    internal static readonly MongoId SourceDogtagCaseTpl = new("5c093e3486f77430cb02e593");

    internal static void Publish(TemplateItem source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Lease next = Capture(source);
        lock (Sync)
        {
            if (pending != null)
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: an unconsumed Preload +2 authority is already pending.");
            pending = next;
        }
    }

    internal static Lease Consume(TemplateTable templates, TemplateItem source)
    {
        lock (Sync)
        {
            Lease lease = pending
                ?? throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: Preload +2 identity authority is missing or was already consumed.");

            // Consumption is monotonic. Once Preload +3 attempts to consume the exact
            // Preload +2 authority, any identity/content/scalar drift permanently burns
            // that pending token. Restoring values later (ABA) cannot revive preflight
            // authority that was already challenged against a different live state.
            pending = null;
            lease.RequireCurrent(templates, source);
            return lease;
        }
    }

    internal static void CancelPending(TemplateItem source)
    {
        ArgumentNullException.ThrowIfNull(source);
        lock (Sync)
        {
            Lease lease = pending
                ?? throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease cancellation refused: no pending Preload +2 authority exists.");
            if (!ReferenceEquals(lease.Source, source))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease cancellation refused: pending authority belongs to a different source reference.");
            pending = null;
        }
    }

    private static Lease Capture(TemplateItem source)
    {
        var properties = source.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: source properties are missing.");
        var gridsCollection = properties.Grids
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: source grids collection is missing.");
        var grids = gridsCollection.ToArray();
        if (grids.Length != 1)
            throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: source grid boundary is ambiguous.");
        var grid = grids[0];
        var gridProperties = grid.Properties
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: source grid properties are missing.");
        var filtersCollection = gridProperties.Filters
            ?? throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: source filters collection is missing.");
        var groups = filtersCollection.ToArray();
        if (groups.Length == 0 || groups.Any(x => x.Filter == null || x.Filter.Count == 0))
            throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: source filters are empty or incomplete.");

        return new Lease(
            source,
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
}
