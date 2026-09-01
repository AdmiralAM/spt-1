using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class ReloadPseudoSlotIdentityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Reload pseudo-slot identity regression failed: module root could not be resolved.");

        string path = Path.Combine(root, "src", "FastAccessSlotPatches.cs");
        string source = File.ReadAllText(path);

        Require(source,
            "Enum.ToObject(slotEnumType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue)",
            "dedicated Belt reload identity must remain an integer-backed EquipmentSlot pseudo-value");
        Require(source,
            "TryInstallReloadCandidateBridge(inventoryType, slotEnumType, dedicatedBelt)",
            "the exact pseudo-slot object must flow into bounded reload candidate bridge installation");
        Require(source,
            "ReloadCandidateBridgeRuntime.BeltSlotsArgument",
            "reload candidate runtime must retain a dedicated pseudo-slot query argument");

        if (source.Contains("Enum.IsDefined", StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot identity regression failed: declared-enum gating would reject dedicated slot15 before the integer-indexed SPT slot boundary.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs");
            if (File.Exists(candidate)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "FastAccessSlotPatches.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Reload pseudo-slot identity regression failed: " + message + ".");
    }
}
