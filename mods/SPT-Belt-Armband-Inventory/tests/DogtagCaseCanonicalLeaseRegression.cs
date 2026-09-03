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

        string[] requiredLeasePins =
        {
            "ReferenceEquals(Source, expectedSource)", "ReferenceEquals(liveSource, Source)",
            "ReferenceEquals(liveSource.Properties, Properties)", "ReferenceEquals(liveSource.Properties?.Grids, GridsCollection)",
            "ReferenceEquals(grids[0], Grid)", "ReferenceEquals(grids[0].Properties, GridProperties)",
            "ReferenceEquals(grids[0].Properties?.Filters, FiltersCollection)", "ReferenceEquals(groups[i], Groups[i])",
            "ReferenceEquals(groups[i].Filter, Included[i])", "ReferenceEquals(groups[i].ExcludedFilter, Excluded[i])",
            "included.SetEquals(IncludedValues[i])", "excluded.SetEquals(expectedExcluded)",
            "included.Any(id => PersistentIdentityManifest.IsOwnedTemplate(id.ToString()))",
            "Equals(liveSource.Parent, SourceParent)", "Equals(liveSource.Properties?.BackgroundColor, BackgroundColor)",
            "Equals(liveSource.Properties?.ExaminedByDefault, ExaminedByDefault)", "Equals(liveSource.Properties?.Width, Width)",
            "Equals(liveSource.Properties?.Height, Height)", "Equals(liveSource.Properties?.StackMaxSize, StackMaxSize)",
            "Equals(grids[0].Name, GridName)", "Equals(grids[0].Id, GridId)", "Equals(grids[0].Parent, GridParent)",
            "Equals(grids[0].Prototype, GridPrototype)", "Equals(grids[0].Properties?.CellsH, CellsH)",
            "Equals(grids[0].Properties?.CellsV, CellsV)", "Equals(grids[0].Properties?.MinCount, MinCount)",
            "Equals(grids[0].Properties?.MaxCount, MaxCount)", "Equals(grids[0].Properties?.MaxWeight, MaxWeight)",
            "Equals(grids[0].Properties?.IsSortingTable, IsSortingTable)"
        };
        foreach (string pin in requiredLeasePins)
            if (!lease.Contains(pin, StringComparison.Ordinal))
                throw new InvalidOperationException("Dogtag canonical lease regression failed: missing exact source identity/content/value pin: " + pin);

        string[] requiredCapturedValues =
        {
            "source.Parent,", "properties.BackgroundColor,", "properties.ExaminedByDefault,", "properties.Width,", "properties.Height,", "properties.StackMaxSize,",
            "grid.Name,", "grid.Id,", "grid.Parent,", "grid.Prototype,", "gridProperties.CellsH,", "gridProperties.CellsV,",
            "gridProperties.MinCount,", "gridProperties.MaxCount,", "gridProperties.MaxWeight,", "gridProperties.IsSortingTable"
        };
        foreach (string value in requiredCapturedValues)
            if (!lease.Contains(value, StringComparison.Ordinal))
                throw new InvalidOperationException("Dogtag canonical lease regression failed: +2 lease is missing detached scalar canonical value capture: " + value);

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
        int consumeClear = consumeLease < 0 ? -1 : lease.IndexOf("pending = null;", consumeLease, StringComparison.Ordinal);
        int consumeProof = consumeClear < 0 ? -1 : lease.IndexOf("lease.RequireCurrent(templates, source);", consumeClear, StringComparison.Ordinal);
        int consumeReturn = consumeProof < 0 ? -1 : lease.IndexOf("return lease;", consumeProof, StringComparison.Ordinal);
        int cancelMethod = lease.IndexOf("internal static void CancelPending(TemplateItem source)", StringComparison.Ordinal);
        int cancelLock = cancelMethod < 0 ? -1 : lease.IndexOf("lock (Sync)", cancelMethod, StringComparison.Ordinal);
        int cancelLease = cancelLock < 0 ? -1 : lease.IndexOf("Lease lease = pending", cancelLock, StringComparison.Ordinal);
        int cancelIdentity = cancelLease < 0 ? -1 : lease.IndexOf("ReferenceEquals(lease.Source, source)", cancelLease, StringComparison.Ordinal);
        int cancelClear = cancelIdentity < 0 ? -1 : lease.IndexOf("pending = null;", cancelIdentity, StringComparison.Ordinal);
        if (publishMethod < 0 || captureNext < 0 || publishLock < 0 || duplicateGuard < 0 || pendingAssignment < 0
            || consumeMethod < 0 || consumeLock < 0 || consumeLease < 0 || consumeClear < 0 || consumeProof < 0 || consumeReturn < 0
            || cancelMethod < 0 || cancelLock < 0 || cancelLease < 0 || cancelIdentity < 0 || cancelClear < 0)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: authority publication/consumption/cancellation shape is incomplete.");
        if (!(consumeLease < consumeClear && consumeClear < consumeProof && consumeProof < consumeReturn))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: Consume must burn pending authority before validating current state so drift/restore ABA cannot revive a challenged lease.");
        if (lease.IndexOf("pending = next;", pendingAssignment + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: canonical authority must have exactly one publication assignment.");
        if (lease.IndexOf("pending = null;", cancelClear + 1, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: canonical authority must not be cleared outside monotonic Consume or exact-source cancellation rollback.");

        int sourceValidation = item.IndexOf("source filters are empty, admit a B&A&HB-owned product", StringComparison.Ordinal);
        int consume = item.IndexOf("DogtagCaseCanonicalIdentityLease.Consume(templateTable, source);", StringComparison.Ordinal);
        int copiedFilters = item.IndexOf("var copiedFilters = sourceFilters", StringComparison.Ordinal);
        int create = item.IndexOf("customItemService.CreateItemFromClone(details);", StringComparison.Ordinal);
        if (sourceValidation < 0 || consume < 0 || copiedFilters < 0 || create < 0
            || !(sourceValidation < consume && consume < copiedFilters && copiedFilters < create))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: Preload +3 must consume exact +2 authority before copying canonical values and before product creation.");

        int beforeCreate = item.LastIndexOf("canonicalLease.RequireCurrent(templateTable, source);", create, StringComparison.Ordinal);
        int afterCreate = item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", create + 1, StringComparison.Ordinal);
        if (beforeCreate < 0 || afterCreate < 0 || !(beforeCreate < create && create < afterCreate))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: new-product creation must remain bracketed by exact leased source reproof.");
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
