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
        string slots = File.ReadAllText(Path.Combine(root, "src", "DedicatedEquipmentSlotPatches.cs"));
        string projection = File.ReadAllText(Path.Combine(root, "src", "BeltContainersPanelProjectionPatches.cs"));
        Require(source.Contains("EFT.UI.ContainersPanel", StringComparison.Ordinal), "must bind the actual owner of special slots");
        Require(source.Contains("EFT.UI.DragAndDrop.SearchableSlotView", StringComparison.Ordinal), "must resolve the actual native special-slot owner");
        Require(source.Contains("_specSlotsPanel", StringComparison.Ordinal) && source.Contains("specialPanel", StringComparison.Ordinal), "must anchor below the exact native special-slot panel");
        Require(source.Contains("ignoreLayout", StringComparison.Ordinal), "overlay and panels must be excluded from automatic layout");
        Require(slots.Contains("BuildWearableOrder", StringComparison.Ordinal) && slots.Contains("HeadBandSlotKey", StringComparison.Ordinal) && slots.Contains("ArmBand", StringComparison.Ordinal), "native ContainersPanel order must create both accessory rows");
        Require(projection.Contains("IsProjectedWearable", StringComparison.Ordinal), "native row factory must accept Belt, HeadBand and ArmBand");
        Require(!source.Contains("GeneratedGridsView", StringComparison.Ordinal) && !source.Contains("Instantiate", StringComparison.Ordinal), "must not create detached decorative grid windows");
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
