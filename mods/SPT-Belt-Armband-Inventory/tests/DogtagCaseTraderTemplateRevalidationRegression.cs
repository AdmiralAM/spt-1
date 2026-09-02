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
        Require(item, "Width = sourceProperties.Width,", "Dogtag clone must preserve canonical root width");
        Require(item, "Height = sourceProperties.Height,", "Dogtag clone must preserve canonical root height");
        Require(item, "StackMaxSize = sourceProperties.StackMaxSize,", "Dogtag clone must preserve canonical root stack policy");
        Require(item, "candidateProperties.Width != sourceProperties.Width", "live publication must reject root-width drift");
        Require(item, "candidateProperties.Height != sourceProperties.Height", "live publication must reject root-height drift");
        Require(item, "candidateProperties.StackMaxSize != sourceProperties.StackMaxSize", "live publication must reject root stack-policy drift");

        Require(assort, "private static void RequirePublicationBoundary(TemplateTable templateTable, MongoId templateId)",
            "Ragman publication must centralize canonical-template and committed-host parity");
        Require(assort, "DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);",
            "publication boundary must revalidate the live Dogtag Case template");
        Require(assort, "RequireExactDogtagHost(templateTable, templateId);",
            "publication boundary must prove the committed vanilla Dogtag host");
        Require(assort, "var assort = trader.Assort", "Ragman publication must capture exact Assort");
        Require(assort, "var items = assort.Items", "Ragman publication must capture exact Items wrapper");
        Require(assort, "var barterScheme = assort.BarterScheme", "Ragman publication must capture exact BarterScheme wrapper");
        Require(assort, "var loyalLevelItems = assort.LoyalLevelItems", "Ragman publication must capture exact LoyalLevelItems wrapper");
        Require(assort, "void RequireAssortWrapperIdentity()", "captured wrapper chain must be re-proven through publication");

        int firstBoundary = assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", StringComparison.Ordinal);
        int traderLookup = assort.IndexOf("tradersTable.GetValueOrDefault", StringComparison.Ordinal);
        int firstCancellation = firstBoundary < 0 ? -1 : assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", firstBoundary + 1, StringComparison.Ordinal);
        if (firstBoundary < 0 || firstCancellation <= firstBoundary || traderLookup <= firstCancellation)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: initial template/host proof and cancellation must complete before Ragman state is touched.");

        int committedHostProof = assort.IndexOf("DogtagCaseHostContract.RequireCommitted(hostFilter);", StringComparison.Ordinal);
        int liveInventoryProof = assort.IndexOf("!ReferenceEquals(liveInventory, inventory)", committedHostProof + 1, StringComparison.Ordinal);
        int liveSlotsWrapperProof = assort.IndexOf("!ReferenceEquals(liveInventory.Properties?.Slots, slotsCollection)", liveInventoryProof + 1, StringComparison.Ordinal);
        int liveSlotProof = assort.IndexOf("!ReferenceEquals(liveSlots[0], slot)", liveSlotsWrapperProof + 1, StringComparison.Ordinal);
        int liveGroupProof = assort.IndexOf("!ReferenceEquals(liveGroups[0], groups[0])", liveSlotProof + 1, StringComparison.Ordinal);
        int liveFilterProof = assort.IndexOf("!ReferenceEquals(liveGroups[0].Filter, hostFilter)", liveGroupProof + 1, StringComparison.Ordinal);
        if (committedHostProof < 0 || liveInventoryProof <= committedHostProof || liveSlotsWrapperProof <= liveInventoryProof
            || liveSlotProof <= liveSlotsWrapperProof || liveGroupProof <= liveSlotProof || liveFilterProof <= liveGroupProof)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: committed host proof must be followed by complete pinned wrapper identity reproof.");

        int loyaltyAdd = assort.IndexOf("loyalLevelItems.Add(id, LoyaltyLevel);", StringComparison.Ordinal);
        int postMutationCancellation = assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", loyaltyAdd + 1, StringComparison.Ordinal);
        int postMutationWrapperProof = assort.IndexOf("RequireAssortWrapperIdentity();", postMutationCancellation + 1, StringComparison.Ordinal);
        int committedOfferProof = assort.IndexOf("ValidateExisting(items, barterScheme, loyalLevelItems, id, offer, templateId);", postMutationWrapperProof + 1, StringComparison.Ordinal);
        int secondBoundary = assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", committedOfferProof + 1, StringComparison.Ordinal);
        int tupleProof = assort.IndexOf("RequirePublishedAssortTupleIdentity(items, barterScheme, loyalLevelItems, id, offer, barter);", secondBoundary + 1, StringComparison.Ordinal);
        int finalWrapperProof = assort.IndexOf("RequireAssortWrapperIdentity();", tupleProof + 1, StringComparison.Ordinal);
        int postBoundaryCancellation = assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", finalWrapperProof + 1, StringComparison.Ordinal);
        int catchBlock = assort.IndexOf("catch", postBoundaryCancellation + 1, StringComparison.Ordinal);
        if (loyaltyAdd < 0 || postMutationCancellation <= loyaltyAdd || postMutationWrapperProof <= postMutationCancellation
            || committedOfferProof <= postMutationWrapperProof || secondBoundary <= committedOfferProof || tupleProof <= secondBoundary
            || finalWrapperProof <= tupleProof || postBoundaryCancellation <= finalWrapperProof || catchBlock <= postBoundaryCancellation)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: new-offer proof ordering drifted outside captured-wrapper rollback ownership.");

        int ownsItemProof = assort.IndexOf("bool ownsItem = ownedItemIndex >= 0;", catchBlock, StringComparison.Ordinal);
        int ownsBarterProof = assort.IndexOf("bool ownsBarter = barterAdded", ownsItemProof + 1, StringComparison.Ordinal);
        int loyaltyRollback = assort.IndexOf("loyalLevelItems.Remove(id);", ownsBarterProof + 1, StringComparison.Ordinal);
        int barterRollback = assort.IndexOf("barterScheme.Remove(id);", loyaltyRollback + 1, StringComparison.Ordinal);
        int itemRollback = assort.IndexOf("items.RemoveAt(ownedItemIndex);", barterRollback + 1, StringComparison.Ordinal);
        if (ownsItemProof <= catchBlock || ownsBarterProof <= ownsItemProof || loyaltyRollback <= ownsBarterProof
            || barterRollback <= loyaltyRollback || itemRollback <= barterRollback)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: rollback ownership/order drifted.");
        Require(assort, "ReferenceEquals(items[i], offer)", "rollback must prove exact offer reference ownership in captured Items");
        Require(assort, "ReferenceEquals(currentBarter, barter)", "rollback must prove exact barter reference ownership in captured BarterScheme");
        Require(assort, "if (loyaltyAdded && ownsItem && ownsBarter", "loyalty rollback requires both reference-owned tuple components");

        int existingProof = assort.IndexOf("ValidateExisting(items, barterScheme, loyalLevelItems, id, existing, templateId);", StringComparison.Ordinal);
        int existingWrapperProof = assort.IndexOf("RequireAssortWrapperIdentity();", existingProof + 1, StringComparison.Ordinal);
        int existingBoundary = assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", existingWrapperProof + 1, StringComparison.Ordinal);
        int existingTupleProof = assort.IndexOf("RequirePublishedAssortTupleIdentity(items, barterScheme, loyalLevelItems, id, existing, existingBarter);", existingBoundary + 1, StringComparison.Ordinal);
        int existingFinalWrapper = assort.IndexOf("RequireAssortWrapperIdentity();", existingTupleProof + 1, StringComparison.Ordinal);
        int existingCancellation = assort.IndexOf("cancellationToken.ThrowIfCancellationRequested();", existingFinalWrapper + 1, StringComparison.Ordinal);
        int existingSuccess = assort.IndexOf("retained validated Ragman", StringComparison.Ordinal);
        if (existingProof < 0 || existingWrapperProof <= existingProof || existingBoundary <= existingWrapperProof
            || existingTupleProof <= existingBoundary || existingFinalWrapper <= existingTupleProof
            || existingCancellation <= existingFinalWrapper || existingSuccess <= existingCancellation)
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: retained-offer success lacks captured-wrapper/template/host/tuple/cancellation reproof.");

        if (assort.Contains("RollbackOwnedAssortTuple(", StringComparison.Ordinal)
            || assort.Contains("ValidateExisting(trader,", StringComparison.Ordinal)
            || assort.Contains("RequirePublishedAssortTupleIdentity(trader,", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag trader template revalidation regression failed: stale direct trader.Assort execution path was restored.");
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
