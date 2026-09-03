using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ProtectionRootPublicationIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Protection-root publication isolation regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "WearableProtectionRuntime.cs"));
        string normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);

        Require(normalized, "ProtectedWearableRoot[] published = Volatile.Read(ref activeRoots);",
            "ActiveRoots must capture the published array exactly once");
        Require(normalized, "return (ProtectedWearableRoot[])published.Clone();",
            "ActiveRoots must return a detached snapshot instead of shared mutable authority");

        int rootsStart = normalized.IndexOf("private static readonly ProtectedWearableRoot[] ArmBandRoots", StringComparison.Ordinal);
        int snapshot = normalized.IndexOf("internal static WearableProtectionSnapshot Snapshot()", StringComparison.Ordinal);
        if (rootsStart < 0 || snapshot <= rootsStart)
            throw new InvalidOperationException("Protection-root publication isolation regression failed: root registry boundary is missing.");

        string registry = normalized.Substring(rootsStart, snapshot - rootsStart);
        if (registry.Contains("DogtagCaseItemId", StringComparison.Ordinal)
            || registry.Contains("DogtagCaseItem.TemplateId", StringComparison.Ordinal)
            || registry.Contains("\"Dogtag\"", StringComparison.Ordinal))
            throw new InvalidOperationException("Protection-root publication isolation regression failed: Dogtag Case must remain outside death/insurance protection roots.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "WearableProtectionRuntime.cs")))
                return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "WearableProtectionRuntime.cs")))
                return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Protection-root publication isolation regression failed: " + message + ".");
    }
}
