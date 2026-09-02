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
        Require(source, "grids == null || grids.Length != 1",
            "canonical preflight must require the exact single-grid boundary");
        Require(source, "filters == null || filters.Length == 0",
            "canonical preflight must reject a vacuous filter-group contract");
        Require(source, "included == null || included.Count == 0",
            "canonical preflight must reject empty positive-admission filters");
        Require(source, "PersistentIdentityManifest.IsOwnedTemplate(accepted.ToString())",
            "canonical preflight must reject every B&A&HB-owned product admission rather than only self-recursion");
        Require(source, "ExcludedFilter is deliberately not constrained here",
            "preflight must preserve live EFT/SPT excluded-filter authority instead of hardcoding taxonomy");

        int priority = source.IndexOf("OnLoadOrder.Preload + 2", StringComparison.Ordinal);
        int lookup = source.IndexOf("TryGetValue(SourceDogtagCaseTpl", StringComparison.Ordinal);
        int grid = source.IndexOf("grids == null || grids.Length != 1", StringComparison.Ordinal);
        int filters = source.IndexOf("filters == null || filters.Length == 0", StringComparison.Ordinal);
        int included = source.IndexOf("included == null || included.Count == 0", StringComparison.Ordinal);
        int owned = source.IndexOf("PersistentIdentityManifest.IsOwnedTemplate(accepted.ToString())", StringComparison.Ordinal);
        if (priority < 0 || lookup < priority || grid < lookup || filters < grid || included < filters || owned < included)
            throw new InvalidOperationException("Dogtag canonical filter preflight regression failed: exact pre-publication proof ordering changed.");
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
