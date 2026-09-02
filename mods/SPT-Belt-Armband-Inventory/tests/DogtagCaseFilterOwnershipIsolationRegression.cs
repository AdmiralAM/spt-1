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

        Require(source, "ReferenceEquals(candidateProperties, sourceProperties)",
            "existing Dogtag Case root properties must not alias canonical source properties");
        Require(source, "ReferenceEquals(grid, sourceGrid)",
            "existing Dogtag Case grid object must not alias the canonical source grid");
        Require(source, "ReferenceEquals(actual, expected)",
            "existing Dogtag Case grid properties must not alias canonical grid properties");
        Require(source, "ReferenceEquals(actualFilters[i], expectedFilters[i])",
            "existing Dogtag Case filter-group object must not alias canonical filter-group state");
        Require(source, "ReferenceEquals(actualIncluded, expectedIncluded)",
            "existing Dogtag Case included filter set must not alias canonical source state");
        Require(source, "ReferenceEquals(actualExcluded, expectedExcluded)",
            "existing Dogtag Case excluded filter set must not alias canonical source state");

        // Value parity alone is insufficient if another startup participant destroys
        // both mutable filter graphs in the same way. Canonical taxonomy remains the
        // authority, but publication must refuse a vacuous empty contract instead of
        // accepting synchronized empty candidate/source state.
        Require(source, "expectedFilters.Length == 0",
            "canonical Dogtag filter-group contract must remain non-empty during every ValidateExisting reproof");
        Require(source, "actualIncluded.Count == 0 || expectedIncluded.Count == 0",
            "canonical and product included dogtag filter sets must remain non-empty during publication reproof");

        int copy = source.IndexOf("var copiedFilters = sourceFilters", StringComparison.Ordinal);
        int details = source.IndexOf("var details = new NewItemFromCloneDetails", StringComparison.Ordinal);
        int publish = source.IndexOf("Filters = copiedFilters", StringComparison.Ordinal);
        int create = source.IndexOf("customItemService.CreateItemFromClone(details)", StringComparison.Ordinal);
        if (copy < 0 || details < 0 || publish < 0 || create < 0
            || !(copy < details && details < publish && publish < create))
            throw new InvalidOperationException("Dogtag filter ownership isolation regression failed: source filters must be copied before clone construction and publication.");

        int validate = source.IndexOf("private static void ValidateExisting", StringComparison.Ordinal);
        int rootAlias = source.IndexOf("ReferenceEquals(candidateProperties, sourceProperties)", StringComparison.Ordinal);
        int gridAlias = source.IndexOf("ReferenceEquals(grid, sourceGrid)", StringComparison.Ordinal);
        int nonEmptyGroups = source.IndexOf("expectedFilters.Length == 0", StringComparison.Ordinal);
        int groupAlias = source.IndexOf("ReferenceEquals(actualFilters[i], expectedFilters[i])", StringComparison.Ordinal);
        int nonEmptyIncluded = source.IndexOf("actualIncluded.Count == 0 || expectedIncluded.Count == 0", StringComparison.Ordinal);
        int includeAlias = source.IndexOf("ReferenceEquals(actualIncluded, expectedIncluded)", StringComparison.Ordinal);
        int excludeAlias = source.IndexOf("ReferenceEquals(actualExcluded, expectedExcluded)", StringComparison.Ordinal);
        if (validate < 0 || rootAlias < validate || gridAlias < rootAlias || nonEmptyGroups < gridAlias
            || groupAlias < nonEmptyGroups || nonEmptyIncluded < groupAlias
            || includeAlias < nonEmptyIncluded || excludeAlias < includeAlias)
            throw new InvalidOperationException("Dogtag filter ownership isolation regression failed: canonical non-empty/non-alias proofs must remain inside ValidateExisting in root-to-filter order.");
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
