using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadSlotArrayContentPinRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        int[] originalFast = { 1, 2, 3 };
        int[] installedFast = { 1, 2, 3, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };
        int[] originalBind = { 4, 5, 6 };
        int[] installedBind = { 4, 5, 6, RuntimeIdentity.DedicatedBeltEquipmentSlotValue };

        ReloadScopeEpochGuard.ResetStateForRegression();
        ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFast;
        ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFast;
        ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBind;
        ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBind;
        ReloadScopeEpochGuard.CaptureSlotArraysForRegression();

        Require(ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast),
            "healthy retained original FastAccessSlots reference must match its pinned content");
        Require(ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(installedFast),
            "healthy installed FastAccessSlots reference must match its pinned content");
        Require(ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalBind),
            "healthy retained original BindAvailableSlotsExtended reference must match its pinned content");
        Require(ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(installedBind),
            "healthy installed BindAvailableSlotsExtended reference must match its pinned content");

        int saved = originalFast[1];
        originalFast[1] = 99;
        Require(!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast),
            "same-reference in-place mutation must fail closed");
        originalFast[1] = saved;
        Require(ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast),
            "restoring exact pinned content on the same retained reference must restore eligibility");

        int[] similarReplacement = { 1, 2, 3 };
        Require(!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(similarReplacement),
            "structurally identical replacement arrays must remain rejected by reference identity");

        installedBind[installedBind.Length - 1] = 777;
        Require(!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(installedBind),
            "same-reference mutation of an installed array must fail closed");

        ReloadScopeEpochGuard.ClearSlotArraySnapshotsForRegression();
        Require(!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(originalFast),
            "reset/rollback snapshot clearing must reject stale retained references");

        ReloadCandidateBridgeRuntime.Reset();
        ReloadScopeEpochGuard.ResetStateForRegression();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("Reload slot-array content pin regression failed: " + message + ".");
    }
}
