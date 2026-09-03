using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class ReloadCandidateBridgeInstallRollbackRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        MethodInfo rollback = typeof(FastAccessSlotPatches).GetMethod(
            "TryRollbackCandidateBridgeOwner",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("reload candidate rollback regression failed: proven rollback boundary missing");

        var cleanOwner = new RecordingOwner();
        MethodInfo cleanUnpatch = typeof(RecordingOwner).GetMethod(nameof(RecordingOwner.UnpatchSelf));
        object cleanResult = rollback.Invoke(null, new object[] { cleanOwner, cleanUnpatch });
        if (!(cleanResult is bool clean) || !clean || cleanOwner.UnpatchCount != 1)
            throw new InvalidOperationException("reload candidate rollback regression failed: clean owner rollback was not proven exactly once");

        MethodInfo throwingUnpatch = typeof(ThrowingOwner).GetMethod(nameof(ThrowingOwner.UnpatchSelf));
        object failedResult = rollback.Invoke(null, new object[] { new ThrowingOwner(), throwingUnpatch });
        if (!(failedResult is bool failed) || failed)
            throw new InvalidOperationException("reload candidate rollback regression failed: throwing rollback was incorrectly treated as safe");

        object missingApiResult = rollback.Invoke(null, new object[] { new RecordingOwner(), null });
        if (!(missingApiResult is bool missingApi) || missingApi)
            throw new InvalidOperationException("reload candidate rollback regression failed: owner without rollback API was incorrectly treated as safe");

        object noOwnerResult = rollback.Invoke(null, new object[] { null, null });
        if (!(noOwnerResult is bool noOwner) || !noOwner)
            throw new InvalidOperationException("reload candidate rollback regression failed: no-owner rollback must be an exact safe no-op");

        string source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "FastAccessSlotPatches.cs"));
        if (!source.Contains("candidateBridgeRollbackUnsafe", StringComparison.Ordinal)
            || !source.Contains("if (candidateBridgeRollbackUnsafe)", StringComparison.Ordinal)
            || !source.Contains("TryRollbackCandidateBridgeOwner(candidateBridgeHarmony, candidateBridgeUnpatchSelf)", StringComparison.Ordinal))
            throw new InvalidOperationException("reload candidate rollback regression failed: terminal reinstall fence is missing from production wiring");
        if (source.Contains("catch { }\n            candidateBridgeHarmony = null;", StringComparison.Ordinal))
            throw new InvalidOperationException("reload candidate rollback regression failed: legacy swallowed rollback failure shape returned");
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
