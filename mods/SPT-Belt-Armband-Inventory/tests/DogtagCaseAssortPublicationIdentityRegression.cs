using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseAssortPublicationIdentityRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: module root could not be resolved.");

        string assort = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs"));

        Require(assort, "private static void RequirePublishedAssortTupleIdentity(",
            "Ragman publication must have an explicit retained-tuple identity proof");
        Require(assort, "ReferenceEquals(item, expectedItem)",
            "validated assort item must remain the exact same object reference");
        Require(assort, "!Equals(expectedItem.Template, new MongoId(RuntimeIdentity.DogtagCaseItemId))",
            "publication must revalidate exact Dogtag Case template after retained item-reference proof");
        Require(assort, "expectedItem.Upd.StackObjectsCount != UnlimitedStock",
            "publication must revalidate exact stock contract against in-place item mutation");
        Require(assort, "ReferenceEquals(liveBarter, expectedBarter)",
            "validated barter tuple must remain the exact same object reference");
        Require(assort, "liveBarter.Count != 1",
            "publication must revalidate outer barter cardinality against in-place mutation");
        Require(assort, "liveBarter[0].Count != 1",
            "publication must revalidate inner barter cardinality against in-place mutation");
        Require(assort, "!Equals(liveBarter[0][0].Template, Money.ROUBLES)",
            "publication must revalidate barter currency after retained-reference proof");
        Require(assort, "liveBarter[0][0].Count != PriceRoubles",
            "publication must revalidate exact price after retained-reference proof");
        Require(assort, "liveLoyalty != LoyaltyLevel",
            "loyalty metadata must be revalidated at the publication boundary");
        Require(assort, "idMatches > 1",
            "duplicate assort-ID publication must fail closed rather than selecting one item");

        Require(assort, "var assort = trader.Assort",
            "publication must capture the exact Ragman Assort wrapper");
        Require(assort, "var items = assort.Items",
            "publication must capture the exact Ragman Items wrapper");
        Require(assort, "var barterScheme = assort.BarterScheme",
            "publication must capture the exact Ragman BarterScheme wrapper");
        Require(assort, "var loyalLevelItems = assort.LoyalLevelItems",
            "publication must capture the exact Ragman LoyalLevelItems wrapper");
        Require(assort, "void RequireAssortWrapperIdentity()",
            "publication must have an exact wrapper-chain identity reproof");
        Require(assort, "!ReferenceEquals(trader.Assort, assort)",
            "Ragman Assort wrapper replacement must fail closed");
        Require(assort, "!ReferenceEquals(trader.Assort?.Items, items)",
            "Ragman Items wrapper replacement must fail closed");
        Require(assort, "!ReferenceEquals(trader.Assort?.BarterScheme, barterScheme)",
            "Ragman BarterScheme wrapper replacement must fail closed");
        Require(assort, "!ReferenceEquals(trader.Assort?.LoyalLevelItems, loyalLevelItems)",
            "Ragman LoyalLevelItems wrapper replacement must fail closed");

        int itemIdentity = assort.IndexOf("idMatches != 1 || exactItemMatches != 1", StringComparison.Ordinal);
        int itemContents = assort.IndexOf("!Equals(expectedItem.Template, new MongoId(RuntimeIdentity.DogtagCaseItemId))", StringComparison.Ordinal);
        int barterIdentity = assort.IndexOf("ReferenceEquals(liveBarter, expectedBarter)", StringComparison.Ordinal);
        int barterContents = assort.IndexOf("liveBarter.Count != 1", StringComparison.Ordinal);
        int loyaltyProof = assort.IndexOf("liveLoyalty != LoyaltyLevel", StringComparison.Ordinal);
        if (itemIdentity < 0 || itemContents < 0 || barterIdentity < 0 || barterContents < 0 || loyaltyProof < 0
            || !(itemIdentity < itemContents && itemContents < barterIdentity && barterIdentity < barterContents && barterContents < loyaltyProof))
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: publication ordering must remain item reference -> item contents -> barter reference -> barter contents -> loyalty.");

        int existingValidation = assort.IndexOf("ValidateExisting(trader, id, existing, templateId);", StringComparison.Ordinal);
        int existingWrapper = existingValidation < 0 ? -1
            : assort.IndexOf("RequireAssortWrapperIdentity();", existingValidation + 1, StringComparison.Ordinal);
        int existingBoundary = existingWrapper < 0 ? -1
            : assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", existingWrapper + 1, StringComparison.Ordinal);
        int existingIdentity = existingBoundary < 0 ? -1
            : assort.IndexOf("RequirePublishedAssortTupleIdentity(trader, id, existing, existingBarter);", existingBoundary + 1, StringComparison.Ordinal);
        int existingFinalWrapper = existingIdentity < 0 ? -1
            : assort.IndexOf("RequireAssortWrapperIdentity();", existingIdentity + 1, StringComparison.Ordinal);
        int existingSuccess = assort.IndexOf("retained validated Ragman", StringComparison.Ordinal);
        if (existingValidation < 0 || existingWrapper < 0 || existingBoundary < 0 || existingIdentity < 0 || existingFinalWrapper < 0 || existingSuccess < 0
            || !(existingValidation < existingWrapper && existingWrapper < existingBoundary && existingBoundary < existingIdentity
                && existingIdentity < existingFinalWrapper && existingFinalWrapper < existingSuccess))
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: retained offer must re-prove captured Ragman wrappers around host/tuple publication before success.");

        int firstMutation = assort.IndexOf("items.Add(offer);", StringComparison.Ordinal);
        int postMutationWrapper = firstMutation < 0 ? -1
            : assort.IndexOf("RequireAssortWrapperIdentity();", firstMutation + 1, StringComparison.Ordinal);
        int newValidation = postMutationWrapper < 0 ? -1
            : assort.IndexOf("ValidateExisting(trader, id, offer, templateId);", postMutationWrapper + 1, StringComparison.Ordinal);
        int newBoundary = newValidation < 0 ? -1
            : assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", newValidation + 1, StringComparison.Ordinal);
        int newIdentity = newBoundary < 0 ? -1
            : assort.IndexOf("RequirePublishedAssortTupleIdentity(trader, id, offer, barter);", newBoundary + 1, StringComparison.Ordinal);
        int finalWrapper = newIdentity < 0 ? -1
            : assort.IndexOf("RequireAssortWrapperIdentity();", newIdentity + 1, StringComparison.Ordinal);
        int catchBlock = finalWrapper < 0 ? -1
            : assort.IndexOf("catch", finalWrapper + 1, StringComparison.Ordinal);
        int ownedItemScan = catchBlock < 0 ? -1
            : assort.IndexOf("ReferenceEquals(items[i], offer)", catchBlock + 1, StringComparison.Ordinal);
        int ownedBarter = ownedItemScan < 0 ? -1
            : assort.IndexOf("ReferenceEquals(currentBarter, barter)", ownedItemScan + 1, StringComparison.Ordinal);
        int loyaltyRollback = ownedBarter < 0 ? -1
            : assort.IndexOf("loyalLevelItems.Remove(id);", ownedBarter + 1, StringComparison.Ordinal);
        int barterRollback = loyaltyRollback < 0 ? -1
            : assort.IndexOf("barterScheme.Remove(id);", loyaltyRollback + 1, StringComparison.Ordinal);
        int itemRollback = barterRollback < 0 ? -1
            : assort.IndexOf("items.RemoveAt(ownedItemIndex);", barterRollback + 1, StringComparison.Ordinal);
        if (firstMutation < 0 || postMutationWrapper < 0 || newValidation < 0 || newBoundary < 0 || newIdentity < 0 || finalWrapper < 0
            || catchBlock < 0 || ownedItemScan < 0 || ownedBarter < 0 || loyaltyRollback < 0 || barterRollback < 0 || itemRollback < 0
            || !(firstMutation < postMutationWrapper && postMutationWrapper < newValidation && newValidation < newBoundary
                && newBoundary < newIdentity && newIdentity < finalWrapper && finalWrapper < catchBlock
                && catchBlock < ownedItemScan && ownedItemScan < ownedBarter && ownedBarter < loyaltyRollback
                && loyaltyRollback < barterRollback && barterRollback < itemRollback))
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: new-offer wrapper proof and captured-wrapper ownership rollback ordering changed.");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string assort = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(assort)) return current.FullName;
            current = current.Parent;
        }

        current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current != null)
        {
            string direct = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(direct)) return current.FullName;

            string nested = Path.Combine(current.FullName, "mods", "SPT-Belt-Armband-Inventory");
            if (File.Exists(Path.Combine(nested, "server", "DogtagCaseAssort.cs"))) return nested;
            current = current.Parent;
        }
        return null;
    }

    private static void Require(string source, string token, string message)
    {
        if (!source.Contains(token, StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: " + message + ".");
    }
}
