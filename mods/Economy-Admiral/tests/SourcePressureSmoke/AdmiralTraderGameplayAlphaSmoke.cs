using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class AdmiralTraderGameplayAlphaSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string campaign = """
        {"schemaVersion":1,"product":{"modName":"Admiral Trader","modGuid":"com.admiralam.spt.admiraltrader","traderId":"d5c27bb3169f8dfbc13f6b69"}}
        """;
        const string identity = """
        {"schemaVersion":3,"product":"Admiral Trader","traderId":"d5c27bb3169f8dfbc13f6b69"}
        """;
        const string traderBase = """
        {"_id":"d5c27bb3169f8dfbc13f6b69","name":"Admiral","nickname":"Admiral","avatar":"/files/trader/avatar/d5c27bb3169f8dfbc13f6b69.jpg"}
        """;
        const string policy = """
        {"schemaVersion":4,"productRole":"specialist-trader-and-capability-broker","traderStock":{"baselineStockRequired":true,"baselineOffersMustBeQuestGated":false,"baselineOffersMustBeFinite":true,"relationshipStockAllowed":true,"milestoneOffersMayBeQuestGated":true,"milestoneOffersMustBeFinite":true},"logistics":{"expectedMilestonePermanentOfferCount":1,"maximumPermanentOfferStockPerReset":80,"milestoneOffersMustBeQuestGated":true,"offersMustBeFinite":true,"specialWeaponsPermanentOfferAllowed":false,"specialWeaponsSampleOnly":true}}
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

        var contract = AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy, baseline, assort, questassort, new[] { quest });
        var offers = contract.Offers;
        Require(contract.ProductName == "Admiral Trader" && contract.ModGuid == "com.admiralam.spt.admiraltrader", "product/owner identity mismatch");
        Require(contract.TraderId == "d5c27bb3169f8dfbc13f6b69" && contract.GameplayPolicySchemaVersion == 4, "trader/schema identity mismatch");
        Require(contract.RelationshipStockAllowed && contract.RelationshipOfferCount == 0, "Relationship must be supported without fabricated current offers");
        Require(!contract.SpecialWeaponsPermanentOfferAllowed && contract.SpecialWeaponsSampleOnly, "sample-only special-weapons semantics drifted");
        Require(contract.BaselineOfferCount == 1 && contract.MilestoneOfferCount == 1 && offers.Count == 2, "class counts mismatch");
        var b = offers.Single(x => x.StockClass == "Baseline");
        Require(b.GateKind == "None" && b.QuestGateId is null && b.EffectiveGate is null, "baseline must not fabricate quest gate");
        Require(b.Source.EarliestProgressionLevel == 1, "baseline first-contact progression must be explicit");
        var m = offers.Single(x => x.StockClass == "Milestone");
        Require(m.GateKind == "Quest" && m.QuestGateId == "q1", "milestone quest gate missing");
        Require(m.Source.EarliestProgressionLevel == 12 && m.EffectiveGate is not null, "milestone effective progression gate mismatch");
        Require(offers.All(x => x.Source.ProvenanceClass == "ExplicitAdapter"), "explicit adapter provenance must survive");
        Require(offers.All(x => x.Capacity.SupplyBound == RenewableSupplyBound.Bounded), "all Gameplay Alpha permanent offers must stay bounded");

        MustFail("wrong mod owner", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign.Replace("com.admiralam.spt.admiraltrader", "other.owner"), identity, traderBase, policy, baseline, assort, questassort, new[] { quest }));
        MustFail("wrong product", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity.Replace("Admiral Trader", "Other Trader"), traderBase, policy, baseline, assort, questassort, new[] { quest }));
        MustFail("wrong trader id", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase.Replace("d5c27bb3169f8dfbc13f6b69", "aaaaaaaaaaaaaaaaaaaaaaaa"), policy, baseline, assort, questassort, new[] { quest }));
        MustFail("baseline in questassort", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy, baseline, assort, "{\"started\":{},\"success\":{\"base1\":\"q1\"},\"fail\":{}}", new[] { quest }));
        MustFail("unclassified relationship-like offer", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy, baseline, assort, "{\"started\":{},\"success\":{},\"fail\":{}}", new[] { quest }));
        MustFail("special weapons made permanent", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy.Replace("\"specialWeaponsPermanentOfferAllowed\":false", "\"specialWeaponsPermanentOfferAllowed\":true"), baseline, assort, questassort, new[] { quest }));
        Console.WriteLine("Economy Admiral Admiral Trader Gameplay Alpha compatibility smoke PASS");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void MustFail(string name, Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException($"Gameplay Alpha smoke expected '{name}' to fail.");
    }
}
