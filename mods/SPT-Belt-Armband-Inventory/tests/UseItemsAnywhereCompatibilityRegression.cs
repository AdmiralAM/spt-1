using System;
using System.IO;

namespace SPTBeltArmbandInventory.Tests;

internal static class UseItemsAnywhereCompatibilityRegression
{
    internal static void Run()
    {
        string root = FindModuleRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "UseItemsAnywhereCompatibility.cs"));
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));

        Require(source.Contains("com.cj.useFromAnywhere", StringComparison.Ordinal), "exact foreign GUID must own detection");
        Require(source.Contains("!Contains(list, armBand) || Contains(list, belt)", StringComparison.Ordinal), "slot15 must follow only lists where ArmBand is enabled");
        Require(source.Contains("list.Add(belt)", StringComparison.Ordinal), "runtime list must receive pseudo-slot15");
        Require(source.Contains("entry.BoxedValue = list", StringComparison.Ordinal), "extended lists must be published through the owning config entry");
        Require(plugin.Contains("BepInDependency(UseItemsAnywhereCompatibility.PluginGuid", StringComparison.Ordinal), "B&A must load after the optional foreign owner");
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
