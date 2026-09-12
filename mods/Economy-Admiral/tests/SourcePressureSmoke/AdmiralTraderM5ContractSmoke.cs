using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class AdmiralTraderM5ContractSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "mods", "Admiral-Trader");
        if (!Directory.Exists(root))
            throw new InvalidOperationException($"Admiral Trader source directory missing: {root}");

        string Read(params string[] parts) => File.ReadAllText(parts.Aggregate(root, Path.Combine));
        var quests = Directory.EnumerateFiles(Path.Combine(root, "db", "quests"), "*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToArray();

        var contract = AdmiralTraderGameplayAlphaAdapter.Parse(
            Read("manifests", "campaign-manifest.json"),
            Read("manifests", "identity-assets.json"),
            Read("db", "base.json"),
            Read("manifests", "gameplay-policy.json"),
            Read("manifests", "baseline-stock.json"),
            Read("db", "assort.json"),
            Read("db", "questassort.json"),
            quests,
            Read("manifests", "relationship-stock.json"),
            Read("manifests", "storefront-core-expansion.json"));

        AdmiralTraderGameplayAlphaAdapter.ValidateActiveCampaignShape(contract, quests.Length);
        Require(contract.GameplayPolicySchemaVersion == 5, "M5 policy schema drift");
        Require(contract.BaselineOfferCount == 4, "M5 Baseline count drift");
        Require(contract.RelationshipOfferCount == 3, "M5 Relationship count drift");
        Require(contract.MilestoneOfferCount == 8, "M5 Milestone count drift");
        Require(contract.CoreOfferCount == 22, "stabilized Core count drift");
        Require(contract.Offers.Count == 37, "stabilized total offer count drift");
        Require(contract.Offers.Where(x => x.StockClass == "Relationship").All(x =>
            x.GateKind == "Loyalty" && x.QuestGateId is null && x.Capacity.SupplyBound == RenewableSupplyBound.Bounded),
            "M5 Relationship gating/capacity drift");
        Console.WriteLine("Economy Admiral live Admiral Trader M5 contract smoke PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
