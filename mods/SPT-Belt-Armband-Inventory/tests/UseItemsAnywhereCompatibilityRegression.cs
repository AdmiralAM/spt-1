using System;
using System.IO;

namespace SPTBeltArmbandInventory.Tests;

internal static class UseItemsAnywhereCompatibilityRegression
{
    internal static void Run()
    {
        string root = FindModuleRoot();
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        Require(!File.Exists(Path.Combine(root, "src", "UseItemsAnywhereCompatibility.cs")), "Belt must not ship a second Use Items Anywhere adapter");
        Require(!plugin.Contains("com.cj.useFromAnywhere", StringComparison.Ordinal), "foreign Use Items Anywhere ownership must be absent from Belt");
        Require(!plugin.Contains("UseItemsAnywhereCompatibility", StringComparison.Ordinal), "Belt plugin must not detect, install or dispose the Suite-owned adapter");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Use Items Anywhere compatibility regression failed: " + message + ".");
    }

    static string FindModuleRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "Plugin.cs"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate B&A&HB module root.");
    }
}
