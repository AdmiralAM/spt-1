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

        if (contract.Contains("RequirePreserved(currentFilter);\n\n        var caseTpl", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: TOCTOU live-filter committed verification was restored.");

        Require(assort, "DogtagCaseHostContract.RequireCommitted(hostFilter);",
            "Ragman publication must consume the centralized committed host proof");
        Require(assort, "requested template identity is not the exact Dogtag Case product",
            "Ragman publication must retain exact product-template identity after centralizing host verification");
        if (assort.Contains("hostFilter.Contains(templateId)", StringComparison.Ordinal)
            || assort.Contains("hostFilter.Any(x => !Equals(x, templateId))", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: assort publication re-read the mutable host after committed verification.");
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
