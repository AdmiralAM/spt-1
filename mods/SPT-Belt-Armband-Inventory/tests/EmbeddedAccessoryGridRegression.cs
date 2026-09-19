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
        Require(source.Contains("GetComponentInParent(EquipmentTabType)", StringComparison.Ordinal), "stash placement must not mutate the left character EquipmentTab");
        Require(source.Contains("HeadBandRightOffset", StringComparison.Ordinal) && source.Contains("HeadBandDownOffset", StringComparison.Ordinal), "HeadBand must use explicit small right/down polish offsets");
        Require(source.Contains("BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic", StringComparison.Ordinal), "public EFT layout fields must be discoverable at runtime");
        Require(source.Contains("ignoreLayout", StringComparison.Ordinal), "overlay and panels must be excluded from automatic layout");
        Require(source.Contains("_slotPlace", StringComparison.Ordinal) && source.Contains("SetActive(false)", StringComparison.Ordinal), "compact panels must hide the duplicate equipped-item card");
        Require(source.Contains("WaitForEndOfFrame", StringComparison.Ordinal) && source.Contains("ForceRebuildLayoutImmediate", StringComparison.Ordinal), "compact panels must move after native layout without extending the container column");
        Require(source.Contains("_gridsContainer", StringComparison.Ordinal) && source.Contains("CompactNativeRow", StringComparison.Ordinal), "panel size must follow the rendered native grid instead of the hidden item card");
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
