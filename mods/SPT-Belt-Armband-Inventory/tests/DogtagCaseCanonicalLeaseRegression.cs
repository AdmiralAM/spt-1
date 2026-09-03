using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseCanonicalLeaseRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: module root could not be resolved.");

        string preflight = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseCanonicalFilterPreflight.cs"));
        string lease = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseCanonicalIdentityLease.cs"));
        string item = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));

        int preflightIdentity = preflight.IndexOf("RequireCanonicalIdentity(source, identity);", StringComparison.Ordinal);
        int publish = preflight.IndexOf("DogtagCaseCanonicalIdentityLease.Publish(source);", StringComparison.Ordinal);
        int postPublishIdentity = publish < 0 ? -1 : preflight.IndexOf("RequireCanonicalIdentity(source, identity);", publish + 1, StringComparison.Ordinal);
        int postPublishCancellation = postPublishIdentity < 0 ? -1 : preflight.IndexOf("cancellationToken.ThrowIfCancellationRequested();", postPublishIdentity, StringComparison.Ordinal);
        int cancellationCatch = postPublishCancellation < 0 ? -1 : preflight.IndexOf("catch (OperationCanceledException)", postPublishCancellation, StringComparison.Ordinal);
        int cancelPendingCall = cancellationCatch < 0 ? -1 : preflight.IndexOf("DogtagCaseCanonicalIdentityLease.CancelPending(source);", cancellationCatch, StringComparison.Ordinal);
        if (preflightIdentity < 0 || publish < 0 || postPublishIdentity < 0 || postPublishCancellation < 0 || cancellationCatch < 0 || cancelPendingCall < 0
            || !(preflightIdentity < publish && publish < postPublishIdentity && postPublishIdentity < postPublishCancellation
                 && postPublishCancellation < cancellationCatch && cancellationCatch < cancelPendingCall))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: Preload +2 must bracket lease publication with exact proof and cancellation-only pending-authority rollback.");
        if (!preflight.Contains("new HashSet<MongoId>(x.Filter!)", StringComparison.Ordinal)
            || !preflight.Contains("new HashSet<MongoId>(x.ExcludedFilter)", StringComparison.Ordinal)
            || !preflight.Contains("included.SetEquals(expected.IncludedValues[i])", StringComparison.Ordinal)
            || !preflight.Contains("excluded.SetEquals(expectedExcluded)", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: Preload +2 proof must pin detached include/exclude content across lease publication.");

        string[] requiredLeasePins =
        {
            "ReferenceEquals(Source, expectedSource)",
            "ReferenceEquals(liveSource, Source)",
            "ReferenceEquals(liveSource.Properties, Properties)",
            "ReferenceEquals(liveSource.Properties?.Grids, GridsCollection)",
            "ReferenceEquals(grids[0], Grid)",
            "ReferenceEquals(grids[0].Properties, GridProperties)",
            "ReferenceEquals(grids[0].Properties?.Filters, FiltersCollection)",
            "ReferenceEquals(groups[i], Groups[i])",
            "ReferenceEquals(groups[i].Filter, Included[i])",
            "ReferenceEquals(groups[i].ExcludedFilter, Excluded[i])",
            "included.SetEquals(IncludedValues[i])",
            "excluded.SetEquals(expectedExcluded)",
            "included.Any(id => PersistentIdentityManifest.IsOwnedTemplate(id.ToString()))"
        };
        foreach (string pin in requiredLeasePins)
            if (!lease.Contains(pin, StringComparison.Ordinal))
                throw new InvalidOperationException("Dogtag canonical lease regression failed: missing exact source identity/content pin: " + pin);

        if (!lease.Contains("new HashSet<MongoId>(x.Filter!)", StringComparison.Ordinal)
            || !lease.Contains("new HashSet<MongoId>(x.ExcludedFilter)", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: +2 lease must keep detached include/exclude content snapshots rather than aliasing live HashSets.");

        int publishMethod = lease.IndexOf("internal static void Publish(TemplateItem source)", StringComparison.Ordinal);
        int captureNext = publishMethod < 0 ? -1 : lease.IndexOf("Lease next = Capture(source);", publishMethod, StringComparison.Ordinal);
        int publishLock = captureNext < 0 ? -1 : lease.IndexOf("lock (Sync)", captureNext, StringComparison.Ordinal);
        int duplicateGuard = publishLock < 0 ? -1 : lease.IndexOf("if (pending != null)", publishLock, StringComparison.Ordinal);
        int pendingAssignment = duplicateGuard < 0 ? -1 : lease.IndexOf("pending = next;", duplicateGuard, StringComparison.Ordinal);
        int consumeMethod = lease.IndexOf("internal static Lease Consume(TemplateTable templates, TemplateItem source)", StringComparison.Ordinal);
        int consumeLock = consumeMethod < 0 ? -1 : lease.IndexOf("lock (Sync)", consumeMethod, StringComparison.Ordinal);
        int consumeLease = consumeLock < 0 ? -1 : lease.IndexOf("Lease lease = pending", consumeLock, StringComparison.Ordinal);
        int consumeProof = consumeLease < 0 ? -1 : lease.IndexOf("lease.RequireCurrent(templates, source);", consumeLease, StringComparison.Ordinal);
        int consumeClear = consumeProof < 0 ? -1 : lease.IndexOf("pending = null;", consumeProof, StringComparison.Ordinal);
        int consumeReturn = consumeClear < 0 ? -1 : lease.IndexOf("return lease;", consumeClear, StringComparison.Ordinal);
        int cancelMethod = lease.IndexOf("internal static void CancelPending(TemplateItem source)", StringComparison.Ordinal);
        int cancelLock = cancelMethod < 0 ? -1 : lease.IndexOf("lock (Sync)", cancelMethod, StringComparison.Ordinal);
        int cancelLease = cancelLock < 0 ? -1 : lease.IndexOf("Lease lease = pending", cancelLock, StringComparison.Ordinal);
        int cancelIdentity = cancelLease < 0 ? -1 : lease.IndexOf("ReferenceEquals(lease.Source, source)", cancelLease, StringComparison.Ordinal);
        int cancelClear = cancelIdentity < 0 ? -1 : lease.IndexOf("pending = null;", cancelIdentity, StringComparison.Ordinal);
        if (publishMethod < 0 || captureNext < 0 || publishLock < 0 || duplicateGuard < 0 || pendingAssignment < 0
            || consumeMethod < 0 || consumeLock < 0 || consumeLease < 0 || consumeProof < 0 || consumeClear < 0 || consumeReturn < 0
            || cancelMethod < 0 || cancelLock < 0 || cancelLease < 0 || cancelIdentity < 0 || cancelClear < 0
            || !(publishMethod < captureNext && captureNext < publishLock && publishLock < duplicateGuard
                 && duplicateGuard < pendingAssignment && pendingAssignment < consumeMethod
                 && consumeMethod < consumeLock && consumeLock < consumeLease && consumeLease < consumeProof
                 && consumeProof < consumeClear && consumeClear < consumeReturn
                 && consumeReturn < cancelMethod && cancelMethod < cancelLock && cancelLock < cancelLease
                 && cancelLease < cancelIdentity && cancelIdentity < cancelClear))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: authority must serialize publish/consume and permit only exact-source cancellation rollback.");
        if (lease.IndexOf("pending = next;", pendingAssignment + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: canonical authority must have exactly one publication assignment.");
        if (lease.IndexOf("pending = null;", cancelClear + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: canonical authority must not be cleared outside successful Consume or exact-source cancellation rollback.");
        if (lease.IndexOf("pending = null;", consumeMethod, StringComparison.Ordinal) < consumeProof)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: Consume must never clear pending authority before exact source reproof.");
        if (lease.IndexOf("pending = null;", cancelMethod, StringComparison.Ordinal) < cancelIdentity)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: cancellation rollback must never clear pending authority before exact source-reference proof.");

        int sourceValidation = item.IndexOf("source filters are empty, admit a B&A&HB-owned product", StringComparison.Ordinal);
        int consume = item.IndexOf("DogtagCaseCanonicalIdentityLease.Consume(templateTable, source);", StringComparison.Ordinal);
        int copiedFilters = item.IndexOf("var copiedFilters = sourceFilters", StringComparison.Ordinal);
        int create = item.IndexOf("customItemService.CreateItemFromClone(details);", StringComparison.Ordinal);
        if (sourceValidation < 0 || consume < 0 || copiedFilters < 0 || create < 0
            || !(sourceValidation < consume && consume < copiedFilters && copiedFilters < create))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: Preload +3 must consume exact +2 authority before copying filters and before product creation.");

        int beforeCreate = item.LastIndexOf("canonicalLease.RequireCurrent(templateTable, source);", create, StringComparison.Ordinal);
        int afterCreate = item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", create + 1, StringComparison.Ordinal);
        int newHostCommit = item.IndexOf("CommitDogtagSlotExposure(dogtagHost, CancellationToken.None);", create + 1, StringComparison.Ordinal);
        int afterNewHostCommit = newHostCommit < 0 ? -1 : item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", newHostCommit + 1, StringComparison.Ordinal);
        if (beforeCreate < 0 || afterCreate < 0 || newHostCommit < 0 || afterNewHostCommit < 0
            || !(beforeCreate < create && create < afterCreate && afterCreate < newHostCommit && newHostCommit < afterNewHostCommit))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: new-product creation/publication must be bracketed by exact +2 source identity/content reproof.");

        int existing = item.IndexOf("if (templateTable.Items.TryGetValue(DogtagCaseTpl, out var existing))", StringComparison.Ordinal);
        int existingHostCommit = existing < 0 ? -1 : item.IndexOf("CommitDogtagSlotExposure(dogtagHost, cancellationToken);", existing, StringComparison.Ordinal);
        int beforeExistingHost = existingHostCommit < 0 ? -1 : item.LastIndexOf("canonicalLease.RequireCurrent(templateTable, source);", existingHostCommit, StringComparison.Ordinal);
        int afterExistingHost = existingHostCommit < 0 ? -1 : item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", existingHostCommit + 1, StringComparison.Ordinal);
        if (existing < 0 || existingHostCommit < 0 || beforeExistingHost < existing || afterExistingHost < 0)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: retained-product host publication must remain bracketed by exact +2 source identity/content reproof.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "server", "DogtagCaseCanonicalIdentityLease.cs");
            if (File.Exists(candidate)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseCanonicalIdentityLease.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseCanonicalIdentityLease.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
