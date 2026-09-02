using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseCanonicalFilterPreflightRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag canonical filter preflight regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseCanonicalFilterPreflight.cs"));
        Require(source, "[Injectable(TypePriority = OnLoadOrder.Preload + 2)]",
            "canonical preflight must execute before DogtagCaseItem preload +3 publication");
        Require(source, "TemplateItem source = RequireCanonicalSourceContract(cancellationToken);",
            "canonical preflight must begin with a complete value proof of the live source");
        Require(source, "CanonicalIdentitySnapshot identity = CaptureCanonicalIdentity(source);",
            "canonical preflight must capture the complete nested mutable identity chain after the first value proof");
        Require(source, "!ReferenceEquals(liveSource, source)",
            "canonical preflight must pin exact TemplateTable source identity between value proofs");
        Require(source, "RequireCanonicalSourceContract(cancellationToken, source);",
            "canonical preflight must re-prove the same source reference after identity validation");
        Require(source, "RequireCanonicalIdentity(source, identity);",
            "canonical preflight must reject value-identical replacement of root/grid/filter objects after the second value proof");
        Require(source, "expectedReference != null && !ReferenceEquals(source, expectedReference)",
            "second canonical value proof must independently fail closed if source identity changes");
        Require(source, "!ReferenceEquals(source.Properties, expected.Properties)",
            "canonical preflight must pin root properties identity");
        Require(source, "!ReferenceEquals(source.Properties?.Grids, expected.GridsCollection)",
            "canonical preflight must pin the mutable canonical grids collection identity");
        Require(source, "!ReferenceEquals(grids[0], expected.Grid)",
            "canonical preflight must pin canonical grid object identity");
        Require(source, "!ReferenceEquals(grids[0].Properties, expected.GridProperties)",
            "canonical preflight must pin canonical grid-properties identity");
        Require(source, "!ReferenceEquals(grids[0].Properties?.Filters, expected.FiltersCollection)",
            "canonical preflight must pin the mutable filter-group collection identity");
        Require(source, "!ReferenceEquals(groups[i], expected.FilterGroups[i])",
            "canonical preflight must pin every filter-group object identity");
        Require(source, "!ReferenceEquals(groups[i].Filter, expected.IncludedFilters[i])",
            "canonical preflight must pin every included filter-set identity");
        Require(source, "!ReferenceEquals(groups[i].ExcludedFilter, expected.ExcludedFilters[i])",
            "canonical preflight must pin nullable excluded filter-set identity without constraining its taxonomy");
        Require(source, "grids == null || grids.Length != 1",
            "canonical preflight must require the exact single-grid boundary");
        Require(source, "!Equals(grid.Parent, SourceDogtagCaseTpl)",
            "canonical preflight must reject a detached/reparented EFT Dogtag Case grid before filter authority is consumed");
        Require(source, "filters == null || filters.Length == 0",
            "canonical preflight must reject a vacuous filter-group contract");
        Require(source, "included == null || included.Count == 0",
            "canonical preflight must reject empty positive-admission filters");
        Require(source, "PersistentIdentityManifest.IsOwnedTemplate(accepted.ToString())",
            "canonical preflight must reject every B&A&HB-owned product admission rather than only self-recursion");
        Require(source, "ExcludedFilter is deliberately not constrained here",
            "preflight must preserve live EFT/SPT excluded-filter authority instead of hardcoding taxonomy");

        int priority = source.IndexOf("OnLoadOrder.Preload + 2", StringComparison.Ordinal);
        int firstProof = source.IndexOf("TemplateItem source = RequireCanonicalSourceContract(cancellationToken);", StringComparison.Ordinal);
        int capture = source.IndexOf("CanonicalIdentitySnapshot identity = CaptureCanonicalIdentity(source);", StringComparison.Ordinal);
        int identity = source.IndexOf("!ReferenceEquals(liveSource, source)", StringComparison.Ordinal);
        int secondProof = source.IndexOf("RequireCanonicalSourceContract(cancellationToken, source);", StringComparison.Ordinal);
        int nestedIdentity = source.IndexOf("RequireCanonicalIdentity(source, identity);", StringComparison.Ordinal);
        int gridsCollection = source.IndexOf("!ReferenceEquals(source.Properties?.Grids, expected.GridsCollection)", StringComparison.Ordinal);
        int filtersCollection = source.IndexOf("!ReferenceEquals(grids[0].Properties?.Filters, expected.FiltersCollection)", StringComparison.Ordinal);
        int grid = source.IndexOf("grids == null || grids.Length != 1", StringComparison.Ordinal);
        int parent = source.IndexOf("!Equals(grid.Parent, SourceDogtagCaseTpl)", StringComparison.Ordinal);
        int filters = source.IndexOf("filters == null || filters.Length == 0", StringComparison.Ordinal);
        int included = source.IndexOf("included == null || included.Count == 0", StringComparison.Ordinal);
        int owned = source.IndexOf("PersistentIdentityManifest.IsOwnedTemplate(accepted.ToString())", StringComparison.Ordinal);
        if (priority < 0 || firstProof < priority || capture < firstProof || identity < capture
            || secondProof < identity || nestedIdentity < secondProof
            || gridsCollection < nestedIdentity || filtersCollection < gridsCollection
            || grid < nestedIdentity || parent < grid || filters < parent || included < filters || owned < included)
            throw new InvalidOperationException("Dogtag canonical filter preflight regression failed: value -> nested identity capture -> source identity -> value -> complete nested identity proof ordering changed.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "server", "DogtagCaseCanonicalFilterPreflight.cs");
            if (File.Exists(candidate)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseCanonicalFilterPreflight.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseCanonicalFilterPreflight.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag canonical filter preflight regression failed: " + message + ".");
    }
}
