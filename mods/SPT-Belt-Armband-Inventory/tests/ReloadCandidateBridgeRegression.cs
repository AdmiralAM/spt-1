using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateBridgeRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        Assert(FastAccessSlotPolicy.ShouldBridgeReloadCandidates(true, false, true),
            "exact fast-access candidate enumeration is bridged only inside reload scope");
        Assert(!FastAccessSlotPolicy.ShouldBridgeReloadCandidates(false, false, true),
            "non-reload callers keep vanilla candidate enumeration");
        Assert(!FastAccessSlotPolicy.ShouldBridgeReloadCandidates(true, true, true),
            "bridge invocation is reentrancy-safe");
        Assert(!FastAccessSlotPolicy.ShouldBridgeReloadCandidates(true, false, false),
            "unrelated slot enumerations are not widened");
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reload candidate bridge regression failed: " + message);
    }
}
