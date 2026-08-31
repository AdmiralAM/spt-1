using System;
using System.Reflection;
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
        Assert(FastAccessSlotPolicy.ShouldReuseVanillaReloadCandidates(false),
            "no exact Belt fallback keeps the original vanilla result object");
        Assert(!FastAccessSlotPolicy.ShouldReuseVanillaReloadCandidates(true),
            "a real exact Belt fallback is the only reason to allocate a merged result");

        // Harmony prefixes/finalizers can nest if EFT routes one reload entry point
        // through another. The ThreadStatic scope must therefore be depth-counted,
        // preserve the original exception, and never underflow after an extra unwind.
        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate bridge regression failed: reloadDepth state field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload candidate bridge regression failed: reentrant state field missing");

        ReloadCandidateBridgeRuntime.Reset();
        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "reset clears per-thread reload scope/reentrancy state");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        ReloadCandidateBridgeRuntime.EnterReloadScope();
        Assert((int)depth.GetValue(null)! == 2,
            "nested Reload/QuickReload entry increments scoped depth instead of collapsing to a boolean");

        var original = new InvalidOperationException("sentinel");
        Exception returned = ReloadCandidateBridgeRuntime.ExitReloadScope(original);
        Assert(ReferenceEquals(original, returned) && (int)depth.GetValue(null)! == 1,
            "finalizer preserves the original exception while unwinding exactly one nested scope");

        returned = ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(returned == null && (int)depth.GetValue(null)! == 0,
            "outer finalizer returns to the vanilla non-reload boundary");

        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert((int)depth.GetValue(null)! == 0,
            "defensive extra unwind cannot make reload scope negative or leak future bridging");

        ReloadCandidateBridgeRuntime.Reset();
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reload candidate bridge regression failed: " + message);
    }
}
