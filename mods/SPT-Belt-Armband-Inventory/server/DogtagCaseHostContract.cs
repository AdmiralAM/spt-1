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

    private static readonly object SnapshotSync = new();
    private static HashSet<MongoId>? capturedVanillaEntries;

    public static int CapturedVanillaEntryCount
    {
        get
        {
            lock (SnapshotSync)
                return capturedVanillaEntries?.Count ?? 0;
        }
    }

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

        lock (SnapshotSync)
        {
            if (capturedVanillaEntries == null)
            {
                // Retain our own set rather than any caller-owned mutable collection.
                // From this point the preload baseline is stable for every verifier.
                capturedVanillaEntries = snapshot;
                return;
            }

            if (!capturedVanillaEntries.SetEquals(snapshot))
                throw new InvalidOperationException("B&A&HB Dogtag host snapshot changed during preload; refusing an ambiguous host contract.");
        }
    }

    public static void RequirePreserved(HashSet<MongoId> currentFilter)
    {
        RequirePreservedSnapshot(SnapshotCurrentFilter(currentFilter));
    }

    public static void RequireCommitted(HashSet<MongoId> currentFilter)
    {
        HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);
        RequirePreservedSnapshot(current);

        var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);
        if (!current.Contains(caseTpl))
            throw new InvalidOperationException("B&A&HB Dogtag host verification refused: exact Dogtag Case template is absent after host commit.");

        // Re-snapshot the same live HashSet after the full preservation/exact-case
        // proof. Reference identity alone cannot detect an in-place mutation by a
        // concurrent startup participant. Any content drift during this bounded
        // verification window therefore fails closed instead of publishing a proof
        // that was already stale by the time RequireCommitted returned.
        HashSet<MongoId> liveAfterProof = SnapshotCurrentFilter(currentFilter);
        if (!current.SetEquals(liveAfterProof))
            throw new InvalidOperationException("B&A&HB Dogtag host verification refused: live Dogtag filter changed during committed-host verification.");
    }

    private static HashSet<MongoId> SnapshotCurrentFilter(HashSet<MongoId> currentFilter)
    {
        ArgumentNullException.ThrowIfNull(currentFilter);

        // Verify one point-in-time copy rather than repeatedly consulting a mutable
        // host set. Host registration is startup-only and tiny, so the bounded copy
        // avoids TOCTOU between preservation and exact-case checks without adding a
        // steady-state hot-path cost. Concurrent mutation that makes enumeration
        // invalid still fails closed by propagating the collection exception.
        return currentFilter.ToHashSet();
    }

    private static void RequirePreservedSnapshot(HashSet<MongoId> current)
    {
        MongoId[] captured;
        lock (SnapshotSync)
        {
            if (capturedVanillaEntries == null || capturedVanillaEntries.Count == 0)
                throw new InvalidOperationException("B&A&HB Dogtag host verification refused: vanilla acceptance snapshot was never captured.");

            // Verify against an immutable point-in-time copy so a concurrent
            // idempotent recapture cannot interleave with host verification.
            captured = capturedVanillaEntries.ToArray();
        }

        foreach (MongoId entry in captured)
        {
            if (!current.Contains(entry))
                throw new InvalidOperationException($"B&A&HB Dogtag host verification refused: pre-mutation acceptance entry {entry} was removed before trader registration.");
        }

        // Keep ownership isolation inside the reusable host contract itself rather
        // than relying only on a particular caller. The Dogtag Case is the sole
        // B&A&HB-owned template allowed in the vanilla Dogtag acceptance set.
        foreach (MongoId entry in current)
        {
            string id = entry.ToString();
            if (PersistentIdentityManifest.IsOwnedTemplate(id)
                && !string.Equals(id, RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))
                throw new InvalidOperationException($"B&A&HB Dogtag host verification refused: owned template {entry} contaminates the vanilla Dogtag host.");
        }
    }
}