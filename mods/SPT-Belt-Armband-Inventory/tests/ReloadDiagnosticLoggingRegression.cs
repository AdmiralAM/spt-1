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

        // failureLogged suppresses repeat diagnostics; a second call must also remain contained.
        FastAccessReloadRuntime.PromoteReachability(new FakeMagazine(), ref reachable);
        Assert(!reachable, "repeat reachability failure cannot escape after diagnostic suppression");
        FastAccessReloadRuntime.Reset();
    }

    static void ThrowingCandidateLoggerCannotEscape()
    {
        var slots = new object();
        var vanilla = new FakeItem[] { new FakeMagazine() };
        var inventory = new FakeInventory();

        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload diagnostic logging regression failed: fake GetItemsInSlots missing");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new object();
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = _ => throw new InvalidOperationException("synthetic candidate failure");
        ReloadCandidateBridgeRuntime.ReadTemplateId = _ => "unused";
        ReloadCandidateBridgeRuntime.LogWarning = _ => throw new InvalidOperationException("synthetic logger failure");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object first = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(first, vanilla),
            "candidate bridge returns exact vanilla result when candidate inspection and warning sink both fail");

        ReloadCandidateBridgeRuntime.EnterReloadScope();
        object second = ReloadCandidateBridgeRuntime.AppendCandidates(inventory, slots, vanilla);
        ReloadCandidateBridgeRuntime.ExitReloadScope(null);
        Assert(ReferenceEquals(second, vanilla),
            "repeat candidate failure remains contained after first diagnostic attempt");

        FieldInfo reentrant = typeof(ReloadCandidateBridgeRuntime).GetField("reentrant", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload diagnostic logging regression failed: reentrant state field missing");
        Assert(!(bool)reentrant.GetValue(null)!, "candidate failure plus logger failure cannot leak reentrant state");
        ReloadCandidateBridgeRuntime.Reset();
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
