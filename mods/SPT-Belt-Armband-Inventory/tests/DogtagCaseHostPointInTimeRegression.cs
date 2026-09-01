using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseHostPointInTimeRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: module root could not be resolved.");

        string contractPath = Path.Combine(root, "server", "DogtagCaseHostContract.cs");
        string assortPath = Path.Combine(root, "server", "DogtagCaseAssort.cs");
        string contract = File.ReadAllText(contractPath);
        string assort = File.ReadAllText(assortPath);

        Require(contract, "RequirePreservedSnapshot(SnapshotCurrentFilter(currentFilter))",
            "preserved verification must consume a detached current-host snapshot");
        Require(contract, "HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);",
            "committed verification must capture one current-host point in time");
        Require(contract, "RequirePreservedSnapshot(current);",
            "committed preservation and exact-case checks must share the same snapshot");
        Require(contract, "if (!current.Contains(caseTpl))",
            "exact-case presence must be checked against the committed snapshot, not the live mutable filter");
        Require(contract, "HashSet<MongoId> liveAfterProof = SnapshotCurrentFilter(currentFilter);",
            "committed verification must re-snapshot the same live filter after the preservation/exact-case proof");
        Require(contract, "if (!current.SetEquals(liveAfterProof))",
            "in-place Dogtag filter drift during committed-host verification must fail closed even when reference identity is unchanged");
        Require(contract, "live Dogtag filter changed during committed-host verification",
            "committed-host drift must remain explicitly diagnosable");

        int firstSnapshot = contract.IndexOf("HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);", StringComparison.Ordinal);
        int preserved = contract.IndexOf("RequirePreservedSnapshot(current);", firstSnapshot, StringComparison.Ordinal);
        int exactCase = contract.IndexOf("if (!current.Contains(caseTpl))", preserved, StringComparison.Ordinal);
        int secondSnapshot = contract.IndexOf("HashSet<MongoId> liveAfterProof = SnapshotCurrentFilter(currentFilter);", exactCase, StringComparison.Ordinal);
        int stabilityProof = contract.IndexOf("if (!current.SetEquals(liveAfterProof))", secondSnapshot, StringComparison.Ordinal);
        if (firstSnapshot < 0 || preserved <= firstSnapshot || exactCase <= preserved
            || secondSnapshot <= exactCase || stabilityProof <= secondSnapshot)
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: committed proof must remain snapshot -> preservation -> exact case -> live re-snapshot -> stability proof.");

        if (contract.Contains("RequirePreserved(currentFilter);\n\n        var caseTpl", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: TOCTOU live-filter committed verification was restored.");

        Require(assort, "DogtagCaseHostContract.RequireCommitted(hostFilter);",
            "Ragman publication must consume the centralized committed host proof");
        Require(assort, "ReferenceEquals(liveInventory, inventory)",
            "publication must prove the verified DefaultInventory object is still installed after committed-host verification");
        Require(assort, "ReferenceEquals(liveSlots[0], slot)",
            "publication must prove the verified Dogtag slot object is still installed after committed-host verification");
        Require(assort, "ReferenceEquals(liveGroups[0], groups[0])",
            "publication must prove the verified sole Dogtag filter-group object is still installed after committed-host verification");
        Require(assort, "ReferenceEquals(liveGroups[0].Filter, hostFilter)",
            "publication must prove the verified Dogtag filter set is still installed after committed-host verification");
        Require(assort, "live DefaultInventory template changed during committed-host verification",
            "whole DefaultInventory replacement must fail closed at publication");
        Require(assort, "live Dogtag slot changed during committed-host verification",
            "slot replacement must fail closed at publication");
        Require(assort, "live Dogtag filter group/filter changed during committed-host verification",
            "filter-group or filter-set replacement must fail closed at publication");
        Require(assort, "requested template identity is not the exact Dogtag Case product",
            "Ragman publication must retain exact product-template identity");

        int committed = assort.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", StringComparison.Ordinal);
        int inventoryReproof = assort.IndexOf("ReferenceEquals(liveInventory, inventory)", committed, StringComparison.Ordinal);
        int slotReproof = assort.IndexOf("ReferenceEquals(liveSlots[0], slot)", inventoryReproof, StringComparison.Ordinal);
        int groupReproof = assort.IndexOf("ReferenceEquals(liveGroups[0], groups[0])", slotReproof, StringComparison.Ordinal);
        int filterReproof = assort.IndexOf("ReferenceEquals(liveGroups[0].Filter, hostFilter)", groupReproof, StringComparison.Ordinal);
        if (committed < 0 || inventoryReproof <= committed || slotReproof <= inventoryReproof
            || groupReproof <= slotReproof || filterReproof <= groupReproof)
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: live host identity reproof must follow committed snapshot proof in inventory/slot/group/filter order.");

        if (assort.Contains("hostFilter.Contains(templateId)", StringComparison.Ordinal)
            || assort.Contains("hostFilter.Any(x => !Equals(x, templateId))", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: assort publication restored value-based live-filter rechecks instead of reference identity reproof.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string contract = Path.Combine(current.FullName, "server", "DogtagCaseHostContract.cs");
            string assort = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(contract) && File.Exists(assort)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string directContract = Path.Combine(current.FullName, "server", "DogtagCaseHostContract.cs");
            string directAssort = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(directContract) && File.Exists(directAssort)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseHostContract.cs"))
                && File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: " + message + ".");
    }
}