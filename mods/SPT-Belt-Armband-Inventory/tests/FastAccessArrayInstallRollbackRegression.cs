using System;
using System.IO;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class FastAccessArrayInstallRollbackRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        int[] original = { 1, 2 };
        int[] installed = { 1, 2, 15 };
        Array installedSnapshot = FastAccessSlotPolicy.CaptureArrayContentSnapshot(installed);
        object current = installed;
        bool released;

        bool clean = FastAccessSlotPolicy.TryRestoreOwnedReference(
            true,
            () => current,
            value => current = value,
            original,
            installed,
            installedSnapshot,
            out released);
        if (!clean || !released || !ReferenceEquals(current, original))
            throw new InvalidOperationException("fast-access array rollback regression failed: clean exact-owned ref+content restore was not proven");

        int[] foreign = { 9 };
        current = foreign;
        int writes = 0;
        bool foreignSafe = FastAccessSlotPolicy.TryRestoreOwnedReference(
            true,
            () => current,
            value => { writes++; current = value; },
            original,
            installed,
            installedSnapshot,
            out released);
        if (!foreignSafe || !released || writes != 0 || !ReferenceEquals(current, foreign))
            throw new InvalidOperationException("fast-access array rollback regression failed: foreign replacement was not preserved as a safe no-op");

        current = installed;
        bool failedRestore = FastAccessSlotPolicy.TryRestoreOwnedReference(
            true,
            () => current,
            value => throw new InvalidOperationException("synthetic restore failure"),
            original,
            installed,
            installedSnapshot,
            out released);
        if (failedRestore || released || !ReferenceEquals(current, installed))
            throw new InvalidOperationException("fast-access array rollback regression failed: failed exact-owned restore released authority");

        installed[0] = 99;
        current = installed;
        writes = 0;
        bool mutatedRejected = FastAccessSlotPolicy.TryRestoreOwnedReference(
            true,
            () => current,
            value => { writes++; current = value; },
            original,
            installed,
            installedSnapshot,
            out released);
        if (mutatedRejected || released || writes != 0 || !ReferenceEquals(current, installed))
            throw new InvalidOperationException("fast-access array rollback regression failed: same-reference content drift was overwritten or ownership was released");

        installed[0] = 1;
        if (!FastAccessSlotPolicy.HasExactArrayContent(installed, installedSnapshot))
            throw new InvalidOperationException("fast-access array rollback regression failed: synthetic ABA restoration did not restore test content");

        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "src", "FastAccessSlotPatches.cs"));
        if (!source.Contains("arrayRollbackUnsafe", StringComparison.Ordinal)
            || !source.Contains("arrayContentAuthorityUnsafe", StringComparison.Ordinal)
            || !source.Contains("if (arrayRollbackUnsafe || arrayContentAuthorityUnsafe)", StringComparison.Ordinal)
            || !source.Contains("if (!rollbackProven) arrayRollbackUnsafe = true;", StringComparison.Ordinal))
            throw new InvalidOperationException("fast-access array rollback regression failed: aggregate terminal reinstall fence is missing from production wiring");

        if (!source.Contains("installedContentSnapshot", StringComparison.Ordinal)
            || !source.Contains("if (!HasExactArrayContent(current, installedContentSnapshot))", StringComparison.Ordinal)
            || !source.Contains("if (!proven && wrote) arrayContentAuthorityUnsafe = true;", StringComparison.Ordinal))
            throw new InvalidOperationException("fast-access array rollback regression failed: teardown is not wired to detached installed-content authority");

        if (!source.Contains("if (!arrayRollbackUnsafe)", StringComparison.Ordinal)
            || !source.Contains("installedBindAvailableSlotsContent, ref wroteBindAvailableSlots", StringComparison.Ordinal)
            || !source.Contains("installedFastAccessSlotsContent, ref wroteFastAccessSlots", StringComparison.Ordinal))
            throw new InvalidOperationException("fast-access array rollback regression failed: exact ownership/content metadata is not retained across ambiguous cleanup");

        if (source.Contains("void RestoreOwnedWrites()", StringComparison.Ordinal)
            || source.Contains("catch { }\n\n            try", StringComparison.Ordinal))
            throw new InvalidOperationException("fast-access array rollback regression failed: legacy swallowed restore-failure shape returned");
    }
}
