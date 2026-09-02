using System;
using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadDiagnosticLoggingRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        ThrowingReachabilityLoggerCannotEscape();
        ThrowingCandidateLoggerCannotEscape();
    }

    static void ThrowingReachabilityLoggerCannotEscape()
    {
        FastAccessReloadRuntime.Reset();
        FastAccessReloadRuntime.ItemType = typeof(FakeItem);
        FastAccessReloadRuntime.MagazineType = typeof(FakeMagazine);
        FastAccessReloadRuntime.GetAllParentItems = _ => throw new InvalidOperationException("synthetic reachability failure");
        FastAccessReloadRuntime.ReadTemplateId = _ => "unused";
        FastAccessReloadRuntime.LogWarning = _ => throw new InvalidOperationException("synthetic logger failure");

        bool reachable = false;
        FastAccessReloadRuntime.PromoteReachability(new FakeMagazine(), ref reachable);
        Assert(!reachable, "reachability failure remains vanilla false even when warning sink throws");

        FastAccessReloadRuntime.PromoteReachability(new FakeMagazine(), ref reachable);
        Assert(!reachable, "repeat reachability failure cannot escape after diagnostic suppression");
        FastAccessReloadRuntime.Reset();
    }

    static void ThrowingCandidateLoggerCannotEscape()
    {
        object slots = new object[] { "original-fast" };
        var vanilla = new FakeItem[] { new FakeMagazine() };
        var inventory = new FakeInventory();

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload diagnostic logging regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = new object[] { "original-fast", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = new object[] { "original-bind" };
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = new object[] { "original-bind", RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = _ => throw new InvalidOperationException("synthetic candidate failure");
        ReloadCandidateBridgeRuntime.ReadTemplateId = _ => "unused";
        ReloadCandidateBridgeRuntime.LogWarning = _ => throw new InvalidOperationException("synthetic logger failure");
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();

        FieldInfo depth = typeof(ReloadCandidateBridgeRuntime).GetField("reloadDepth", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload diagnostic logging regression failed: reloadDepth state field missing");
        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload diagnostic logging regression failed: reentrant state field missing");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object first = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        Assert(ReferenceEquals(first, vanilla),
            "candidate bridge returns exact vanilla result when candidate inspection and warning sink both fail");
        Assert((int)depth.GetValue(null)! == 1,
            "candidate failure cannot consume the active Reload/QuickReload scope owned by the Harmony finalizer");
        Assert(!(bool)reentrant.GetValue(null)!,
            "candidate failure plus logger failure cannot leak reentrant state while reload scope is still active");
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert((int)depth.GetValue(null)! == 0,
            "finalizer remains the sole owner of reload-scope unwind after a candidate failure");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object second = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        Assert(ReferenceEquals(second, vanilla),
            "repeat candidate failure remains contained after first diagnostic attempt");
        Assert((int)depth.GetValue(null)! == 1 && !(bool)reentrant.GetValue(null)!,
            "repeat failure preserves scope depth and clears reentrancy after diagnostic suppression");
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);

        Assert((int)depth.GetValue(null)! == 0 && !(bool)reentrant.GetValue(null)!,
            "candidate failure plus logger failure cannot leak reload or reentrant state into future vanilla calls");
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    sealed class FakeInventory
    {
        public FakeItem[] GetItemsInSlots(object slots)
        {
            return new FakeItem[] { new FakeMagazine() };
        }
    }

    class FakeItem
    {
    }

    sealed class FakeMagazine : FakeItem
    {
    }

    static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Reload diagnostic logging regression failed: " + message);
    }
}
