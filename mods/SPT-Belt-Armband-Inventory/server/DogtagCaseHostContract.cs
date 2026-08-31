using SPTarkov.Server.Core.Models.Common;

namespace SPTBeltArmbandInventory.Server;

/// <summary>
/// Captures the exact non-B&A&HB Dogtag-slot acceptance set before the mod
/// appends its container. Trader registration then proves that every captured
/// vanilla/foreign entry still survives and that no other B&A&HB product has
/// contaminated the host. This prevents a later mutation from silently
/// satisfying the host check with just one arbitrary non-case entry.
/// Public surface exists only so the separate regression assembly can exercise
/// the runtime contract directly.
/// </summary>
public static class DogtagCaseHostContract
{
    // Canonical SPT/EFT dogtag templates. The Dogtag Case is additive: preload
    // must never succeed against a host that has already lost either ordinary
    // faction dogtag acceptance, otherwise equipping the container could mask a
    // broken vanilla Dogtag slot contract.
    public const string BearDogtagTemplateId = "59f32bb586f774757e1e8442";
    public const string UsecDogtagTemplateId = "59f32c3b86f77472a31742f0";

    private static HashSet<MongoId>? capturedVanillaEntries;

    public static int CapturedVanillaEntryCount => capturedVanillaEntries?.Count ?? 0;

    public static void CaptureVanillaEntries(IEnumerable<MongoId> acceptedTemplates)
    {
        ArgumentNullException.ThrowIfNull(acceptedTemplates);

        var snapshot = acceptedTemplates.ToHashSet();
        if (snapshot.Count == 0)
            throw new InvalidOperationException("B&A&HB Dogtag host snapshot refused: no vanilla/non-owned acceptance entries were present before mutation.");

        foreach (MongoId entry in snapshot)
        {
            if (PersistentIdentityManifest.IsOwnedTemplate(entry.ToString()))
                throw new InvalidOperationException($"B&A&HB Dogtag host snapshot refused: owned template {entry} was presented as a vanilla/foreign acceptance entry.");
        }

        var bearDogtag = new MongoId(BearDogtagTemplateId);
        var usecDogtag = new MongoId(UsecDogtagTemplateId);
        if (!snapshot.Contains(bearDogtag) || !snapshot.Contains(usecDogtag))
            throw new InvalidOperationException("B&A&HB Dogtag host snapshot refused: canonical BEAR/USEC dogtag acceptance is incomplete before mutation.");

        if (capturedVanillaEntries == null)
        {
            capturedVanillaEntries = snapshot;
            return;
        }

        if (!capturedVanillaEntries.SetEquals(snapshot))
            throw new InvalidOperationException("B&A&HB Dogtag host snapshot changed during preload; refusing an ambiguous host contract.");
    }

    public static void RequirePreserved(HashSet<MongoId> currentFilter)
    {
        ArgumentNullException.ThrowIfNull(currentFilter);

        if (capturedVanillaEntries == null || capturedVanillaEntries.Count == 0)
            throw new InvalidOperationException("B&A&HB Dogtag host verification refused: vanilla acceptance snapshot was never captured.");

        foreach (MongoId entry in capturedVanillaEntries)
        {
            if (!currentFilter.Contains(entry))
                throw new InvalidOperationException($"B&A&HB Dogtag host verification refused: pre-mutation acceptance entry {entry} was removed before trader registration.");
        }

        // Keep ownership isolation inside the reusable host contract itself rather
        // than relying only on a particular caller. The Dogtag Case is the sole
        // B&A&HB-owned template allowed in the vanilla Dogtag acceptance set.
        foreach (MongoId entry in currentFilter)
        {
            string id = entry.ToString();
            if (PersistentIdentityManifest.IsOwnedTemplate(id)
                && !string.Equals(id, RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))
                throw new InvalidOperationException($"B&A&HB Dogtag host verification refused: owned template {entry} contaminates the vanilla Dogtag host.");
        }
    }

    public static void RequireCommitted(HashSet<MongoId> currentFilter)
    {
        RequirePreserved(currentFilter);

        var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);
        if (!currentFilter.Contains(caseTpl))
            throw new InvalidOperationException("B&A&HB Dogtag host verification refused: exact Dogtag Case template is absent after host commit.");
    }
}
