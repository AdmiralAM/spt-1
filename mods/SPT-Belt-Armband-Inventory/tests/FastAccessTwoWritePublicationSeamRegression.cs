using System;
using System.IO;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class FastAccessTwoWritePublicationSeamRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        int[] originalSecond = { 1, 2 };
        int[] installedFirst = { 1, 2, 15 };
        Array originalSecondSnapshot = FastAccessSlotPolicy.CaptureArrayContentSnapshot(originalSecond)
            ?? throw new InvalidOperationException("FastAccess publication seam regression failed: second baseline snapshot missing.");
        Array installedFirstSnapshot = FastAccessSlotPolicy.CaptureArrayContentSnapshot(installedFirst)
            ?? throw new InvalidOperationException("FastAccess publication seam regression failed: first installed snapshot missing.");

        if (!FastAccessSlotPolicy.HasExactArrayReferenceAndContent(installedFirst, installedFirst, installedFirstSnapshot)
            || !FastAccessSlotPolicy.HasExactArrayReferenceAndContent(originalSecond, originalSecond, originalSecondSnapshot))
            throw new InvalidOperationException("FastAccess publication seam regression failed: unchanged first-published/second-baseline authority must prove.");

        installedFirst[2] = 99;
        if (FastAccessSlotPolicy.HasExactArrayReferenceAndContent(installedFirst, installedFirst, installedFirstSnapshot))
            throw new InvalidOperationException("FastAccess publication seam regression failed: same-reference first-field mutation must invalidate seam authority.");
        installedFirst[2] = 15;
        originalSecond[0] = 99;
        if (FastAccessSlotPolicy.HasExactArrayReferenceAndContent(originalSecond, originalSecond, originalSecondSnapshot))
            throw new InvalidOperationException("FastAccess publication seam regression failed: same-reference second-baseline mutation must invalidate seam authority.");
        originalSecond[0] = 1;
        if (FastAccessSlotPolicy.HasExactArrayReferenceAndContent((int[])installedFirst.Clone(), installedFirst, installedFirstSnapshot)
            || FastAccessSlotPolicy.HasExactArrayReferenceAndContent((int[])originalSecond.Clone(), originalSecond, originalSecondSnapshot))
            throw new InvalidOperationException("FastAccess publication seam regression failed: value-identical replacement references must not inherit publication authority.");

        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("FastAccess publication seam regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));
        int tryInstall = source.IndexOf("internal bool TryInstall()", StringComparison.Ordinal);
        int firstWrite = source.IndexOf("fastAccessSlotsField.SetValue(null, installedFastAccessSlots);", tryInstall, StringComparison.Ordinal);
        int firstOwned = source.IndexOf("wroteFastAccessSlots = true;", firstWrite, StringComparison.Ordinal);
        int seamCall = source.IndexOf("ReproveFirstPublicationSeam()", firstOwned, StringComparison.Ordinal);
        int secondWrite = source.IndexOf("bindAvailableSlotsField.SetValue(null, installedBindAvailableSlots);", seamCall, StringComparison.Ordinal);
        int seamMethod = source.IndexOf("bool ReproveFirstPublicationSeam()", secondWrite, StringComparison.Ordinal);
        int rereadFirst = source.IndexOf("fastAccessSlotsField.GetValue(null)", seamMethod, StringComparison.Ordinal);
        int rereadSecond = source.IndexOf("bindAvailableSlotsField.GetValue(null)", rereadFirst, StringComparison.Ordinal);
        int firstProof = source.IndexOf("currentFastAccessSlots, installedFastAccessSlots, installedFastAccessSlotsContent", rereadSecond, StringComparison.Ordinal);
        int secondProof = source.IndexOf("currentBindAvailableSlots, originalBindAvailableSlots, originalBindAvailableSlotsContent", firstProof, StringComparison.Ordinal);
        int unsafeMarker = source.IndexOf("arrayContentAuthorityUnsafe = true;", secondProof, StringComparison.Ordinal);

        if (!(tryInstall >= 0 && firstWrite > tryInstall && firstOwned > firstWrite
            && seamCall > firstOwned && secondWrite > seamCall && seamMethod > secondWrite
            && rereadFirst > seamMethod && rereadSecond > rereadFirst
            && firstProof > rereadSecond && secondProof > firstProof && unsafeMarker > secondProof))
            throw new InvalidOperationException("FastAccess publication seam regression failed: exact first-write -> dual reproof -> second-write ordering changed.");

        string seamBody = source.Substring(seamMethod, source.IndexOf("bool ValidateExistingInstallAuthority()", seamMethod, StringComparison.Ordinal) - seamMethod);
        Require(seamBody, "if (firstPublishedExact && secondBaselineExact)", "second write must require both exact authorities");
        Require(seamBody, "RestoreOwnedWrite(fastAccessSlotsField", "safe exact first publication must roll back when second baseline drifts");
        Require(seamBody, "!ReferenceEquals(currentFastAccessSlots, installedFastAccessSlots)", "foreign first-field replacement must relinquish ownership without overwrite");
        Require(seamBody, "arrayRollbackUnsafe = true;", "same-reference content ambiguity/unreadable seam must become terminal fail-closed");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string source = Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs");
            if (File.Exists(source)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "src", "FastAccessSlotPatches.cs");
            if (File.Exists(direct)) return current.FullName;
            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "src", "FastAccessSlotPatches.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("FastAccess publication seam regression failed: " + message + ".");
    }
}
