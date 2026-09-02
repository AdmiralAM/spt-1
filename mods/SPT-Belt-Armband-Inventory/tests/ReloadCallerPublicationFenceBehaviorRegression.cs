using System;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCallerPublicationFenceBehaviorRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        object vanilla = new object();
        object candidate = new object();

        try
        {
            var originalFast = new[] { 1, 2 };
            var installedFast = new[] { 1, 2, 15 };
            var originalBind = new[] { 1, 3 };
            var installedBind = new[] { 1, 3, 15 };

            ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFast;
            ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFast;
            ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBind;
            ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBind;

            ReloadScopeEpochGuard.ResetStateForRegression();
            ReloadEpochPublicationFence.ResetForRegression();
            ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
            ReloadScopeEpochGuard.EnterForRegression();

            ReloadEpochPublicationFence.Snapshot healthy = ReloadEpochPublicationFence.CaptureForRegression();
            AssertSame(candidate,
                ReloadCallerPublicationFence.SelectForRegression(vanilla, candidate, installedFast, healthy),
                "healthy current caller publication must preserve the candidate result");

            ReloadEpochPublicationFence.InvalidateForRegression();
            AssertSame(vanilla,
                ReloadCallerPublicationFence.SelectForRegression(vanilla, candidate, installedFast, healthy),
                "Reset-generation drift after candidate production must restore exact vanilla identity");

            ReloadScopeEpochGuard.ResetStateForRegression();
            ReloadEpochPublicationFence.ResetForRegression();
            ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
            ReloadScopeEpochGuard.EnterForRegression();
            ReloadEpochPublicationFence.Snapshot fastAccessGeneration = ReloadEpochPublicationFence.CaptureForRegression();
            ReloadScopeEpochGuard.InvalidateForRegression();
            AssertSame(vanilla,
                ReloadCallerPublicationFence.SelectForRegression(vanilla, candidate, installedFast, fastAccessGeneration),
                "FastAccess generation drift after candidate production must restore exact vanilla identity");

            ReloadScopeEpochGuard.ResetStateForRegression();
            ReloadEpochPublicationFence.ResetForRegression();
            ReloadScopeEpochGuard.CaptureSlotArraysForRegression();
            ReloadScopeEpochGuard.EnterForRegression();
            ReloadEpochPublicationFence.Snapshot callerMutation = ReloadEpochPublicationFence.CaptureForRegression();
            installedFast[0] = 99;
            AssertSame(vanilla,
                ReloadCallerPublicationFence.SelectForRegression(vanilla, candidate, installedFast, callerMutation),
                "same-reference in-place caller-array drift must restore exact vanilla identity");
        }
        finally
        {
            ReloadScopeEpochGuard.ResetStateForRegression();
            ReloadEpochPublicationFence.ResetForRegression();
            ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = null;
            ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = null;
            ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = null;
            ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = null;
        }
    }

    static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new InvalidOperationException("Reload caller publication behavior regression failed: " + message + ".");
    }
}
