using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseCapturedBaselineIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag captured-baseline isolation regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseHostContract.cs"));

        Require(source, "var snapshot = acceptedTemplates.ToHashSet();",
            "preload capture must materialize an owned point-in-time HashSet rather than retain caller-owned mutable state");
        Require(source, "capturedVanillaEntries = snapshot;",
            "the retained baseline must be the owned snapshot created inside CaptureVanillaEntries");
        Require(source, "captured = capturedVanillaEntries.ToArray();",
            "verification must also take a bounded point-in-time copy under the snapshot lock");

        int snapshot = source.IndexOf("var snapshot = acceptedTemplates.ToHashSet();", StringComparison.Ordinal);
        int lockStart = source.IndexOf("lock (SnapshotSync)", snapshot, StringComparison.Ordinal);
        int publish = source.IndexOf("capturedVanillaEntries = snapshot;", lockStart, StringComparison.Ordinal);
        if (snapshot < 0 || lockStart < 0 || publish < 0 || !(snapshot < lockStart && lockStart < publish))
            throw new InvalidOperationException("Dogtag captured-baseline isolation regression failed: owned snapshot must be created before synchronized publication.");

        if (source.Contains("capturedVanillaEntries = acceptedTemplates", StringComparison.Ordinal)
            || source.Contains("capturedVanillaEntries = currentFilter", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag captured-baseline isolation regression failed: caller-owned mutable collection aliasing is forbidden.");
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
            throw new InvalidOperationException("Dogtag captured-baseline isolation regression failed: " + message + ".");
    }
}
