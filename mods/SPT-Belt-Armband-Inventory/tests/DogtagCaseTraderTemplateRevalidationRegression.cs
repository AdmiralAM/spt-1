using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseTraderTemplateRevalidationRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: module root could not be resolved.");

        string item = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseItem.cs"));
        string assort = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs"));

        Require(item, "public static void RequireCanonicalRegisteredTemplate(TemplateTable templates)",
            "Dogtag product must expose one reusable canonical live-template verifier");
        Require(item, "ValidateExisting(candidate, source);",
            "publication verifier must reuse the exact preload canonical geometry/filter contract");
        Require(item, "Width = sourceProperties.Width,",
            "Dogtag clone must explicitly preserve canonical root width");
        Require(item, "Height = sourceProperties.Height,",
            "Dogtag clone must explicitly preserve canonical root height");
        Require(item, "StackMaxSize = sourceProperties.StackMaxSize,",
            "Dogtag clone must explicitly preserve canonical root stack policy");
        Require(item, "candidateProperties.Width != sourceProperties.Width",
            "live publication revalidation must reject root-width drift");
        Require(item, "candidateProperties.Height != sourceProperties.Height",
            "live publication revalidation must reject root-height drift");
        Require(item, "candidateProperties.StackMaxSize != sourceProperties.StackMaxSize",
            "live publication revalidation must reject root stack-policy drift");
        Require(assort, "private static void RequirePublicationBoundary(TemplateTable templateTable, MongoId templateId)",
            "Ragman publication must centralize canonical-template and committed-host parity");
        Require(assort, "DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);",
            "publication boundary must revalidate the live Dogtag Case template");
        Require(assort, "RequireExactDogtagHost(templateTable, templateId);",
            "publication boundary must prove the committed vanilla Dogtag host");
        Require(assort, "templateTable.Items.TryGetValue(RuntimeCandidateBeltItem.DefaultInventoryTpl, out var liveInventory)",
            "committed-host publication must re-resolve the live DefaultInventory template after the point-in-time host proof");
        Require(assort, "!ReferenceEquals(liveInventory, inventory)",
            "replacement of the DefaultInventory template object during host proof must fail closed");
        Require(assort, "var liveSlots = liveInventory.Properties?.Slots?",
            "slot/filter identity reproof must resolve from the revalidated live DefaultInventory object rather than a detached stale capture");

        int firstBoundary = assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", StringComparison.Ordinal);
        int traderLookup = assort.IndexOf("tradersTable.GetValueOrDefault", StringComparison.Ordinal);
        if (firstBoundary < 0 || traderLookup < 0 || firstBoundary >= traderLookup)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: template + host proof must complete before Ragman publication state is touched.");

        int firstPostBoundaryCancellation = firstBoundary < 0
            ? -1
            : assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", firstBoundary + 1, StringComparison.Ordinal);
        if (firstPostBoundaryCancellation < 0 || traderLookup < 0 || firstPostBoundaryCancellation >= traderLookup)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: cancellation must be re-observed after the initial product/host proof and before Ragman state is touched.");

        int committedHostProof = assort.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", StringComparison.Ordinal);
        int liveInventoryProof = committedHostProof < 0
            ? -1
            : assort.IndexOf("!ReferenceEquals(liveInventory, inventory)", committedHostProof + 1, StringComparison.Ordinal);
        int liveSlotProof = liveInventoryProof < 0
            ? -1
            : assort.IndexOf("!ReferenceEquals(liveSlots[0], slot)", liveInventoryProof + 1, StringComparison.Ordinal);
        int liveFilterProof = liveSlotProof < 0
            ? -1
            : assort.IndexOf("!ReferenceEquals(liveGroups[0].Filter, hostFilter)", liveSlotProof + 1, StringComparison.Ordinal);
        if (committedHostProof < 0 || liveInventoryProof < 0 || liveSlotProof < 0 || liveFilterProof < 0
            || !(committedHostProof < liveInventoryProof && liveInventoryProof < liveSlotProof && liveSlotProof < liveFilterProof))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: committed snapshot proof must be followed by live DefaultInventory, slot, then filter reference-identity reproof.");

        int committedOfferProof = assort.IndexOf("ValidateExisting(trader, id, offer, templateId);", StringComparison.Ordinal);
        int secondBoundary = committedOfferProof < 0
            ? -1
            : assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", committedOfferProof + 1, StringComparison.Ordinal);
        int rollbackCall = secondBoundary < 0
            ? -1
            : assort.IndexOf("RollbackOwnedAssortTuple(trader, id, offer, barter, itemAdded, barterAdded, loyaltyAdded);", secondBoundary + 1, StringComparison.Ordinal);
        if (committedOfferProof < 0 || secondBoundary < 0 || rollbackCall < 0 || !(committedOfferProof < secondBoundary && secondBoundary < rollbackCall))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: post-commit template + host proof must remain inside the owned assort rollback boundary.");

        int loyaltyAdd = assort.IndexOf("trader.Assort.LoyalLevelItems.Add(id, LoyaltyLevel);", StringComparison.Ordinal);
        int postMutationCancellation = loyaltyAdd < 0
            ? -1
            : assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", loyaltyAdd + 1, StringComparison.Ordinal);
        int postBoundaryCancellation = secondBoundary < 0
            ? -1
            : assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", secondBoundary + 1, StringComparison.Ordinal);
        if (loyaltyAdd < 0 || postMutationCancellation < 0 || secondBoundary < 0 || postBoundaryCancellation < 0 || rollbackCall < 0
            || !(loyaltyAdd < postMutationCancellation
                && postMutationCancellation < committedOfferProof
                && secondBoundary < postBoundaryCancellation
                && postBoundaryCancellation < rollbackCall))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: cancellation after owned assort mutation and after final publication proof must remain inside rollback ownership.");

        int rollbackHelper = assort.IndexOf("private static void RollbackOwnedAssortTuple", StringComparison.Ordinal);
        if (rollbackHelper < 0 || rollbackHelper <= rollbackCall)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: owned rollback call must resolve to the explicit replacement-safe helper.");
        Require(assort, "bool ownsItem = ownedItemIndex >= 0;",
            "rollback helper must prove exact offer reference ownership");
        Require(assort, "bool ownsBarter = barterAdded",
            "rollback helper must prove exact barter reference ownership");
        Require(assort, "if (loyaltyAdded && ownsItem && ownsBarter",
            "value-only loyalty metadata may be removed only while both reference-owned tuple components remain ours");

        int existingProof = assort.IndexOf("ValidateExisting(trader, id, existing, templateId);", StringComparison.Ordinal);
        int existingBoundary = existingProof < 0
            ? -1
            : assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", existingProof + 1, StringComparison.Ordinal);
        int existingCancellation = existingBoundary < 0
            ? -1
            : assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", existingBoundary + 1, StringComparison.Ordinal);
        int existingSuccess = assort.IndexOf("retained validated Ragman", StringComparison.Ordinal);
        if (existingProof < 0 || existingBoundary < 0 || existingCancellation < 0 || existingSuccess < 0
            || !(existingProof < existingBoundary && existingBoundary < existingCancellation && existingCancellation < existingSuccess))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: pre-existing offer success must be preceded by fresh live template/host proof and cancellation observation.");

        if (assort.Contains("templateTable.Items.ContainsKey(templateId)", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: existence-only template gating was restored.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string item = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            string assort = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(item) && File.Exists(assort)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string directItem = Path.Combine(current.FullName, "server", "DogtagCaseItem.cs");
            string directAssort = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(directItem) && File.Exists(directAssort)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseItem.cs"))
                && File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: " + message + ".");
    }
}
