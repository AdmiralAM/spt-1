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
        Require(assort, "List<Item> items", "publication proof must use captured Items wrapper");
        Require(assort, "Dictionary<MongoId, List<List<BarterScheme>>> barterScheme", "publication proof must use captured BarterScheme wrapper");
        Require(assort, "Dictionary<MongoId, int> loyalLevelItems", "publication proof must use captured LoyalLevelItems wrapper");
        Require(assort, "ReferenceEquals(item, expectedItem)", "validated assort item must remain exact same object reference");
        Require(assort, "!Equals(expectedItem.Template, new MongoId(RuntimeIdentity.DogtagCaseItemId))", "publication must revalidate exact Dogtag Case template");
        Require(assort, "expectedItem.Upd.StackObjectsCount != UnlimitedStock", "publication must revalidate exact stock contract");
        Require(assort, "ReferenceEquals(liveBarter, expectedBarter)", "validated barter tuple must remain exact same object reference");
        Require(assort, "var expectedInnerBarter = liveBarter[0];", "inner barter wrapper must be pinned before value proof");
        Require(assort, "var expectedScheme = expectedInnerBarter[0];", "barter scheme object must be pinned before value proof");
        Require(assort, "ReferenceEquals(liveBarter[0], expectedInnerBarter)", "initial tuple proof must re-prove pinned inner barter identity");
        Require(assort, "ReferenceEquals(liveBarter[0][0], expectedScheme)", "initial tuple proof must re-prove pinned scheme identity");
        Require(assort, "liveBarter.Count != 1", "publication must revalidate outer barter cardinality");
        Require(assort, "liveBarter[0].Count != 1", "publication must revalidate inner barter cardinality");
        Require(assort, "!Equals(liveBarter[0][0].Template, Money.ROUBLES)", "publication must revalidate barter currency");
        Require(assort, "liveBarter[0][0].Count != PriceRoubles", "publication must revalidate exact price");
        Require(assort, "liveLoyalty != LoyaltyLevel", "loyalty metadata must be revalidated");
        Require(assort, "idMatches > 1", "duplicate assort-ID publication must fail closed");

        Require(assort, "var assort = trader.Assort", "publication must capture exact Ragman Assort wrapper");
        Require(assort, "var items = assort.Items", "publication must capture exact Ragman Items wrapper");
        Require(assort, "var barterScheme = assort.BarterScheme", "publication must capture exact Ragman BarterScheme wrapper");
        Require(assort, "var loyalLevelItems = assort.LoyalLevelItems", "publication must capture exact Ragman LoyalLevelItems wrapper");
        Require(assort, "void RequireAssortWrapperIdentity()", "publication must have exact wrapper-chain identity reproof");
        Require(assort, "!ReferenceEquals(trader.Assort, assort)", "Ragman Assort replacement must fail closed");
        Require(assort, "!ReferenceEquals(trader.Assort?.Items, items)", "Ragman Items replacement must fail closed");
        Require(assort, "!ReferenceEquals(trader.Assort?.BarterScheme, barterScheme)", "Ragman BarterScheme replacement must fail closed");
        Require(assort, "!ReferenceEquals(trader.Assort?.LoyalLevelItems, loyalLevelItems)", "Ragman LoyalLevelItems replacement must fail closed");

        int itemIdentity = assort.IndexOf("idMatches != 1 || exactItemMatches != 1", StringComparison.Ordinal);
        int itemContents = assort.IndexOf("!Equals(expectedItem.Template, new MongoId(RuntimeIdentity.DogtagCaseItemId))", StringComparison.Ordinal);
        int barterIdentity = assort.IndexOf("ReferenceEquals(liveBarter, expectedBarter)", StringComparison.Ordinal);
        int innerCapture = assort.IndexOf("var expectedInnerBarter = liveBarter[0];", barterIdentity + 1, StringComparison.Ordinal);
        int schemeCapture = assort.IndexOf("var expectedScheme = expectedInnerBarter[0];", innerCapture + 1, StringComparison.Ordinal);
        int schemeContents = assort.IndexOf("!Equals(expectedScheme.Template, Money.ROUBLES)", schemeCapture + 1, StringComparison.Ordinal);
        int initialInnerReproof = assort.IndexOf("!ReferenceEquals(liveBarter[0], expectedInnerBarter)", schemeContents + 1, StringComparison.Ordinal);
        int loyaltyProof = assort.IndexOf("liveLoyalty != LoyaltyLevel", initialInnerReproof + 1, StringComparison.Ordinal);
        if (itemIdentity < 0 || itemContents <= itemIdentity || barterIdentity <= itemContents || innerCapture <= barterIdentity
            || schemeCapture <= innerCapture || schemeContents <= schemeCapture || initialInnerReproof <= schemeContents || loyaltyProof <= initialInnerReproof)
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: publication ordering must remain item reference -> item contents -> outer barter reference -> inner identity capture -> scheme value proof -> inner identity reproof -> loyalty.");

        int existingValidation = assort.IndexOf("ValidateExisting(items, barterScheme, loyalLevelItems, id, existing, templateId);", StringComparison.Ordinal);
        int existingWrapper = assort.IndexOf("RequireAssortWrapperIdentity();", existingValidation + 1, StringComparison.Ordinal);
        int existingBoundary = assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", existingWrapper + 1, StringComparison.Ordinal);
        int existingIdentity = assort.IndexOf("RequirePublishedAssortTupleIdentity(items, barterScheme, loyalLevelItems, id, existing, existingBarter);", existingBoundary + 1, StringComparison.Ordinal);
        int existingFinalWrapper = assort.IndexOf("RequireAssortWrapperIdentity();", existingIdentity + 1, StringComparison.Ordinal);
        int existingSuccess = assort.IndexOf("retained validated Ragman", StringComparison.Ordinal);
        if (existingValidation < 0 || existingWrapper <= existingValidation || existingBoundary <= existingWrapper || existingIdentity <= existingBoundary
            || existingFinalWrapper <= existingIdentity || existingSuccess <= existingFinalWrapper)
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: retained offer must re-prove captured wrappers around host/tuple publication before success.");

        int firstMutation = assort.IndexOf("items.Add(offer);", StringComparison.Ordinal);
        int postMutationWrapper = assort.IndexOf("RequireAssortWrapperIdentity();", firstMutation + 1, StringComparison.Ordinal);
        int newValidation = assort.IndexOf("ValidateExisting(items, barterScheme, loyalLevelItems, id, offer, templateId);", postMutationWrapper + 1, StringComparison.Ordinal);
        int newBoundary = assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", newValidation + 1, StringComparison.Ordinal);
        int newIdentity = assort.IndexOf("RequirePublishedAssortTupleIdentity(items, barterScheme, loyalLevelItems, id, offer, barter);", newBoundary + 1, StringComparison.Ordinal);
        int finalWrapper = assort.IndexOf("RequireAssortWrapperIdentity();", newIdentity + 1, StringComparison.Ordinal);
        int catchBlock = assort.IndexOf("catch", finalWrapper + 1, StringComparison.Ordinal);
        int ownedItemScan = assort.IndexOf("ReferenceEquals(items[i], offer)", catchBlock + 1, StringComparison.Ordinal);
        int ownedBarter = assort.IndexOf("ReferenceEquals(currentBarter, barter)", ownedItemScan + 1, StringComparison.Ordinal);
        int loyaltyRollback = assort.IndexOf("loyalLevelItems.Remove(id);", ownedBarter + 1, StringComparison.Ordinal);
        int barterRollback = assort.IndexOf("barterScheme.Remove(id);", loyaltyRollback + 1, StringComparison.Ordinal);
        int itemRollback = assort.IndexOf("items.RemoveAt(ownedItemIndex);", barterRollback + 1, StringComparison.Ordinal);
        if (firstMutation < 0 || postMutationWrapper <= firstMutation || newValidation <= postMutationWrapper || newBoundary <= newValidation
            || newIdentity <= newBoundary || finalWrapper <= newIdentity || catchBlock <= finalWrapper || ownedItemScan <= catchBlock
            || ownedBarter <= ownedItemScan || loyaltyRollback <= ownedBarter || barterRollback <= loyaltyRollback || itemRollback <= barterRollback)
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: new-offer wrapper proof and captured-wrapper ownership rollback ordering changed.");

        if (assort.Contains("ValidateExisting(trader,", StringComparison.Ordinal)
            || assort.Contains("RequirePublishedAssortTupleIdentity(trader,", StringComparison.Ordinal)
            || assort.Contains("RollbackOwnedAssortTuple(", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: direct mutable trader.Assort helper path was restored.");
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
