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

        int barterIdentity = assort.IndexOf("ReferenceEquals(liveBarter, expectedBarter)", StringComparison.Ordinal);
        int barterContents = assort.IndexOf("liveBarter.Count != 1", StringComparison.Ordinal);
        int loyaltyProof = assort.IndexOf("liveLoyalty != LoyaltyLevel", StringComparison.Ordinal);
        if (barterIdentity < 0 || barterContents < 0 || loyaltyProof < 0
            || !(barterIdentity < barterContents && barterContents < loyaltyProof))
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: retained barter reference must be proven before exact in-place contents and loyalty are revalidated.");

        int existingValidation = assort.IndexOf("ValidateExisting(trader, id, existing, templateId);", StringComparison.Ordinal);
        int existingBoundary = existingValidation < 0 ? -1
            : assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", existingValidation + 1, StringComparison.Ordinal);
        int existingIdentity = existingBoundary < 0 ? -1
            : assort.IndexOf("RequirePublishedAssortTupleIdentity(trader, id, existing, existingBarter);", existingBoundary + 1, StringComparison.Ordinal);
        int existingSuccess = assort.IndexOf("retained validated Ragman", StringComparison.Ordinal);
        if (existingValidation < 0 || existingBoundary < 0 || existingIdentity < 0 || existingSuccess < 0
            || !(existingValidation < existingBoundary && existingBoundary < existingIdentity && existingIdentity < existingSuccess))
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: retained offer must re-prove host then exact assort tuple identity before success.");

        int newValidation = assort.IndexOf("ValidateExisting(trader, id, offer, templateId);", StringComparison.Ordinal);
        int newBoundary = newValidation < 0 ? -1
            : assort.IndexOf("RequirePublicationBoundary(templateTable, templateId);", newValidation + 1, StringComparison.Ordinal);
        int newIdentity = newBoundary < 0 ? -1
            : assort.IndexOf("RequirePublishedAssortTupleIdentity(trader, id, offer, barter);", newBoundary + 1, StringComparison.Ordinal);
        int rollback = newIdentity < 0 ? -1
            : assort.IndexOf("RollbackOwnedAssortTuple(trader, id, offer, barter, itemAdded, barterAdded, loyaltyAdded);", newIdentity + 1, StringComparison.Ordinal);
        if (newValidation < 0 || newBoundary < 0 || newIdentity < 0 || rollback < 0
            || !(newValidation < newBoundary && newBoundary < newIdentity && newIdentity < rollback))
            throw new InvalidOperationException("Dogtag assort publication identity regression failed: newly owned tuple identity proof must remain inside rollback ownership after host proof.");
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