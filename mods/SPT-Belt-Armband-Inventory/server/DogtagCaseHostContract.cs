using SPTarkov.Server.Core.Models.Common;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Captures the exact non-B&A&HB Dogtag-slot acceptance set before the mod
/// appends its container. Trader registration then proves that every captured
/// vanilla/foreign entry still survives. This prevents a later mutation from
/// silently satisfying the host check with just one arbitrary non-case entry.
/// </summary>
internal static class DogtagCaseHostContract
{
    private static HashSet<MongoId>? capturedVanillaEntries;

    internal static int CapturedVanillaEntryCount => capturedVanillaEntries?.Count ?? 0;

    internal static void CaptureVanillaEntries(IEnumerable<MongoId> acceptedTemplates)
    {
        ArgumentNullException.ThrowIfNull(acceptedTemplates);

        var snapshot = acceptedTemplates.ToHashSet();
        if (snapshot.Count == 0)
            throw new InvalidOperationException("B&A&HB Dogtag host snapshot refused: no vanilla/non-owned acceptance entries were present before mutation.");

        if (capturedVanillaEntries == null)
        {
            capturedVanillaEntries = snapshot;
            return;
        }

        if (!capturedVanillaEntries.SetEquals(snapshot))
            throw new InvalidOperationException("B&A&HB Dogtag host snapshot changed during preload; refusing an ambiguous host contract.");
    }

    internal static void RequirePreserved(HashSet<MongoId> currentFilter)
    {
        ArgumentNullException.ThrowIfNull(currentFilter);

        if (capturedVanillaEntries == null || capturedVanillaEntries.Count == 0)
            throw new InvalidOperationException("B&A&HB Dogtag host verification refused: vanilla acceptance snapshot was never captured.");

        foreach (MongoId entry in capturedVanillaEntries)
        {
            if (!currentFilter.Contains(entry))
                throw new InvalidOperationException($"B&A&HB Dogtag host verification refused: pre-mutation acceptance entry {entry} was removed before trader registration.");
        }
    }
}