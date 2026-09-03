using System;
using System.IO;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class FastAccessArrayInstallRollbackRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        object original = new object();
        object installed = new object();
        object current = installed;
        bool released;

        bool clean = FastAccessSlotPolicy.TryRestoreOwnedReference(
            true,
            () => current,
            value => current = value,
            original,
            installed,
            out released);
        if (!clean || !released || !ReferenceEquals(current, original))
            throw new InvalidOperationException("fast-access array rollback regression failed: clean exact-owned restore was not proven");

        object foreign = new object();
        current = foreign;
        int writes = 0;
        bool foreignSafe = FastAccessSlotPolicy.TryRestoreOwnedReference(
            true,
            () => current,
            value => { writes++; current = value; },
            original,
            installed,
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
            out released);
        if (failedRestore || released || !ReferenceEquals(current, installed))
            throw new InvalidOperationException("fast-access array rollback regression failed: failed exact-owned restore released authority");

        string source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "src", "FastAccessSlotPatches.cs"));
        if (!source.Contains("arrayRollbackUnsafe", StringComparison.Ordinal)
            || !source.Contains("arrayContentAuthorityUnsafe", StringComparison.Ordinal)
            || !source.Contains("if (arrayRollbackUnsafe || arrayContentAuthorityUnsafe)", StringComparison.Ordinal)
            || !source.Contains("if (!rollbackProven) arrayRollbackUnsafe = true;", StringComparison.Ordinal))
            throw new InvalidOperationException("fast-access array rollback regression failed: aggregate terminal reinstall fence is missing from production wiring");

        if (!source.Contains("if (!arrayRollbackUnsafe)", StringComparison.Ordinal)
            || !source.Contains("RestoreOwnedWrite(bindAvailableSlotsField", StringComparison.Ordinal)
            || !source.Contains("RestoreOwnedWrite(fastAccessSlotsField", StringComparison.Ordinal))
            throw new InvalidOperationException("fast-access array rollback regression failed: exact ownership metadata is not retained across ambiguous cleanup");

        if (source.Contains("void RestoreOwnedWrites()", StringComparison.Ordinal)
            || source.Contains("catch { }\n\n            try", StringComparison.Ordinal))
            throw new InvalidOperationException("fast-access array rollback regression failed: legacy swallowed restore-failure shape returned");
    }
}
