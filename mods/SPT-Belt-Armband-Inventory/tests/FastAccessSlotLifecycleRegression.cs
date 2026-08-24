using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class FastAccessSlotLifecycleRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        object installed = new object();
        object replacement = new object();

        Assert(FastAccessSlotPolicy.ShouldRestoreReference(installed, installed), "owned installed static array can be restored");
        Assert(!FastAccessSlotPolicy.ShouldRestoreReference(replacement, installed), "later third-party static array replacement is preserved");
        Assert(!FastAccessSlotPolicy.ShouldRestoreReference(null, installed), "missing current static array is not overwritten during cleanup");
        Assert(!FastAccessSlotPolicy.ShouldRestoreReference(installed, null), "missing installed reference never claims ownership");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FastAccessSlot lifecycle regression failed: " + message);
    }
}
