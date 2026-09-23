using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class FastAccessSlotLifecycleRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        object installedFast = new object();
        object installedBind = new object();
        object replacement = new object();

        Assert(FastAccessSlotPolicy.ShouldRestoreReference(installedFast, installedFast), "owned installed static array can be restored");
        Assert(!FastAccessSlotPolicy.ShouldRestoreReference(replacement, installedFast), "later third-party static array replacement is preserved");
        Assert(!FastAccessSlotPolicy.ShouldRestoreReference(null, installedFast), "missing current static array is not overwritten during cleanup");
        Assert(!FastAccessSlotPolicy.ShouldRestoreReference(installedFast, null), "missing installed reference never claims ownership");

        Assert(FastAccessSlotPolicy.HasExactInstalledArrayAuthority(installedFast, installedFast, installedBind, installedBind),
            "repeat install is idempotent only while both live arrays retain the exact installed references");
        Assert(!FastAccessSlotPolicy.HasExactInstalledArrayAuthority(replacement, installedFast, installedBind, installedBind),
            "FastAccessSlots reference drift refuses repeat installation");
        Assert(!FastAccessSlotPolicy.HasExactInstalledArrayAuthority(installedFast, installedFast, replacement, installedBind),
            "BindAvailableSlotsExtended reference drift refuses repeat installation");
        Assert(!FastAccessSlotPolicy.HasExactInstalledArrayAuthority(installedFast, null, installedBind, installedBind),
            "missing prior FastAccessSlots authority refuses repeat installation");
        Assert(!FastAccessSlotPolicy.HasExactInstalledArrayAuthority(installedFast, installedFast, installedBind, null),
            "missing prior BindAvailableSlotsExtended authority refuses repeat installation");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FastAccessSlot lifecycle regression failed: " + message);
    }
}
