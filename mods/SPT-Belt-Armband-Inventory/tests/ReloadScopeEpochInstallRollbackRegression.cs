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

        var owner = new RecordingOwner();
        rollback.Invoke(null, new object[] { owner });
        if (owner.UnpatchCount != 1)
            throw new InvalidOperationException("reload epoch regression failed: partial Harmony owner was not unpatched exactly once");

        // Cleanup is itself a failure boundary. A broken/changed Harmony owner must not
        // escape module initialization or convert fail-closed discovery into a startup crash.
        rollback.Invoke(null, new object[] { new ThrowingOwner() });
        rollback.Invoke(null, new object[] { null });
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
}