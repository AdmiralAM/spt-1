using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseEffectiveHostPointInTimeRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag effective-host point-in-time regression failed: module root could not be resolved.");

        string path = Path.Combine(root, "server", "DogtagCaseHostExclusionGuard.cs");
        string source = File.ReadAllText(path);

        Require(source, "var inventoryProperties = inventory.Properties",
            "effective proof must capture DefaultInventory properties");
        Require(source, "var slotsCollection = inventoryProperties.Slots",
            "effective proof must capture the Slots wrapper");
        Require(source, "var slot = slots[0];",
            "effective proof must capture the sole Dogtag slot");
        Require(source, "var slotProperties = slot.Properties",
            "effective proof must capture Dogtag slot properties");
        Require(source, "var filtersCollection = slotProperties.Filters",
            "effective proof must capture the Filters wrapper");
        Require(source, "var filterGroup = groups[0];",
            "effective proof must capture the sole filter-group object");
        Require(source, "var hostFilter = filterGroup.Filter;",
            "effective proof must capture the exact included-filter HashSet");
        Require(source, "DogtagCaseHostContract.RequireCommitted(hostFilter);",
            "effective proof must bracket optional-exclusion evaluation with committed included-host proofs");
        Require(source, "var excludedBefore = SnapshotOptionalExcludedFilter(filterGroup);",
            "optional future exclusions must be detached before effective evaluation");
        Require(source, "RequireEffectiveAcceptance(hostFilter, excludedBefore);",
            "effective acceptance must consume the captured included filter and detached exclusion snapshot");
        Require(source, "var excludedAfter = SnapshotOptionalExcludedFilter(filterGroup);",
            "optional future exclusions must be re-snapshotted before publication authority returns");
        Require(source, "OptionalFilterSetEquals(excludedBefore, excludedAfter)",
            "in-place optional exclusion drift must fail closed");
        Require(source, "ReferenceEquals(liveInventory, inventory)",
            "DefaultInventory replacement during effective proof must fail closed");
        Require(source, "ReferenceEquals(liveInventory.Properties, inventoryProperties)",
            "DefaultInventory properties replacement during effective proof must fail closed");
        Require(source, "ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)",
            "Slots wrapper replacement during effective proof must fail closed");
        Require(source, "ReferenceEquals(liveSlots[0], slot)",
            "Dogtag slot replacement during effective proof must fail closed");
        Require(source, "ReferenceEquals(liveSlots[0].Properties, slotProperties)",
            "Dogtag slot properties replacement during effective proof must fail closed");
        Require(source, "ReferenceEquals(liveSlots[0].Properties?.Filters, filtersCollection)",
            "Filters wrapper replacement during effective proof must fail closed");
        Require(source, "ReferenceEquals(liveGroups[0], filterGroup)",
            "filter-group replacement during effective proof must fail closed");
        Require(source, "ReferenceEquals(liveGroups[0].Filter, hostFilter)",
            "included-filter replacement during effective proof must fail closed");

        int capture = source.IndexOf("var hostFilter = filterGroup.Filter;", StringComparison.Ordinal);
        int committedBefore = source.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", capture, StringComparison.Ordinal);
        int exclusionBefore = source.IndexOf("var excludedBefore = SnapshotOptionalExcludedFilter(filterGroup);", committedBefore, StringComparison.Ordinal);
        int effective = source.IndexOf("RequireEffectiveAcceptance(hostFilter, excludedBefore);", exclusionBefore, StringComparison.Ordinal);
        int exclusionAfter = source.IndexOf("var excludedAfter = SnapshotOptionalExcludedFilter(filterGroup);", effective, StringComparison.Ordinal);
        int exclusionStability = source.IndexOf("OptionalFilterSetEquals(excludedBefore, excludedAfter)", exclusionAfter, StringComparison.Ordinal);
        int inventoryReproof = source.IndexOf("ReferenceEquals(liveInventory, inventory)", exclusionStability, StringComparison.Ordinal);
        int groupReproof = source.IndexOf("ReferenceEquals(liveGroups[0], filterGroup)", inventoryReproof, StringComparison.Ordinal);
        int filterReproof = source.IndexOf("ReferenceEquals(liveGroups[0].Filter, hostFilter)", groupReproof, StringComparison.Ordinal);
        int committedAfter = source.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", filterReproof, StringComparison.Ordinal);

        if (capture < 0 || committedBefore <= capture || exclusionBefore <= committedBefore
            || effective <= exclusionBefore || exclusionAfter <= effective
            || exclusionStability <= exclusionAfter || inventoryReproof <= exclusionStability
            || groupReproof <= inventoryReproof || filterReproof <= groupReproof
            || committedAfter <= filterReproof)
            throw new InvalidOperationException(
                "Dogtag effective-host point-in-time regression failed: required capture -> committed -> exclusion snapshot -> effective proof -> exclusion re-snapshot -> full identity reproof -> committed sequence changed.");

        if (source.Contains("RequireEffectiveAcceptance(groups[0].Filter, ReadOptionalExcludedFilter(groups[0]))", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Dogtag effective-host point-in-time regression failed: one-shot live lookup/evaluation path was restored.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseHostExclusionGuard.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseHostExclusionGuard.cs"))) return nested;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseHostExclusionGuard.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseHostExclusionGuard.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag effective-host point-in-time regression failed: " + message + ".");
    }
}