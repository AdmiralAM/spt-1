using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadScopeEpochInstallRollbackRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo rollback = typeof(ReloadScopeEpochGuard).GetMethod(
            "TryRollbackOwner",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("reload epoch regression failed: owner rollback boundary missing");
        MethodInfo findRollback = typeof(ReloadScopeEpochGuard).GetMethod(
            "FindZeroArgInstanceMethod",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("reload epoch regression failed: preflight rollback discovery boundary missing");

        MethodInfo recordingUnpatch = (MethodInfo)findRollback.Invoke(null, new object[] { typeof(RecordingOwner), "UnpatchSelf" });
        if (recordingUnpatch == null)
            throw new InvalidOperationException("reload epoch regression failed: unique zero-arg rollback API was not resolved");

        var owner = new RecordingOwner();
        object cleanResult = rollback.Invoke(null, new object[] { owner, recordingUnpatch });
        if (!(cleanResult is bool clean) || !clean || owner.UnpatchCount != 1)
            throw new InvalidOperationException("reload epoch regression failed: partial Harmony owner was not unpatched exactly once");

        MethodInfo throwingUnpatch = (MethodInfo)findRollback.Invoke(null, new object[] { typeof(ThrowingOwner), "UnpatchSelf" });
        object failedResult = rollback.Invoke(null, new object[] { new ThrowingOwner(), throwingUnpatch });
        if (!(failedResult is bool failed) || failed)
            throw new InvalidOperationException("reload epoch regression failed: throwing rollback was not reported as terminally unsafe");

        object missingApiResult = rollback.Invoke(null, new object[] { new RecordingOwner(), null });
        if (!(missingApiResult is bool missingApi) || missingApi)
            throw new InvalidOperationException("reload epoch regression failed: missing rollback API was incorrectly treated as safe");

        object noOwnerResult = rollback.Invoke(null, new object[] { null, null });
        if (!(noOwnerResult is bool noOwner) || !noOwner)
            throw new InvalidOperationException("reload epoch regression failed: no-owner rollback must be an exact safe no-op");

        // Ambiguous rollback APIs are rejected before any Harmony.Patch mutation.
        object ambiguous = findRollback.Invoke(null, new object[] { typeof(AmbiguousOwner), "UnpatchSelf" });
        if (ambiguous != null)
            throw new InvalidOperationException("reload epoch regression failed: ambiguous zero-arg rollback API did not fail closed");
    }

    private sealed class RecordingOwner
    {
        internal int UnpatchCount { get; private set; }

        public void UnpatchSelf()
        {
            UnpatchCount++;
        }
    }

    private sealed class ThrowingOwner
    {
        public void UnpatchSelf()
        {
            throw new InvalidOperationException("synthetic rollback failure");
        }
    }

    private class AmbiguousBase
    {
        public void UnpatchSelf() { }
    }

    private sealed class AmbiguousOwner : AmbiguousBase
    {
        public new void UnpatchSelf() { }
    }
}