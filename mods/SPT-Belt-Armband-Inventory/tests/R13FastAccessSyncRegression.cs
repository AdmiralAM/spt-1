using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class R13FastAccessSyncRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Assert(FastAccessBeltSyncPolicy.ShouldQueue(true, true, true, true), "successful loaded ArmBand equip/remove queues refresh");
        Assert(!FastAccessBeltSyncPolicy.ShouldQueue(false, true, true, true), "failed inventory event cannot refresh fast access");
        Assert(!FastAccessBeltSyncPolicy.ShouldQueue(true, false, true, true), "foreign-owner inventory event cannot refresh fast access");
        Assert(!FastAccessBeltSyncPolicy.ShouldQueue(true, true, false, true), "non-ArmBand compound event remains vanilla");
        Assert(!FastAccessBeltSyncPolicy.ShouldQueue(true, true, true, false), "plain armband event does not refresh grenade fast access");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("R13 regression failed: " + message);
    }
}
