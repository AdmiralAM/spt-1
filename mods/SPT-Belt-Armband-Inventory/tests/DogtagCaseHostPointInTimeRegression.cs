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

        string path = Path.Combine(root, "server", "DogtagCaseHostContract.cs");
        string source = File.ReadAllText(path);

        Require(source, "RequirePreservedSnapshot(SnapshotCurrentFilter(currentFilter))",
            "preserved verification must consume a detached current-host snapshot");
        Require(source, "HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);",
            "committed verification must capture one current-host point in time");
        Require(source, "RequirePreservedSnapshot(current);",
            "committed preservation and exact-case checks must share the same snapshot");
        Require(source, "if (!current.Contains(caseTpl))",
            "exact-case presence must be checked against the committed snapshot, not the live mutable filter");

        if (source.Contains("RequirePreserved(currentFilter);\n\n        var caseTpl", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag host point-in-time regression failed: TOCTOU live-filter committed verification was restored.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "server", "DogtagCaseHostContract.cs");
            if (File.Exists(candidate)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseHostContract.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseHostContract.cs"))) return nested;
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
