using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseCapturedAssortExecutionRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag captured assort execution regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs"));
        if (source.Contains("RollbackOwnedAssortTuple(", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag captured assort execution regression failed: stale direct-wrapper rollback helper was restored.");

        int publish = source.IndexOf("private static void RequirePublishedAssortTupleIdentity(", StringComparison.Ordinal);
        int hostBoundary = source.IndexOf("private static void RequirePublicationBoundary", publish, StringComparison.Ordinal);
        if (publish < 0 || hostBoundary <= publish)
            throw new InvalidOperationException("Dogtag captured assort execution regression failed: publication helper region is missing.");

        string publicationRegion = source.Substring(publish, hostBoundary - publish);
        Require(publicationRegion, "List<Item> items", "publication helpers must receive the captured Items wrapper");
        Require(publicationRegion, "Dictionary<MongoId, List<List<BarterScheme>>> barterScheme", "publication helpers must receive the captured BarterScheme wrapper");
        Require(publicationRegion, "Dictionary<MongoId, int> loyalLevelItems", "publication helpers must receive the captured LoyalLevelItems wrapper");
        if (publicationRegion.Contains("trader.Assort", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag captured assort execution regression failed: publication helpers re-read mutable trader.Assort state.");

        int validate = source.IndexOf("private static void ValidateExisting(", hostBoundary, StringComparison.Ordinal);
        if (validate < 0)
            throw new InvalidOperationException("Dogtag captured assort execution regression failed: validation helper is missing.");
        string validationRegion = source.Substring(validate);
        Require(validationRegion, "List<Item> items", "validation must receive the captured Items wrapper");
        Require(validationRegion, "Dictionary<MongoId, List<List<BarterScheme>>> barterScheme", "validation must receive the captured BarterScheme wrapper");
        Require(validationRegion, "Dictionary<MongoId, int> loyalLevelItems", "validation must receive the captured LoyalLevelItems wrapper");
        if (validationRegion.Contains("trader.Assort", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag captured assort execution regression failed: validation re-read mutable trader.Assort state.");

        Require(source, "ValidateExisting(items, barterScheme, loyalLevelItems, id, existing, templateId);", "retained offer must validate only through captured wrappers");
        Require(source, "ValidateExisting(items, barterScheme, loyalLevelItems, id, offer, templateId);", "new offer must validate only through captured wrappers");
        Require(source, "RequirePublishedAssortTupleIdentity(items, barterScheme, loyalLevelItems, id, existing, existingBarter);", "retained publication must use captured wrappers");
        Require(source, "RequirePublishedAssortTupleIdentity(items, barterScheme, loyalLevelItems, id, offer, barter);", "new publication must use captured wrappers");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs"))) return current.FullName;
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
            throw new InvalidOperationException("Dogtag captured assort execution regression failed: " + message + ".");
    }
}
