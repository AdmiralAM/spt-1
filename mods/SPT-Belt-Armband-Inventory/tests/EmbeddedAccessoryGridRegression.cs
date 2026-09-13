using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class EmbeddedAccessoryGridRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string root = FindModuleRoot();
        string source = File.ReadAllText(Path.Combine(root, "src", "EmbeddedAccessoryGridPatches.cs"));
        string plugin = File.ReadAllText(Path.Combine(root, "src", "Plugin.cs"));
        Require(source.Contains("EFT.UI.DragAndDrop.GeneratedGridsView", StringComparison.Ordinal), "must use the native EFT generated-grid view");
        Require(source.Contains("DedicatedHeadBandEquipmentSlotValue, 0", StringComparison.Ordinal), "HeadBand must remain above ArmBand");
        Require(source.Contains("Enum.Parse(EquipmentSlotType, \"ArmBand\"", StringComparison.Ordinal), "must bind the native ArmBand equipment slot");
        Require(source.Contains("SpecialSlot", StringComparison.Ordinal) && source.Contains("firstSpecial", StringComparison.Ordinal), "must anchor below the native special-slot row");
        Require(source.Contains("ignoreLayout", StringComparison.Ordinal), "overlay and panels must be excluded from automatic layout");
        Require(source.Contains("EmbeddedAccessoryGridLifetime", StringComparison.Ordinal) && source.Contains("OnDisable()", StringComparison.Ordinal), "view ownership must follow the EquipmentTab lifetime");
        Require(!source.Contains("ClosePrefix", StringComparison.Ordinal), "must not patch the inherited global UI close method");
        Require(!source.Contains("Update(", StringComparison.Ordinal) && !source.Contains("FindObjectsOfType", StringComparison.Ordinal), "embedded panels must not poll or scan the scene");
        Require(plugin.Contains("new EmbeddedAccessoryGridPatches", StringComparison.Ordinal), "production plugin must install the embedded-grid owner");
        Require(plugin.Contains("embeddedAccessoryGridPatches.Dispose()", StringComparison.Ordinal), "production plugin must release the embedded-grid owner");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Embedded accessory grid regression failed: " + message + ".");
    }

    static string FindModuleRoot()
    {
        DirectoryInfo current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "Plugin.cs"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate B&A&HB module root.");
    }
}
