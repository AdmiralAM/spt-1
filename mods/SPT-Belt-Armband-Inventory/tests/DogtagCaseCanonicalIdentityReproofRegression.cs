using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseCanonicalIdentityReproofRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag canonical identity reproof regression failed: module root could not be resolved.");

        string item = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));
        string assort = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs"));

        int verifier = item.IndexOf("public static void RequireCanonicalRegisteredTemplate(TemplateTable templates)", StringComparison.Ordinal);
        int validate = verifier < 0 ? -1 : item.IndexOf("ValidateExisting(candidate, source);", verifier, StringComparison.Ordinal);
        int liveSource = validate < 0 ? -1 : item.IndexOf("templates.Items.TryGetValue(SourceDogtagCaseTpl, out var liveSource)", validate + 1, StringComparison.Ordinal);
        int sourceIdentity = liveSource < 0 ? -1 : item.IndexOf("!ReferenceEquals(liveSource, source)", liveSource, StringComparison.Ordinal);
        int liveCandidate = sourceIdentity < 0 ? -1 : item.IndexOf("templates.Items.TryGetValue(DogtagCaseTpl, out var liveCandidate)", sourceIdentity + 1, StringComparison.Ordinal);
        int candidateIdentity = liveCandidate < 0 ? -1 : item.IndexOf("!ReferenceEquals(liveCandidate, candidate)", liveCandidate, StringComparison.Ordinal);
        int finalValueProof = candidateIdentity < 0 ? -1 : item.IndexOf("ValidateExisting(liveCandidate, liveSource);", candidateIdentity + 1, StringComparison.Ordinal);

        if (verifier < 0 || validate < 0 || liveSource < 0 || sourceIdentity < 0 || liveCandidate < 0 || candidateIdentity < 0 || finalValueProof < 0
            || !(verifier < validate && validate < liveSource && liveSource <= sourceIdentity
                && sourceIdentity < liveCandidate && liveCandidate <= candidateIdentity && candidateIdentity < finalValueProof))
            throw new InvalidOperationException("Dogtag canonical identity reproof regression failed: canonical value validation must be followed by live source/product reference-identity reproof and a final same-reference value proof.");

        int existingBranch = item.IndexOf("if (templateTable.Items.TryGetValue(DogtagCaseTpl, out var existing))", StringComparison.Ordinal);
        int existingValueProof = existingBranch < 0 ? -1 : item.IndexOf("ValidateExisting(existing, source);", existingBranch, StringComparison.Ordinal);
        int existingProof = existingValueProof < 0 ? -1 : item.IndexOf("RequireCanonicalRegisteredTemplate(templateTable);", existingValueProof, StringComparison.Ordinal);
        int firstHostCommit = existingProof < 0 ? -1 : item.IndexOf("CommitDogtagSlotExposure(dogtagHost, cancellationToken);", existingProof, StringComparison.Ordinal);
        if (existingBranch < 0 || existingValueProof < 0 || existingProof < 0 || firstHostCommit < 0
            || !(existingBranch < existingValueProof && existingValueProof < existingProof && existingProof < firstHostCommit))
            throw new InvalidOperationException("Dogtag canonical identity reproof regression failed: retained-template host exposure must follow explicit value validation and fresh canonical reference/value proof.");

        int create = item.IndexOf("customItemService.CreateItemFromClone(details);", StringComparison.Ordinal);
        int createdValueProof = create < 0 ? -1 : item.IndexOf("ValidateExisting(created, source);", create + 1, StringComparison.Ordinal);
        int createdProof = createdValueProof < 0 ? -1 : item.IndexOf("RequireCanonicalRegisteredTemplate(templateTable);", createdValueProof + 1, StringComparison.Ordinal);
        int createdHostCommit = createdProof < 0 ? -1 : item.IndexOf("CommitDogtagSlotExposure(dogtagHost, CancellationToken.None);", createdProof, StringComparison.Ordinal);
        if (create < 0 || createdValueProof < 0 || createdProof < 0 || createdHostCommit < 0
            || !(create < createdValueProof && createdValueProof < createdProof && createdProof < createdHostCommit))
            throw new InvalidOperationException("Dogtag canonical identity reproof regression failed: newly-created product must be value-validated, re-resolved and reference/value-proven before host exposure.");

        if (item.Contains("templates.Items[SourceDogtagCaseTpl]", StringComparison.Ordinal)
            || item.Contains("templates.Items[DogtagCaseTpl]", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag canonical identity reproof regression failed: publication reproof must remain bounded TryGetValue fail-closed lookup rather than indexer reads.");

        // Trader publication must retain reference identity for every structural
        // link in the live Dogtag host. Reusing the same HashSet behind a replaced
        // sole filter-group object is still a host replacement and must fail closed.
        int host = assort.IndexOf("internal static void RequireExactDogtagHost(TemplateTable templateTable, MongoId templateId)", StringComparison.Ordinal);
        int committed = host < 0 ? -1 : assort.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", host, StringComparison.Ordinal);
        int inventoryIdentity = committed < 0 ? -1 : assort.IndexOf("!ReferenceEquals(liveInventory, inventory)", committed, StringComparison.Ordinal);
        int slotIdentity = inventoryIdentity < 0 ? -1 : assort.IndexOf("!ReferenceEquals(liveSlots[0], slot)", inventoryIdentity, StringComparison.Ordinal);
        int groupIdentity = slotIdentity < 0 ? -1 : assort.IndexOf("!ReferenceEquals(liveGroups[0], groups[0])", slotIdentity, StringComparison.Ordinal);
        int filterIdentity = groupIdentity < 0 ? -1 : assort.IndexOf("!ReferenceEquals(liveGroups[0].Filter, hostFilter)", groupIdentity, StringComparison.Ordinal);
        int finalCommitted = filterIdentity < 0 ? -1 : assort.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", filterIdentity + 1, StringComparison.Ordinal);
        if (host < 0 || committed < 0 || inventoryIdentity < 0 || slotIdentity < 0 || groupIdentity < 0 || filterIdentity < 0 || finalCommitted < 0
            || !(host < committed && committed < inventoryIdentity && inventoryIdentity < slotIdentity
                && slotIdentity < groupIdentity && groupIdentity < filterIdentity && filterIdentity < finalCommitted))
            throw new InvalidOperationException("Dogtag canonical identity reproof regression failed: trader publication must bracket live DefaultInventory/Dogtag slot/filter-group/filter reference identity with committed content proofs.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string candidate = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(candidate)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseItem.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }
}
