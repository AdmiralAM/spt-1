using System;
using System.IO;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class FastAccessCompletedPublicationReproofRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        int[] installedFast = { 1, 2, 15 };
        int[] installedBind = { 1, 2, 15 };
        Array fastSnapshot = FastAccessSlotPolicy.CaptureArrayContentSnapshot(installedFast)
            ?? throw new InvalidOperationException("FastAccess completed publication regression failed: fast snapshot missing.");
        Array bindSnapshot = FastAccessSlotPolicy.CaptureArrayContentSnapshot(installedBind)
            ?? throw new InvalidOperationException("FastAccess completed publication regression failed: bind snapshot missing.");

        if (!FastAccessSlotPolicy.HasExactArrayReferenceAndContent(installedFast, installedFast, fastSnapshot)
            || !FastAccessSlotPolicy.HasExactArrayReferenceAndContent(installedBind, installedBind, bindSnapshot))
            throw new InvalidOperationException("FastAccess completed publication regression failed: unchanged completed arrays must prove exact ref+content authority.");

        installedFast[2] = 99;
        if (FastAccessSlotPolicy.HasExactArrayReferenceAndContent(installedFast, installedFast, fastSnapshot))
            throw new InvalidOperationException("FastAccess completed publication regression failed: same-reference FastAccessSlots mutation must invalidate final publication authority.");
        installedFast[2] = 15;

        installedBind[0] = 99;
        if (FastAccessSlotPolicy.HasExactArrayReferenceAndContent(installedBind, installedBind, bindSnapshot))
            throw new InvalidOperationException("FastAccess completed publication regression failed: same-reference BindAvailableSlotsExtended mutation must invalidate final publication authority.");
        installedBind[0] = 1;

        if (FastAccessSlotPolicy.HasExactArrayReferenceAndContent((int[])installedFast.Clone(), installedFast, fastSnapshot)
            || FastAccessSlotPolicy.HasExactArrayReferenceAndContent((int[])installedBind.Clone(), installedBind, bindSnapshot))
            throw new InvalidOperationException("FastAccess completed publication regression failed: value-identical replacement arrays must not inherit publication authority.");

        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("FastAccess completed publication regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs")).Replace("\r\n", "\n", StringComparison.Ordinal);
        int tryInstall = source.IndexOf("internal bool TryInstall()", StringComparison.Ordinal);
        int secondWrite = source.IndexOf("bindAvailableSlotsField.SetValue(null, installedBindAvailableSlots);", tryInstall, StringComparison.Ordinal);
        int secondOwned = source.IndexOf("wroteBindAvailableSlots = true;", secondWrite, StringComparison.Ordinal);
        int completedCall = source.IndexOf("ReproveCompletedPublication()", secondOwned, StringComparison.Ordinal);
        int installedFlag = source.IndexOf("installed = true;", completedCall, StringComparison.Ordinal);
        int reachabilityCall = source.IndexOf("TryInstallReloadReachability()", installedFlag, StringComparison.Ordinal);
        int candidateCall = source.IndexOf("TryInstallReloadCandidateBridge", reachabilityCall, StringComparison.Ordinal);
        int completedMethod = source.IndexOf("bool ReproveCompletedPublication()", completedCall, StringComparison.Ordinal);
        int rereadFast = source.IndexOf("fastAccessSlotsField.GetValue(null)", completedMethod, StringComparison.Ordinal);
        int rereadBind = source.IndexOf("bindAvailableSlotsField.GetValue(null)", rereadFast, StringComparison.Ordinal);
        int fastProof = source.IndexOf("currentFastAccessSlots, installedFastAccessSlots, installedFastAccessSlotsContent", rereadBind, StringComparison.Ordinal);
        int bindProof = source.IndexOf("currentBindAvailableSlots, installedBindAvailableSlots, installedBindAvailableSlotsContent", fastProof, StringComparison.Ordinal);
        int unsafeMarker = source.IndexOf("arrayContentAuthorityUnsafe = true;", bindProof, StringComparison.Ordinal);
        int rollbackCall = source.IndexOf("RestoreOwnedWrites()", unsafeMarker, StringComparison.Ordinal);

        if (!(tryInstall >= 0 && secondWrite > tryInstall && secondOwned > secondWrite
            && completedCall > secondOwned && installedFlag > completedCall
            && reachabilityCall > installedFlag && candidateCall > reachabilityCall
            && completedMethod > completedCall && rereadFast > completedMethod && rereadBind > rereadFast
            && fastProof > rereadBind && bindProof > fastProof && unsafeMarker > bindProof && rollbackCall > unsafeMarker))
            throw new InvalidOperationException("FastAccess completed publication regression failed: second write -> dual final reproof -> installed/Harmony publication ordering changed.");

        int nextMethod = source.IndexOf("bool ValidateExistingInstallAuthority()", completedMethod, StringComparison.Ordinal);
        if (nextMethod <= completedMethod)
            throw new InvalidOperationException("FastAccess completed publication regression failed: completed publication method boundary missing.");
        string body = source.Substring(completedMethod, nextMethod - completedMethod);
        Require(body, "if (fastExact && bindExact)", "both completed arrays must prove before reload integration publication");
        Require(body, "arrayContentAuthorityUnsafe = true;", "completed publication drift must terminally poison content authority");
        Require(body, "RestoreOwnedWrites()", "completed publication drift must enter ownership-aware rollback");
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
            throw new InvalidOperationException("FastAccess completed publication regression failed: " + message + ".");
    }
}
