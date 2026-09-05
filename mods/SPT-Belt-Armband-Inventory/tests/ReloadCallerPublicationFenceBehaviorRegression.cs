using System;
using System.Reflection;
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

            ExerciseActualThreadStaticCleanup(installedFast, vanilla, candidate);

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
            TryCleanupActualThreadStaticState();
            ReloadScopeEpochGuard.ResetStateForRegression();
            ReloadEpochPublicationFence.ResetForRegression();
            ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = null;
            ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = null;
            ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = null;
            ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = null;
        }
    }

    private static void ExerciseActualThreadStaticCleanup(object slots, object outerVanilla, object candidate)
    {
        Type type = typeof(ReloadCallerPublicationFence);
        MethodInfo captureEntryDepth = RequirePrivate(type, "CaptureEntryDepth");
        MethodInfo captureVanilla = RequirePrivate(type, "CaptureVanilla");
        MethodInfo finalizePublication = RequirePrivate(type, "FinalizePublication");
        MethodInfo cleanupPublicationState = RequirePrivate(type, "CleanupPublicationState");

        int outerEntryDepth = CaptureDepth(captureEntryDepth);
        if (outerEntryDepth != 0)
            throw new InvalidOperationException("Reload caller publication behavior regression failed: ThreadStatic publication stack was not empty before executable cleanup exercise.");

        captureVanilla.Invoke(null, new[] { slots, outerVanilla });
        int nestedEntryDepth = CaptureDepth(captureEntryDepth);
        if (nestedEntryDepth != 1)
            throw new InvalidOperationException("Reload caller publication behavior regression failed: outer vanilla capture did not create exactly one publication frame.");

        object innerVanilla = new object();
        captureVanilla.Invoke(null, new[] { slots, innerVanilla });
        if (CaptureDepth(captureEntryDepth) != 2)
            throw new InvalidOperationException("Reload caller publication behavior regression failed: nested/reentrant capture did not stack above the outer frame.");

        var foreignFailure = new InvalidOperationException("synthetic intervening postfix failure");
        object? returned = cleanupPublicationState.Invoke(null, new object?[] { foreignFailure, nestedEntryDepth });
        AssertSame(foreignFailure, returned!, "cleanup finalizer must preserve the exact incoming exception object");
        if (CaptureDepth(captureEntryDepth) != 1)
            throw new InvalidOperationException("Reload caller publication behavior regression failed: nested exception cleanup did not trim exactly to the nested entry depth.");

        object[] finalizeArgs = { slots, candidate };
        finalizePublication.Invoke(null, finalizeArgs);
        AssertSame(candidate, finalizeArgs[1], "outer publication must remain healthy after bounded nested cleanup");
        if (CaptureDepth(captureEntryDepth) != outerEntryDepth)
            throw new InvalidOperationException("Reload caller publication behavior regression failed: normal outer finalization did not restore the exact entry depth.");

        object staleSentinel = new object();
        object[] noFrameFinalizeArgs = { slots, staleSentinel };
        finalizePublication.Invoke(null, noFrameFinalizeArgs);
        AssertSame(staleSentinel, noFrameFinalizeArgs[1], "a subsequent call must not observe a stale publication frame after exception cleanup");

        captureVanilla.Invoke(null, new[] { slots, outerVanilla });
        ReloadEpochPublicationFence.InvalidateForRegression();
        object[] invalidatedArgs = { slots, candidate };
        finalizePublication.Invoke(null, invalidatedArgs);
        AssertSame(outerVanilla, invalidatedArgs[1], "actual finalization must restore exact captured vanilla after epoch invalidation");
        if (CaptureDepth(captureEntryDepth) != 0)
            throw new InvalidOperationException("Reload caller publication behavior regression failed: invalidated finalization leaked ThreadStatic publication state.");

        // Restore a clean generation for the remaining regression cases.
        ReloadEpochPublicationFence.ResetForRegression();
    }

    private static int CaptureDepth(MethodInfo captureEntryDepth)
    {
        object[] args = { 0 };
        captureEntryDepth.Invoke(null, args);
        return (int)args[0];
    }

    private static MethodInfo RequirePrivate(Type type, string name)
    {
        return type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Reload caller publication behavior regression failed: missing private runtime method " + name + ".");
    }

    private static void TryCleanupActualThreadStaticState()
    {
        try
        {
            Type type = typeof(ReloadCallerPublicationFence);
            MethodInfo captureEntryDepth = RequirePrivate(type, "CaptureEntryDepth");
            MethodInfo cleanup = RequirePrivate(type, "CleanupPublicationState");
            int depth = CaptureDepth(captureEntryDepth);
            if (depth > 0)
                cleanup.Invoke(null, new object?[] { null, 0 });
        }
        catch
        {
            // Do not mask the original regression failure from the finally path.
        }
    }

    static void AssertSame(object expected, object actual, string message)
    {
        if (!ReferenceEquals(expected, actual))
            throw new InvalidOperationException("Reload caller publication behavior regression failed: " + message + ".");
    }
}
