using System;
using System.IO;
using System.Runtime.CompilerServices;
using SPTBeltArmbandInventory;

internal static class FastAccessFirstInstallBaselineIsolationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        int[] baseline = { 1, 2, 3 };
        Array snapshot = FastAccessSlotPolicy.CaptureArrayContentSnapshot(baseline);
        if (snapshot == null || ReferenceEquals(snapshot, baseline))
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: snapshot must be detached.");
        if (!FastAccessSlotPolicy.HasExactArrayReferenceAndContent(baseline, baseline, snapshot))
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: unchanged exact baseline must prove.");

        baseline[1] = 99;
        if (FastAccessSlotPolicy.HasExactArrayReferenceAndContent(baseline, baseline, snapshot))
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: same-reference in-place mutation must invalidate baseline authority.");
        baseline[1] = 2;
        if (!FastAccessSlotPolicy.HasExactArrayReferenceAndContent(baseline, baseline, snapshot))
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: helper must remain a point-in-time proof; lifecycle monotonicity belongs to TryInstall authority state.");
        if (FastAccessSlotPolicy.HasExactArrayReferenceAndContent((int[])baseline.Clone(), baseline, snapshot))
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: value-identical replacement reference must not inherit authority.");

        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "src", "FastAccessSlotPatches.cs"));
        int tryInstall = source.IndexOf("internal bool TryInstall()", StringComparison.Ordinal);
        int originalFastRead = source.IndexOf("originalFastAccessSlots = fastAccessSlotsField.GetValue(null);", tryInstall, StringComparison.Ordinal);
        int originalBindRead = source.IndexOf("originalBindAvailableSlots = bindAvailableSlotsField.GetValue(null);", originalFastRead, StringComparison.Ordinal);
        int fastSnapshot = source.IndexOf("originalFastAccessSlotsContent = FastAccessSlotPolicy.CaptureArrayContentSnapshot(originalFastAccessSlots);", originalBindRead, StringComparison.Ordinal);
        int bindSnapshot = source.IndexOf("originalBindAvailableSlotsContent = FastAccessSlotPolicy.CaptureArrayContentSnapshot(originalBindAvailableSlots);", fastSnapshot, StringComparison.Ordinal);
        int buildFast = source.IndexOf("installedFastAccessSlots = AppendSlots(originalFastAccessSlotsContent", bindSnapshot, StringComparison.Ordinal);
        int buildBind = source.IndexOf("installedBindAvailableSlots = AppendSlots(originalBindAvailableSlotsContent", buildFast, StringComparison.Ordinal);
        int rereadFast = source.IndexOf("object currentFastAccessSlots = fastAccessSlotsField.GetValue(null);", buildBind, StringComparison.Ordinal);
        int rereadBind = source.IndexOf("object currentBindAvailableSlots = bindAvailableSlotsField.GetValue(null);", rereadFast, StringComparison.Ordinal);
        int proofFast = source.IndexOf("HasExactArrayReferenceAndContent(currentFastAccessSlots, originalFastAccessSlots, originalFastAccessSlotsContent)", rereadBind, StringComparison.Ordinal);
        int proofBind = source.IndexOf("HasExactArrayReferenceAndContent(currentBindAvailableSlots, originalBindAvailableSlots, originalBindAvailableSlotsContent)", proofFast, StringComparison.Ordinal);
        int unsafeMarker = source.IndexOf("arrayContentAuthorityUnsafe = true;", proofBind, StringComparison.Ordinal);
        int firstWrite = source.IndexOf("fastAccessSlotsField.SetValue(null, installedFastAccessSlots);", unsafeMarker, StringComparison.Ordinal);
        int secondWrite = source.IndexOf("bindAvailableSlotsField.SetValue(null, installedBindAvailableSlots);", firstWrite, StringComparison.Ordinal);

        if (!(tryInstall >= 0 && originalFastRead > tryInstall && originalBindRead > originalFastRead
            && fastSnapshot > originalBindRead && bindSnapshot > fastSnapshot
            && buildFast > bindSnapshot && buildBind > buildFast
            && rereadFast > buildBind && rereadBind > rereadFast
            && proofFast > rereadBind && proofBind > proofFast
            && unsafeMarker > proofBind && firstWrite > unsafeMarker && secondWrite > firstWrite))
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: capture/build/reproof/publication ordering changed.");

        string firstInstallBody = source.Substring(tryInstall, source.IndexOf("bool ValidateExistingInstallAuthority()", tryInstall, StringComparison.Ordinal) - tryInstall);
        Require(firstInstallBody, "first install refused because vanilla array reference/content authority drifted before publication", "baseline drift must fail closed explicitly");
        Require(firstInstallBody, "arrayContentAuthorityUnsafe = true;", "first observed pre-publication drift must become monotonic for the lifecycle");
        if (firstInstallBody.Contains("AppendSlots(originalFastAccessSlots as Array", StringComparison.Ordinal)
            || firstInstallBody.Contains("AppendSlots(originalBindAvailableSlots as Array", StringComparison.Ordinal))
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: installed arrays must be derived only from detached baseline snapshots.");
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
            throw new InvalidOperationException("FastAccess first-install baseline regression failed: " + message + ".");
    }
}
