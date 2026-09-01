using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseFilterOwnershipIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag filter ownership isolation regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));
        Require(source, "Filter = new HashSet<MongoId>(filter.Filter!)",
            "Dogtag Case included filters must be deep-copied instead of sharing the canonical source HashSet");
        Require(source, "new HashSet<MongoId>(filter.ExcludedFilter)",
            "Dogtag Case excluded filters must be deep-copied instead of sharing the canonical source HashSet");
        Require(source, "Filters = copiedFilters",
            "the cloned grid must publish only the independently owned copied filter groups");

        int copy = source.IndexOf("var copiedFilters = sourceFilters", StringComparison.Ordinal);
        int details = source.IndexOf("var details = new NewItemFromCloneDetails", StringComparison.Ordinal);
        int publish = source.IndexOf("Filters = copiedFilters", StringComparison.Ordinal);
        int create = source.IndexOf("customItemService.CreateItemFromClone(details)", StringComparison.Ordinal);
        if (copy < 0 || details < 0 || publish < 0 || create < 0
            || !(copy < details && details < publish && publish < create))
            throw new InvalidOperationException("Dogtag filter ownership isolation regression failed: source filters must be copied before clone construction and publication.");

        // Exact positive construction + ordering proofs are authoritative here.
        // Negative substring checks against source variable/property names are not:
        // those names necessarily occur in the legitimate copy expression itself.
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(candidate)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseItem.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag filter ownership isolation regression failed: " + message + ".");
    }
}
