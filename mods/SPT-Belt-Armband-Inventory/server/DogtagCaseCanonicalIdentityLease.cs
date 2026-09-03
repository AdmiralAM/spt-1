using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Tables;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Carries the exact canonical Dogtag Case source authority proven at Preload +2
/// into the Preload +3 product transaction. The lease is single-consumer and
/// fail-closed: wrapper replacement and in-place filter-content drift cannot
/// inherit preflight authority.
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
            HashSet<MongoId>?[] excludedValues)
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

            var grids = liveSource.Properties?.Grids?.ToArray();
            if (grids == null || grids.Length != 1 || !ReferenceEquals(grids[0], Grid))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical grid identity/cardinality drifted after Preload +2.");
            if (!ReferenceEquals(grids[0].Properties, GridProperties)
                || !ReferenceEquals(grids[0].Properties?.Filters, FiltersCollection))
                throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: canonical grid properties/filters wrapper was replaced after Preload +2.");

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
            pending = next;
        }
    }

    internal static Lease Consume(TemplateTable templates, TemplateItem source)
    {
        Lease lease;
        lock (Sync)
        {
            lease = pending
                ?? throw new InvalidOperationException("B&A&HB Dogtag Case canonical lease refused: Preload +2 identity authority is missing or was already consumed.");
            pending = null;
        }

        lease.RequireCurrent(templates, source);
        return lease;
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
            groups.Select(x => x.ExcludedFilter == null ? null : new HashSet<MongoId>(x.ExcludedFilter)).ToArray());
    }
}
