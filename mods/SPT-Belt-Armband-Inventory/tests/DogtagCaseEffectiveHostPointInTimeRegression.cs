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
        Require(source, "var hostFilter = filterGroup.Filter",
            "effective proof must capture the exact included-filter HashSet");
        Require(source, "DogtagCaseHostContract.RequireCommitted(hostFilter);",
            "effective proof must bracket optional-exclusion evaluation with committed included-host proofs");
        Require(source, "var excludedBefore = SnapshotOptionalExcludedFilter(filterGroup);",
            "optional future exclusions must be detached before effective evaluation");
        Require(source, "RequireEffectiveAcceptance(hostFilter, excludedBefore);",
            "effective acceptance must consume the captured included filter and detached exclusion snapshot");
        Require(source, "var excludedAfter = SnapshotOptionalExcludedFilter(filterGroup);",
            "optional future exclusions must be re-snapshotted before wrapper-chain reproof");
        Require(source, "OptionalFilterSetEquals(excludedBefore, excludedAfter)",
            "in-place optional exclusion drift during effective evaluation must fail closed");
        Require(source, "RequireCapturedHostIdentity(templateTable, inventory, inventoryProperties, slotsCollection, slot, slotProperties, filtersCollection, filterGroup, hostFilter);",
            "effective proof must use the exact captured wrapper chain before and after final exclusion evaluation");
        Require(source, "ReferenceEquals(liveInventory, inventory)",
            "captured-host helper must reject DefaultInventory replacement");
        Require(source, "ReferenceEquals(liveInventory.Properties, inventoryProperties)",
            "captured-host helper must reject DefaultInventory properties replacement");
        Require(source, "ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)",
            "captured-host helper must reject Slots wrapper replacement");
        Require(source, "ReferenceEquals(liveSlots[0], slot)",
            "captured-host helper must reject Dogtag slot replacement");
        Require(source, "ReferenceEquals(liveSlots[0].Properties, slotProperties)",
            "captured-host helper must reject Dogtag slot properties replacement");
        Require(source, "ReferenceEquals(liveSlots[0].Properties?.Filters, filtersCollection)",
            "captured-host helper must reject Filters wrapper replacement");
        Require(source, "ReferenceEquals(liveGroups[0], filterGroup)",
            "captured-host helper must reject filter-group replacement");
        Require(source, "ReferenceEquals(liveGroups[0].Filter, hostFilter)",
            "captured-host helper must reject included-filter replacement");
        Require(source, "var excludedFinal = SnapshotOptionalExcludedFilter(filterGroup);",
            "optional exclusions must be snapshotted again after the first full wrapper-chain reproof");
        Require(source, "OptionalFilterSetEquals(excludedBefore, excludedFinal)",
            "optional exclusion drift during the wrapper-chain proof must fail closed");
        Require(source, "RequireEffectiveAcceptance(hostFilter, excludedFinal);",
            "final detached exclusion snapshot must receive effective acceptance");

        const string identityCall = "RequireCapturedHostIdentity(templateTable, inventory, inventoryProperties, slotsCollection, slot, slotProperties, filtersCollection, filterGroup, hostFilter);";
        int capture = source.IndexOf("var hostFilter = filterGroup.Filter", StringComparison.Ordinal);
        int committedBefore = source.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", capture, StringComparison.Ordinal);
        int exclusionBefore = source.IndexOf("var excludedBefore = SnapshotOptionalExcludedFilter(filterGroup);", committedBefore, StringComparison.Ordinal);
        int effective = source.IndexOf("RequireEffectiveAcceptance(hostFilter, excludedBefore);", exclusionBefore, StringComparison.Ordinal);
        int exclusionAfter = source.IndexOf("var excludedAfter = SnapshotOptionalExcludedFilter(filterGroup);", effective, StringComparison.Ordinal);
        int exclusionStability = source.IndexOf("OptionalFilterSetEquals(excludedBefore, excludedAfter)", exclusionAfter, StringComparison.Ordinal);
        int firstIdentity = source.IndexOf(identityCall, exclusionStability, StringComparison.Ordinal);
        int committedAfter = source.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", firstIdentity, StringComparison.Ordinal);
        int exclusionFinal = source.IndexOf("var excludedFinal = SnapshotOptionalExcludedFilter(filterGroup);", committedAfter, StringComparison.Ordinal);
        int finalStability = source.IndexOf("OptionalFilterSetEquals(excludedBefore, excludedFinal)", exclusionFinal, StringComparison.Ordinal);
        int finalEffective = source.IndexOf("RequireEffectiveAcceptance(hostFilter, excludedFinal);", finalStability, StringComparison.Ordinal);
        int finalIdentity = source.IndexOf(identityCall, firstIdentity + identityCall.Length, StringComparison.Ordinal);
        int finalCommitted = source.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", finalIdentity, StringComparison.Ordinal);

        if (capture < 0 || committedBefore <= capture || exclusionBefore <= committedBefore
            || effective <= exclusionBefore || exclusionAfter <= effective
            || exclusionStability <= exclusionAfter || firstIdentity <= exclusionStability
            || committedAfter <= firstIdentity || exclusionFinal <= committedAfter
            || finalStability <= exclusionFinal || finalEffective <= finalStability
            || finalIdentity <= finalEffective || finalCommitted <= finalIdentity)
            throw new InvalidOperationException(
                "Dogtag effective-host point-in-time regression failed: required capture -> committed -> exclusion snapshot/effective/stability -> identity -> committed -> final exclusion snapshot/stability/effective -> final identity/committed sequence changed.");

        if (source.IndexOf(identityCall, finalIdentity + identityCall.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException(
                "Dogtag effective-host point-in-time regression failed: captured-host identity proof count drifted from the exact two bounded publication reproofs.");

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
