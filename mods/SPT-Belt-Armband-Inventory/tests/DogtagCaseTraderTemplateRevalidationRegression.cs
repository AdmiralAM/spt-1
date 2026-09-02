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
        Require(assort, "!ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)",
            "slot/filter identity reproof must first prove the revalidated live DefaultInventory still owns the captured Slots wrapper");
        Require(assort, "var liveSlots = slotsCollection",
            "slot/filter identity reproof must enumerate only the wrapper whose exact ownership was just re-proven against live DefaultInventory");
        Require(assort, "!ReferenceEquals(liveGroups[0], groups[0])",
            "replacement of the sole Dogtag filter-group object must fail closed even when it reuses the same filter set");
        Require(assort, "var assort = trader.Assort",
            "Ragman publication must capture the exact Assort wrapper after the initial template/host proof");
        Require(assort, "void RequireAssortWrapperIdentity()",
            "captured Ragman wrapper chain must be re-proven through publication");

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
        int liveSlotsWrapperProof = liveInventoryProof < 0
            ? -1
            : assort.IndexOf("!ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)", liveInventoryProof + 1, StringComparison.Ordinal);
        int liveSlotProof = liveSlotsWrapperProof < 0
            ? -1
            : assort.IndexOf("!ReferenceEquals(liveSlots[0], slot)", liveSlotsWrapperProof + 1, StringComparison.Ordinal);
        int liveGroupProof = liveSlotProof < 0
            ? -1
            : assort.IndexOf("!ReferenceEquals(liveGroups[0], groups[0])", liveSlotProof + 1, StringComparison.Ordinal);
        int liveFilterProof = liveGroupProof < 0
            ? -1
            : assort.IndexOf("!ReferenceEquals(liveGroups[0].Filter, hostFilter)", liveGroupProof + 1, StringComparison.Ordinal);
        if (committedHostProof < 0 || liveInventoryProof < 0 || liveSlotsWrapperProof < 0 || liveSlotProof < 0 || liveGroupProof < 0 || liveFilterProof < 0
            || !(committedHostProof < liveInventoryProof && liveInventoryProof < liveSlotsWrapperProof
                && liveSlotsWrapperProof < liveSlotProof && liveSlotProof < liveGroupProof && liveGroupProof < liveFilterProof))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: committed snapshot proof must be followed by live DefaultInventory ownership of the pinned Slots wrapper, then slot, sole filter-group and filter-set reference-identity reproof.");

        int loyaltyAdd = assort.IndexOf("loyalLevelItems.Add(id, LoyaltyLevel);", StringComparison.Ordinal);
        int postMutationCancellation = loyaltyAdd < 0
            ? -1
            : assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", loyaltyAdd + 1, StringComparison.Ordinal);
        int postMutationWrapperProof = postMutationCancellation < 0
            ? -1
            : assort.IndexOf("RequireAssortWrapperIdentity();", postMutationCancellation + 1, StringComparison.Ordinal);
        int committedOfferProof = postMutationWrapperProof < 0
            ? -1
            : assort.IndexOf("ValidateExisting(trader, id, offer, templateId);", postMutationWrapperProof + 1, StringComparison.Ordinal);
        int secondBoundary = committedOfferProof < 0
            ? -1
            : assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", committedOfferProof + 1, StringComparison.Ordinal);
        int tupleProof = secondBoundary < 0
            ? -1
            : assort.IndexOf("RequirePublishedAssortTupleIdentity(trader, id, offer, barter);", secondBoundary + 1, StringComparison.Ordinal);
        int finalWrapperProof = tupleProof < 0
            ? -1
            : assort.IndexOf("RequireAssortWrapperIdentity();", tupleProof + 1, StringComparison.Ordinal);
        int postBoundaryCancellation = finalWrapperProof < 0
            ? -1
            : assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", finalWrapperProof + 1, StringComparison.Ordinal);
        int catchBlock = postBoundaryCancellation < 0
            ? -1
            : assort.IndexOf("catch", postBoundaryCancellation + 1, StringComparison.Ordinal);
        if (loyaltyAdd < 0 || postMutationCancellation < 0 || postMutationWrapperProof < 0 || committedOfferProof < 0
            || secondBoundary < 0 || tupleProof < 0 || finalWrapperProof < 0 || postBoundaryCancellation < 0 || catchBlock < 0
            || !(loyaltyAdd < postMutationCancellation && postMutationCancellation < postMutationWrapperProof
                && postMutationWrapperProof < committedOfferProof && committedOfferProof < secondBoundary
                && secondBoundary < tupleProof && tupleProof < finalWrapperProof
                && finalWrapperProof < postBoundaryCancellation && postBoundaryCancellation < catchBlock))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: post-commit template/host/tuple/wrapper proofs and cancellation must remain inside captured-wrapper rollback ownership.");

        int ownsItemProof = assort.IndexOf("bool ownsItem = ownedItemIndex >= 0;", catchBlock, StringComparison.Ordinal);
        int ownsBarterProof = ownsItemProof < 0
            ? -1
            : assort.IndexOf("bool ownsBarter = barterAdded", ownsItemProof + 1, StringComparison.Ordinal);
        int loyaltyRollback = ownsBarterProof < 0
            ? -1
            : assort.IndexOf("loyalLevelItems.Remove(id);", ownsBarterProof + 1, StringComparison.Ordinal);
        int barterRollback = loyaltyRollback < 0
            ? -1
            : assort.IndexOf("barterScheme.Remove(id);", loyaltyRollback + 1, StringComparison.Ordinal);
        int itemRollback = barterRollback < 0
            ? -1
            : assort.IndexOf("items.RemoveAt(ownedItemIndex);", barterRollback + 1, StringComparison.Ordinal);
        if (ownsItemProof < 0 || ownsBarterProof < 0 || loyaltyRollback < 0 || barterRollback < 0 || itemRollback < 0
            || !(catchBlock < ownsItemProof && ownsItemProof < ownsBarterProof && ownsBarterProof < loyaltyRollback
                && loyaltyRollback < barterRollback && barterRollback < itemRollback))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: captured-wrapper rollback must prove item/barter ownership before loyalty -> barter -> item removal.");
        Require(assort, "ReferenceEquals(items[i], offer)",
            "rollback must prove exact offer reference ownership in the captured Items wrapper");
        Require(assort, "ReferenceEquals(currentBarter, barter)",
            "rollback must prove exact barter reference ownership in the captured BarterScheme wrapper");
        Require(assort, "if (loyaltyAdded && ownsItem && ownsBarter",
            "value-only loyalty metadata may be removed only while both reference-owned tuple components remain ours");

        int existingProof = assort.IndexOf("ValidateExisting(trader, id, existing, templateId);", StringComparison.Ordinal);
        int existingWrapperProof = existingProof < 0
            ? -1
            : assort.IndexOf("RequireAssortWrapperIdentity();", existingProof + 1, StringComparison.Ordinal);
        int existingBoundary = existingWrapperProof < 0
            ? -1
            : assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", existingWrapperProof + 1, StringComparison.Ordinal);
        int existingTupleProof = existingBoundary < 0
            ? -1
            : assort.IndexOf("RequirePublishedAssortTupleIdentity(trader, id, existing, existingBarter);", existingBoundary + 1, StringComparison.Ordinal);
        int existingFinalWrapper = existingTupleProof < 0
            ? -1
            : assort.IndexOf("RequireAssortWrapperIdentity();", existingTupleProof + 1, StringComparison.Ordinal);
        int existingCancellation = existingFinalWrapper < 0
            ? -1
            : assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", existingFinalWrapper + 1, StringComparison.Ordinal);
        int existingSuccess = assort.IndexOf("retained validated Ragman", StringComparison.Ordinal);
        if (existingProof < 0 || existingWrapperProof < 0 || existingBoundary < 0 || existingTupleProof < 0
            || existingFinalWrapper < 0 || existingCancellation < 0 || existingSuccess < 0
            || !(existingProof < existingWrapperProof && existingWrapperProof < existingBoundary
                && existingBoundary < existingTupleProof && existingTupleProof < existingFinalWrapper
                && existingFinalWrapper < existingCancellation && existingCancellation < existingSuccess))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: pre-existing offer success must be preceded by captured-wrapper, live template/host, tuple and cancellation reproof.");

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
