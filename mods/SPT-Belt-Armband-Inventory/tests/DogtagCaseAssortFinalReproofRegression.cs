using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class DogtagCaseAssortFinalReproofRegression
{
    [ModuleInitializer]
    internal static void Run()
    {
        string? root = FindModuleRoot();
        if (root == null)
            throw new InvalidOperationException("Dogtag assort final reproof regression failed: module root could not be resolved.");

        string source = File.ReadAllText(Path.Combine(root, "server", "DogtagCaseAssort.cs"));
        const string loyalty = "liveLoyalty != LoyaltyLevel";
        const string finalCall = "RequirePublishedAssortTupleStillStable(items, barterScheme, loyalLevelItems, id, expectedItem, expectedBarter, expectedInnerBarter, expectedScheme);";
        const string finalMethod = "private static void RequirePublishedAssortTupleStillStable(";
        const string publicationBoundary = "private static void RequirePublicationBoundary(TemplateTable templateTable, MongoId templateId)";
        const string effectiveHostReproof = "DogtagCaseHostExclusionPolicy.RequireCurrentHost(templateTable);";

        int firstLoyalty = source.IndexOf(loyalty, StringComparison.Ordinal);
        int reproofCall = firstLoyalty < 0 ? -1 : source.IndexOf(finalCall, firstLoyalty + loyalty.Length, StringComparison.Ordinal);
        int reproofMethod = source.IndexOf(finalMethod, StringComparison.Ordinal);
        if (firstLoyalty < 0 || reproofCall < 0 || reproofMethod < 0 || !(firstLoyalty < reproofCall && reproofCall < reproofMethod))
            throw new InvalidOperationException("Dogtag assort final reproof regression failed: whole-tuple reproof must run after the first loyalty proof and before publication returns.");

        string identityRegion = source.Substring(0, reproofCall);
        Require(identityRegion, "var expectedInnerBarter = liveBarter[0];", "first publication proof must pin the exact validated inner barter list");
        Require(identityRegion, "var expectedScheme = expectedInnerBarter[0];", "first publication proof must pin the exact validated BarterScheme object");

        string finalRegion = source.Substring(reproofMethod);
        Require(finalRegion, "List<Item> items", "final reproof must operate on captured Items wrapper");
        Require(finalRegion, "Dictionary<MongoId, List<List<BarterScheme>>> barterScheme", "final reproof must operate on captured BarterScheme wrapper");
        Require(finalRegion, "Dictionary<MongoId, int> loyalLevelItems", "final reproof must operate on captured LoyalLevelItems wrapper");
        Require(finalRegion, "ReferenceEquals(item, expectedItem)", "final reproof must retain exact item reference identity");
        Require(finalRegion, "expectedItem.Upd.StackObjectsCount != UnlimitedStock", "final reproof must retain exact item stock/value contract");
        Require(finalRegion, "ReferenceEquals(liveBarter, expectedBarter)", "final reproof must retain exact outer barter reference identity");
        Require(finalRegion, "ReferenceEquals(liveBarter[0], expectedInnerBarter)", "final reproof must retain exact inner barter-list reference identity");
        Require(finalRegion, "ReferenceEquals(liveBarter[0][0], expectedScheme)", "final reproof must retain exact BarterScheme reference identity");
        Require(finalRegion, "liveBarter[0][0].Count != PriceRoubles", "final reproof must retain exact RUB price contract");
        Require(finalRegion, "liveLoyalty != LoyaltyLevel", "final reproof must retain exact loyalty metadata");
        Require(finalRegion, "idMatches > 1", "final reproof must fail closed on assort-ID ambiguity");
        if (finalRegion.Contains("trader.Assort", StringComparison.Ordinal))
            throw new InvalidOperationException("Dogtag assort final reproof regression failed: final proof re-read mutable trader.Assort state.");

        int boundaryStart = source.IndexOf(publicationBoundary, StringComparison.Ordinal);
        int nextMethod = boundaryStart < 0 ? -1 : source.IndexOf("internal static void RequireExactDogtagHost", boundaryStart, StringComparison.Ordinal);
        if (boundaryStart < 0 || nextMethod < 0)
            throw new InvalidOperationException("Dogtag assort final reproof regression failed: publication boundary could not be isolated.");
        string boundaryRegion = source.Substring(boundaryStart, nextMethod - boundaryStart);
        Require(boundaryRegion, "DogtagCaseItem.RequireCanonicalRegisteredTemplate(templateTable);", "assort publication boundary must reprove canonical registered template");
        Require(boundaryRegion, "RequireExactDogtagHost(templateTable, templateId);", "assort publication boundary must reprove exact Dogtag host identity/content");
        Require(boundaryRegion, effectiveHostReproof, "assort publication boundary must reprove effective Dogtag acceptance including any optional future exclusions");
    }

    private static string? FindModuleRoot()
    {
        DirectoryInfo? current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            string source = Path.Combine(current.FullName, "server", "DogtagCaseAssort.cs");
            if (File.Exists(source)) return current.FullName;
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
            throw new InvalidOperationException("Dogtag assort final reproof regression failed: " + message + ".");
    }
}
