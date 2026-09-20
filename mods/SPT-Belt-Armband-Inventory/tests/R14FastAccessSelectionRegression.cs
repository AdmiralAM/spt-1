using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class R14FastAccessSelectionRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Assert(FastAccessBeltSyncPolicy.ShouldClearSelected(true, true), "removed belt clears a selected grenade that belonged to it");
        Assert(!FastAccessBeltSyncPolicy.ShouldClearSelected(false, true), "belt equip never clears current grenade selection");
        Assert(!FastAccessBeltSyncPolicy.ShouldClearSelected(true, false), "removing belt preserves selection from vest/pockets");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("R14 regression failed: " + message);
    }
}
