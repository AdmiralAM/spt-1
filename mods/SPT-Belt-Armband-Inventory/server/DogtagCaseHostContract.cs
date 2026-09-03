using System.Runtime.CompilerServices;
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
    public const string BearDogtagTemplateId = "59f32bb586f774757e1e8442";
    public const string UsecDogtagTemplateId = "59f32c3b86f77472a31742f0";

    private sealed class RollbackAuthority
    {
        internal readonly HashSet<MongoId> Host;
        internal readonly HashSet<MongoId> Baseline;

        internal RollbackAuthority(HashSet<MongoId> host, HashSet<MongoId> baseline)
        {
            Host = host;
            Baseline = new HashSet<MongoId>(baseline);
        }
    }

    private static readonly object SnapshotSync = new();
    private static readonly ConditionalWeakTable<HashSet<MongoId>, RollbackAuthority> RollbackAuthorities = new();
    private static readonly ConditionalWeakTable<HashSet<MongoId>, RollbackAuthority> ActiveRollbackHosts = new();
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
                capturedVanillaEntries = snapshot;
                return;
            }

            if (!capturedVanillaEntries.SetEquals(snapshot))
                throw new InvalidOperationException("B&A&HB Dogtag host snapshot changed during preload; refusing an ambiguous host contract.");
        }
    }

    public static void RequirePreserved(HashSet<MongoId> currentFilter)
    {
        HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);
        RequirePreservedSnapshot(current);

        HashSet<MongoId> liveAfterProof = SnapshotCurrentFilter(currentFilter);
        if (!current.SetEquals(liveAfterProof))
            throw new InvalidOperationException("B&A&HB Dogtag host verification refused: live Dogtag filter changed during preserved-host verification.");
    }

    public static void RequireCommitted(HashSet<MongoId> currentFilter)
    {
        HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);
        RequirePreservedSnapshot(current);

        var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);
        if (!current.Contains(caseTpl))
            throw new InvalidOperationException("B&A&HB Dogtag host verification refused: exact Dogtag Case template is absent after host commit.");

        HashSet<MongoId> liveAfterProof = SnapshotCurrentFilter(currentFilter);
        if (!current.SetEquals(liveAfterProof))
            throw new InvalidOperationException("B&A&HB Dogtag host verification refused: live Dogtag filter changed during committed-host verification.");

        // Once the exact committed shape has been observed stable, another preload
        // transaction cannot still be in the pre-add capture phase for this host.
        // Release only the per-host capture gate; snapshot-key rollback authority is
        // retained so a later cancellation/exception can still undo this exact add.
        lock (SnapshotSync)
            ActiveRollbackHosts.Remove(currentFilter);
    }

    public static HashSet<MongoId> CaptureRollbackBaseline(HashSet<MongoId> currentFilter)
    {
        HashSet<MongoId> snapshot = SnapshotCurrentFilter(currentFilter);
        RequirePreservedSnapshot(snapshot);
        if (snapshot.Contains(new MongoId(RuntimeIdentity.DogtagCaseItemId)))
            throw new InvalidOperationException("B&A&HB Dogtag host rollback baseline refused: exact Dogtag Case was already present before an owned add transaction.");

        var authority = new RollbackAuthority(currentFilter, snapshot);
        lock (SnapshotSync)
        {
            if (ActiveRollbackHosts.TryGetValue(currentFilter, out _))
                throw new InvalidOperationException("B&A&HB Dogtag host rollback baseline refused: another pre-add transaction already owns exact-host capture authority.");

            ActiveRollbackHosts.Add(currentFilter, authority);
            try
            {
                RollbackAuthorities.Add(snapshot, authority);
            }
            catch
            {
                ActiveRollbackHosts.Remove(currentFilter);
                throw;
            }
        }
        return snapshot;
    }

    public static bool TryRollbackOwnedCaseAddition(HashSet<MongoId> currentFilter, HashSet<MongoId> preCommitSnapshot)
    {
        if (currentFilter == null || preCommitSnapshot == null) return false;
        try
        {
            RollbackAuthority? authority;
            lock (SnapshotSync)
            {
                if (!RollbackAuthorities.TryGetValue(preCommitSnapshot, out authority))
                    return false;

                // Rollback authority is single-consumer and belongs only to the exact
                // host reference captured before the owned add. A value-identical
                // replacement must never inherit removal authority from an earlier
                // host object. Consume both snapshot-key authority and any still-live
                // pre-add host gate atomically under the same synchronization boundary.
                RollbackAuthorities.Remove(preCommitSnapshot);
                if (ActiveRollbackHosts.TryGetValue(authority.Host, out RollbackAuthority? active)
                    && ReferenceEquals(active, authority))
                    ActiveRollbackHosts.Remove(authority.Host);
            }

            if (!ReferenceEquals(currentFilter, authority.Host)
                || !preCommitSnapshot.SetEquals(authority.Baseline))
                return false;

            // From this point onward the local name is rebound to the internally pinned
            // baseline, not caller-controlled mutable snapshot state.
            preCommitSnapshot = authority.Baseline;
            var caseTpl = new MongoId(RuntimeIdentity.DogtagCaseItemId);
            if (preCommitSnapshot.Contains(caseTpl)) return false;

            HashSet<MongoId> expectedCommitted = new(preCommitSnapshot) { caseTpl };
            HashSet<MongoId> current = SnapshotCurrentFilter(currentFilter);
            if (!current.SetEquals(expectedCommitted))
                return false;

            if (!currentFilter.Remove(caseTpl))
                return false;

            HashSet<MongoId> after = SnapshotCurrentFilter(currentFilter);
            return after.SetEquals(preCommitSnapshot);
        }
        catch
        {
            return false;
        }
    }

    private static HashSet<MongoId> SnapshotCurrentFilter(HashSet<MongoId> currentFilter)
    {
        ArgumentNullException.ThrowIfNull(currentFilter);
        return currentFilter.ToHashSet();
    }

    private static void RequirePreservedSnapshot(HashSet<MongoId> current)
    {
        MongoId[] captured;
        lock (SnapshotSync)
        {
            if (capturedVanillaEntries == null || capturedVanillaEntries.Count == 0)
                throw new InvalidOperationException("B&A&HB Dogtag host verification refused: vanilla acceptance snapshot was never captured.");
            captured = capturedVanillaEntries.ToArray();
        }

        foreach (MongoId entry in captured)
        {
            if (!current.Contains(entry))
                throw new InvalidOperationException($"B&A&HB Dogtag host verification refused: pre-mutation acceptance entry {entry} was removed before trader registration.");
        }

        foreach (MongoId entry in current)
        {
            string id = entry.ToString();
            if (PersistentIdentityManifest.IsOwnedTemplate(id)
                && !string.Equals(id, RuntimeIdentity.DogtagCaseItemId, StringComparison.Ordinal))
                throw new InvalidOperationException($"B&A&HB Dogtag host verification refused: owned template {entry} contaminates the vanilla Dogtag host.");
        }
    }
}