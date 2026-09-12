using System;
using System.IO;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class EquipmentCacheCapacityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        if (EquipmentCacheCapacityPolicy.RequiredCapacity(15, 16) != 17)
            throw new InvalidOperationException("Sparse slot16 must allocate a 17-entry InventoryEquipment cache.");
        if (EquipmentCacheCapacityPolicy.RequiredCapacity(20, 16) != 20)
            throw new InvalidOperationException("Existing larger foreign capacity must be preserved.");

        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "src", "EquipmentCacheCapacityPatches.cs"));
        if (!source.Contains("allocationMatches != 1", StringComparison.Ordinal)
            || !source.Contains("OpCodes.Ldlen", StringComparison.Ordinal)
            || !source.Contains("OpCodes.Newarr", StringComparison.Ordinal)
            || !source.Contains("denseEnumerationMatches != 1", StringComparison.Ordinal)
            || !source.Contains("code[i].operand = TargetSlotsField", StringComparison.Ordinal)
            || !source.Contains("harmony?.UnpatchSelf()", StringComparison.Ordinal))
            throw new InvalidOperationException("Equipment cache transpiler lost exact-shape or rollback proof.");
    }
}
