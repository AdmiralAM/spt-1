using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadEpochPrefixFailClosedRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo beforeAppend = typeof(ReloadScopeEpochGuard).GetMethod(
            "BeforeAppend",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: BeforeAppend callback missing.");

        var slots = new object();
        var vanilla = new FakeItem[] { new FakeItem() };
        Configure(slots);
        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadScopeEpochGuard.EnterForRegression();

        // A process-wide contract drift must be rejected by the actual Harmony
        // prefix callback, not just by its helper. The prefix must install the
        // exact vanilla result reference and suppress AppendCandidates entirely.
        ReloadCandidateBridgeRuntime.ReturnType = typeof(object[]);
        object?[] driftArgs = { vanilla, null };
        bool driftProceed = (bool)(beforeAppend.Invoke(null, driftArgs)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: drift callback returned null."));
        Assert(!driftProceed, "drifted runtime contract must suppress the bridge callback");
        Assert(ReferenceEquals(driftArgs[1], vanilla),
            "drifted runtime contract must publish the exact vanilla result object");

        // A valid pinned contract inside the current generation must proceed and
        // must not pre-write Harmony's result. This proves the fail-closed branch
        // does not leak into the healthy path after a transient rejected state.
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        object sentinel = new object();
        object?[] exactArgs = { vanilla, sentinel };
        bool exactProceed = (bool)(beforeAppend.Invoke(null, exactArgs)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: exact callback returned null."));
        Assert(exactProceed, "exact Item[] + one pseudo-slot15 contract must proceed");
        Assert(ReferenceEquals(exactArgs[1], sentinel),
            "healthy prefix path must leave Harmony result ownership to AppendCandidates");

        // Generation invalidation must immediately revoke an otherwise healthy
        // scope and again preserve exact vanilla identity without touching the
        // bounded fallback query.
        ReloadScopeEpochGuard.InvalidateForRegression();
        object?[] staleArgs = { vanilla, null };
        bool staleProceed = (bool)(beforeAppend.Invoke(null, staleArgs)
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: stale callback returned null."));
        Assert(!staleProceed, "stale generation scope must suppress the bridge callback");
        Assert(ReferenceEquals(staleArgs[1], vanilla),
            "stale generation scope must return the exact vanilla result object");

        ReloadScopeEpochGuard.ExitForRegression();
        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    private static void Configure(object slots)
    {
        ReloadCandidateBridgeRuntime.Reset();
        ReloadCandidateBridgeRuntime.GetItemsInSlots = typeof(FakeInventory).GetMethod(nameof(FakeInventory.GetItemsInSlots))
            ?? throw new InvalidOperationException("Reload epoch prefix regression failed: fake GetItemsInSlots missing.");
        ReloadCandidateBridgeRuntime.BeltSlotsArgument = new[] { RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = slots;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = new object();
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = new object();
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = new object();
        ReloadCandidateBridgeRuntime.ItemType = typeof(FakeItem);
        ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeMagazine);
        ReloadCandidateBridgeRuntime.ReturnType = typeof(FakeItem[]);
        ReloadCandidateBridgeRuntime.GetAllParentItems = _ => Array.Empty<object>();
        ReloadCandidateBridgeRuntime.ReadTemplateId = _ => string.Empty;
    }

    private sealed class FakeInventory
    {
        public FakeItem[] GetItemsInSlots(object slots) => Array.Empty<FakeItem>();
    }

    private class FakeItem { }
    private sealed class FakeMagazine : FakeItem { }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload epoch prefix regression failed: " + message);
    }
}
