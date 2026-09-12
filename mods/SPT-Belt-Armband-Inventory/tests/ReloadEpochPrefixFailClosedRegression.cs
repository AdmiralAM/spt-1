using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadEpochPrefixFailClosedRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        int[] slots = { 1, 2, 3 };
        IEnumerable<FakeItem> vanilla = new FakeItem[] { new FakeItem() };
        MethodInfo beforeAppend = typeof(ReloadScopeEpochGuard).GetMethod(
            "BeforeAppend",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: BeforeAppend callback missing.");

        ReloadScopeEpochGuard.ResetStateForRegression();
        Configure(slots);
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
        ReloadScopeEpochGuard.EnterForRegression();

        // A process-wide contract drift must be rejected by the actual Harmony
        // prefix callback, not just by its helper. The prefix must install the
        // exact vanilla result reference and suppress AppendCandidates entirely.
        ReloadCandidateBridgeRuntime.ReturnType = typeof(object[]);
        object?[] driftArgs = { slots, vanilla, null };
        bool driftProceed = (bool)(beforeAppend.Invoke(null, driftArgs)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: drift callback returned null."));
        Assert(!driftProceed, "drifted runtime contract must suppress the bridge callback");
        Assert(ReferenceEquals(driftArgs[2], vanilla),
            "drifted runtime contract must publish the exact vanilla result object");

        // A valid pinned SPT 4.x generic-interface contract inside the current
        // generation must proceed and must not pre-write Harmony's result. This
        // proves the fail-closed branch does not leak into the healthy path after
        // a transient rejected state.
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        object sentinel = new object();
        object?[] exactArgs = { slots, vanilla, sentinel };
        bool exactProceed = (bool)(beforeAppend.Invoke(null, exactArgs)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: exact callback returned null."));
        Assert(exactProceed, "exact IEnumerable<Item> + pinned slot-array + one pseudo-slot15 contract must proceed");
        Assert(ReferenceEquals(exactArgs[2], sentinel),
            "healthy prefix path must leave Harmony result ownership to AppendCandidates");

        // Losing the install-time array snapshot is process-wide contract drift too.
        // It must be rejected before the bounded fallback query while preserving
        // exact vanilla result identity.
        ReloadScopeEpochGuard.ClearSlotArraySnapshotsForRegression();
        object?[] unpinnedArgs = { slots, vanilla, null };
        bool unpinnedProceed = (bool)(beforeAppend.Invoke(null, unpinnedArgs)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: unpinned callback returned null."));
        Assert(!unpinnedProceed, "unpinned accepted slot-array must suppress the bridge callback");
        Assert(ReferenceEquals(unpinnedArgs[2], vanilla),
            "unpinned slot-array must return the exact vanilla result object");
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();

        // Generation invalidation must immediately revoke an otherwise healthy
        // scope and again preserve exact vanilla identity without touching the
        // bounded fallback query.
        ReloadScopeEpochGuard.InvalidateForRegression();
        object?[] staleArgs = { slots, vanilla, null };
        bool staleProceed = (bool)(beforeAppend.Invoke(null, staleArgs)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: stale callback returned null."));
        Assert(!staleProceed, "stale generation scope must suppress the bridge callback");
        Assert(ReferenceEquals(staleArgs[2], vanilla),
            "stale generation scope must return the exact vanilla result object");

        ReloadScopeEpochGuard.ExitForRegression();
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    private static void Configure(int[] slots)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: fake GetItemsInSlots missing.");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = new[] { 1, 2, 3, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = new[] { 4, 5, 6 };
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = new[] { 4, 5, 6, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<FakeItem>);
        ReloadCandidateBridgeRuntime.GetAllParentItems = _ => Array.Empty<object>();
        ReloadCandidateBridgeRuntime.ReadTemplateId = _ => string.Empty;
    }

    private sealed class FakeInventory
    {
        public IEnumerable<FakeItem> GetItemsInSlots(IEnumerable<int> slots) => Array.Empty<FakeItem>();
    }

    private class FakeItem { }
    private sealed class FakeMagazine : FakeItem { }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload epoch prefix regression failed: " + message);
    }
}
