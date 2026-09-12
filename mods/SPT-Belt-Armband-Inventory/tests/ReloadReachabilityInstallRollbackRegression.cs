using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadReachabilityInstallRollbackRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo rollback = typeof(FastAccessSlotPatches).GetMethod(
            "TryRollbackReachabilityOwner",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("reload reachability rollback regression failed: proven rollback boundary missing");

        var cleanOwner = new RecordingOwner();
        MethodInfo cleanUnpatch = typeof(RecordingOwner).GetMethod(nameof(RecordingOwner.UnpatchSelf));
        object cleanResult = rollback.Invoke(null, new object[] { cleanOwner, cleanUnpatch });
        if (!(cleanResult is bool clean) || !clean || cleanOwner.UnpatchCount != 1)
            throw new InvalidOperationException("reload reachability rollback regression failed: clean owner rollback was not proven exactly once");

        MethodInfo throwingUnpatch = typeof(ThrowingOwner).GetMethod(nameof(ThrowingOwner.UnpatchSelf));
        object failedResult = rollback.Invoke(null, new object[] { new ThrowingOwner(), throwingUnpatch });
        if (!(failedResult is bool failed) || failed)
            throw new InvalidOperationException("reload reachability rollback regression failed: throwing rollback was incorrectly treated as safe");

        object missingApiResult = rollback.Invoke(null, new object[] { new RecordingOwner(), null });
        if (!(missingApiResult is bool missingApi) || missingApi)
            throw new InvalidOperationException("reload reachability rollback regression failed: owner without rollback API was incorrectly treated as safe");

        object noOwnerResult = rollback.Invoke(null, new object[] { null, null });
        if (!(noOwnerResult is bool noOwner) || !noOwner)
            throw new InvalidOperationException("reload reachability rollback regression failed: no-owner rollback must be an exact safe no-op");

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "FastAccessSlotPatches.cs"));
        if (!source.Contains("reachabilityRollbackUnsafe", StringComparison.Ordinal)
            || !source.Contains("if (reachabilityRollbackUnsafe)", StringComparison.Ordinal)
            || !source.Contains("TryRollbackReachabilityOwner(reachabilityHarmony, reachabilityUnpatchSelf)", StringComparison.Ordinal))
            throw new InvalidOperationException("reload reachability rollback regression failed: terminal reinstall fence is missing from production wiring");
        if (!source.Contains("FastAccessReloadRuntime.Reset();", StringComparison.Ordinal)
            || !source.Contains("reachabilityRollbackUnsafe = true;", StringComparison.Ordinal))
            throw new InvalidOperationException("reload reachability rollback regression failed: unproven rollback does not revoke runtime promotion authority");
        if (source.Contains("catch { }\n            reachabilityHarmony = null;", StringComparison.Ordinal))
            throw new InvalidOperationException("reload reachability rollback regression failed: legacy swallowed rollback failure shape returned");
    }

    private sealed class RecordingOwner
    {
        internal int UnpatchCount { get; private set; }
        public void UnpatchSelf() => UnpatchCount++;
    }

    private sealed class ThrowingOwner
    {
        public void UnpatchSelf() => throw new InvalidOperationException("synthetic rollback failure");
    }
}
