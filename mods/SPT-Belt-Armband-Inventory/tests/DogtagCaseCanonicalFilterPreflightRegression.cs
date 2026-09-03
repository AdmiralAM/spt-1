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
        Require(source, "[Injectable(TypePriority = OnLoadOrder.Preload + 2)]", "canonical preflight must execute before DogtagCaseItem preload +3 publication");
        Require(source, "TemplateItem source = RequireCanonicalSourceContract(cancellationToken);", "canonical preflight must begin with a complete value proof of the live source");
        Require(source, "CanonicalIdentitySnapshot identity = CaptureCanonicalIdentity(source);", "canonical preflight must capture mutable identity/content/scalar authority after the first value proof");
        Require(source, "!ReferenceEquals(liveSource, source)", "canonical preflight must pin exact TemplateTable source identity between value proofs");
        Require(source, "RequireCanonicalSourceContract(cancellationToken, source);", "canonical preflight must re-prove the same source reference after identity validation");
        Require(source, "RequireCanonicalIdentity(source, identity);", "canonical preflight must reject nested replacement/content/scalar drift before and after lease publication");
        Require(source, "expectedReference != null && !ReferenceEquals(source, expectedReference)", "second canonical value proof must independently fail closed if source identity changes");
        Require(source, "!ReferenceEquals(source.Properties, expected.Properties)", "canonical preflight must pin root properties identity");
        Require(source, "!ReferenceEquals(source.Properties?.Grids, expected.GridsCollection)", "canonical preflight must pin mutable canonical grids collection identity");
        Require(source, "!ReferenceEquals(grids[0], expected.Grid)", "canonical preflight must pin canonical grid object identity");
        Require(source, "!ReferenceEquals(grids[0].Properties, expected.GridProperties)", "canonical preflight must pin canonical grid-properties identity");
        Require(source, "!ReferenceEquals(grids[0].Properties?.Filters, expected.FiltersCollection)", "canonical preflight must pin mutable filter-group collection identity");
        Require(source, "!ReferenceEquals(groups[i], expected.FilterGroups[i])", "canonical preflight must pin every filter-group object identity");
        Require(source, "!ReferenceEquals(groups[i].Filter, expected.IncludedFilters[i])", "canonical preflight must pin every included filter-set identity");
        Require(source, "!ReferenceEquals(groups[i].ExcludedFilter, expected.ExcludedFilters[i])", "canonical preflight must pin nullable excluded filter-set identity without replacing its taxonomy");

        string[] scalarPins =
        {
            "!Equals(source.Parent, expected.SourceParent)",
            "!Equals(source.Properties?.BackgroundColor, expected.BackgroundColor)",
            "!Equals(source.Properties?.ExaminedByDefault, expected.ExaminedByDefault)",
            "!Equals(source.Properties?.Width, expected.Width)",
            "!Equals(source.Properties?.Height, expected.Height)",
            "!Equals(source.Properties?.StackMaxSize, expected.StackMaxSize)",
            "!Equals(grids[0].Name, expected.GridName)",
            "!Equals(grids[0].Id, expected.GridId)",
            "!Equals(grids[0].Parent, expected.GridParent)",
            "!Equals(grids[0].Prototype, expected.GridPrototype)",
            "!Equals(grids[0].Properties?.CellsH, expected.CellsH)",
            "!Equals(grids[0].Properties?.CellsV, expected.CellsV)",
            "!Equals(grids[0].Properties?.MinCount, expected.MinCount)",
            "!Equals(grids[0].Properties?.MaxCount, expected.MaxCount)",
            "!Equals(grids[0].Properties?.MaxWeight, expected.MaxWeight)",
            "!Equals(grids[0].Properties?.IsSortingTable, expected.IsSortingTable)"
        };
        foreach (string pin in scalarPins) Require(source, pin, "canonical preflight must pin scalar source authority: " + pin);

        Require(source, "grids == null || grids.Length != 1", "canonical preflight must require the exact single-grid boundary");
        Require(source, "!Equals(grid.Parent, SourceDogtagCaseTpl)", "canonical preflight must reject a detached/reparented EFT Dogtag Case grid before filter authority is consumed");
        Require(source, "filters == null || filters.Length == 0", "canonical preflight must reject a vacuous filter-group contract");
        Require(source, "included == null || included.Count == 0", "canonical preflight must reject empty positive-admission filters");
        Require(source, "PersistentIdentityManifest.IsOwnedTemplate(accepted.ToString())", "canonical preflight must reject every B&A&HB-owned product admission rather than only self-recursion");
        Require(source, "ExcludedFilter remains live EFT/SPT authority", "preflight must preserve live EFT/SPT excluded-filter authority instead of hardcoding taxonomy");

        int firstProof = source.IndexOf("TemplateItem source = RequireCanonicalSourceContract(cancellationToken);", StringComparison.Ordinal);
        int capture = source.IndexOf("CanonicalIdentitySnapshot identity = CaptureCanonicalIdentity(source);", StringComparison.Ordinal);
        int secondProof = source.IndexOf("RequireCanonicalSourceContract(cancellationToken, source);", StringComparison.Ordinal);
        int proofBeforePublish = source.IndexOf("RequireCanonicalIdentity(source, identity);", secondProof, StringComparison.Ordinal);
        int publish = source.IndexOf("DogtagCaseCanonicalIdentityLease.Publish(source);", StringComparison.Ordinal);
        int proofAfterPublish = source.IndexOf("RequireCanonicalIdentity(source, identity);", publish + 1, StringComparison.Ordinal);
        if (firstProof < 0 || capture < firstProof || secondProof < capture || proofBeforePublish < secondProof
            || publish < proofBeforePublish || proofAfterPublish < publish)
            throw new InvalidOperationException("Dogtag canonical filter preflight regression failed: initial proof -> capture -> same-source proof -> full proof -> lease capture -> full post-capture proof ordering changed.");
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
