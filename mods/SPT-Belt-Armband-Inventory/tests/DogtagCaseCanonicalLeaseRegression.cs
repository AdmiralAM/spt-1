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
        if (preflightIdentity < 0 || publish < 0 || publish <= preflightIdentity)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: Preload +2 must publish the lease only after its final exact canonical identity proof.");

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
            "ReferenceEquals(groups[i].ExcludedFilter, Excluded[i])"
        };
        foreach (string pin in requiredLeasePins)
            if (!lease.Contains(pin, StringComparison.Ordinal))
                throw new InvalidOperationException("Dogtag canonical lease regression failed: missing exact source identity pin: " + pin);

        if (!lease.Contains("pending = null;", StringComparison.Ordinal)
            || !lease.Contains("lease.RequireCurrent(templates, source);", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag canonical lease regression failed: lease must be single-consumer and immediately re-proven during consumption.");

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
            throw new InvalidOperationException("Dogtag canonical lease regression failed: new-product creation/publication must be bracketed by exact +2 source identity reproof.");

        int existing = item.IndexOf("if (templateTable.Items.TryGetValue(DogtagCaseTpl, out var existing))", StringComparison.Ordinal);
        int existingHostCommit = existing < 0 ? -1 : item.IndexOf("CommitDogtagSlotExposure(dogtagHost, cancellationToken);", existing, StringComparison.Ordinal);
        int beforeExistingHost = existingHostCommit < 0 ? -1 : item.LastIndexOf("canonicalLease.RequireCurrent(templateTable, source);", existingHostCommit, StringComparison.Ordinal);
        int afterExistingHost = existingHostCommit < 0 ? -1 : item.IndexOf("canonicalLease.RequireCurrent(templateTable, source);", existingHostCommit + 1, StringComparison.Ordinal);
        if (existing < 0 || existingHostCommit < 0 || beforeExistingHost < existing || afterExistingHost < 0)
            throw new InvalidOperationException("Dogtag canonical lease regression failed: retained-product host publication must remain bracketed by exact +2 source identity reproof.");
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
