using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class AdmiralTraderGameplayAlphaSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string policy = """
        {"schemaVersion":4,"productRole":"specialist-trader-and-capability-broker","traderStock":{"baselineStockRequired":true,"baselineOffersMustBeQuestGated":false,"baselineOffersMustBeFinite":true,"relationshipStockAllowed":true,"milestoneOffersMayBeQuestGated":true,"milestoneOffersMustBeFinite":true},"logistics":{"expectedMilestonePermanentOfferCount":1,"maximumPermanentOfferStockPerReset":80,"milestoneOffersMustBeQuestGated":true,"offersMustBeFinite":true}}
        """;
        const string baseline = """
        {"schemaVersion":1,"stockClass":"Baseline","offers":[{"offerId":"base1","tpl":"tpl-base","stockPerReset":4,"buyRestriction":1,"loyaltyLevel":1,"questGate":null}]}
        """;
        const string assort = """
        {"items":[{"_id":"base1","_tpl":"tpl-base","upd":{"UnlimitedCount":false,"StackObjectsCount":4,"BuyRestrictionMax":1}},{"_id":"mile1","_tpl":"tpl-mile","upd":{"UnlimitedCount":false,"StackObjectsCount":10,"BuyRestrictionMax":2}}],"loyal_level_items":{"base1":1,"mile1":1}}
        """;
        const string questassort = """
        {"started":{},"success":{"mile1":"q1"},"fail":{}}
        """;
        const string quest = """
        {"_id":"q1","conditions":{"AvailableForStart":[{"conditionType":"Level","compareMethod":">=","value":12}]}}
        """;

        var offers = AdmiralTraderGameplayAlphaAdapter.Parse(policy, baseline, assort, questassort, new[] { quest });
        Require(offers.Count == 2, "two offers expected");
        var b = offers.Single(x => x.StockClass == "Baseline");
        Require(b.GateKind == "None" && b.QuestGateId is null && b.EffectiveGate is null, "baseline must not fabricate quest gate");
        Require(b.Source.EarliestProgressionLevel == 1, "baseline first-contact progression must be explicit");
        var m = offers.Single(x => x.StockClass == "Milestone");
        Require(m.GateKind == "Quest" && m.QuestGateId == "q1", "milestone quest gate missing");
        Require(m.Source.EarliestProgressionLevel == 12 && m.EffectiveGate is not null, "milestone effective progression gate mismatch");
        Require(offers.All(x => x.Capacity.SupplyBound == RenewableSupplyBound.Bounded), "all Gameplay Alpha offers must stay bounded");

        MustFail("baseline in questassort", () => AdmiralTraderGameplayAlphaAdapter.Parse(policy, baseline, assort, "{\"started\":{},\"success\":{\"base1\":\"q1\"},\"fail\":{}}", new[] { quest }));
        MustFail("unclassified offer", () => AdmiralTraderGameplayAlphaAdapter.Parse(policy, baseline, assort, "{\"started\":{},\"success\":{},\"fail\":{}}", new[] { quest }));
        Console.WriteLine("Economy Admiral Admiral Trader Gameplay Alpha smoke PASS");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void MustFail(string name, Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException($"Gameplay Alpha smoke expected '{name}' to fail.");
    }
}
