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
        {"_id":"d5c27bb3169f8dfbc13f6b69","name":"Admiral","nickname":"Admiral","avatar":"/files/trader/avatar/d5c27bb3169f8dfbc13f6b69.jpg","loyaltyLevels":[{"minLevel":1,"minSalesSum":0,"minStanding":0},{"minLevel":15,"minSalesSum":0,"minStanding":0.1},{"minLevel":25,"minSalesSum":0,"minStanding":0.3},{"minLevel":35,"minSalesSum":0,"minStanding":0.55}]}
        """;
        const string policy = """
        {"schemaVersion":4,"productRole":"specialist-trader-and-capability-broker","traderStock":{"baselineStockRequired":true,"baselineOffersMustBeQuestGated":false,"baselineOffersMustBeFinite":true,"relationshipStockAllowed":true,"milestoneOffersMayBeQuestGated":true,"milestoneOffersMustBeFinite":true},"logistics":{"expectedMilestonePermanentOfferCount":1,"maximumPermanentOfferStockPerReset":80,"milestoneOffersMustBeQuestGated":true,"offersMustBeFinite":true,"specialWeaponsPermanentOfferAllowed":false,"specialWeaponsSampleOnly":true}}
        """;
        const string baseline = """
        {"schemaVersion":1,"stockClass":"Baseline","offers":[{"offerId":"base1","tpl":"tpl-base","stockPerReset":4,"buyRestriction":1,"loyaltyLevel":1,"questGate":null}]}
        """;
        const string relationship = """
        {"schemaVersion":1,"stockClass":"Relationship","authority":{"salesSumGateAllowed":false,"questGateAllowed":false,"capabilityAuthority":false,"finiteStockRequired":true},"materialization":{"enabled":true},"offers":[{"offerId":"rel1","tpl":"tpl-rel","loyaltyLevel":3,"stockPerReset":4,"buyRestriction":2,"questGate":null}]}
        """;
        const string assort = """
        {"items":[{"_id":"base1","_tpl":"tpl-base","upd":{"UnlimitedCount":false,"StackObjectsCount":4,"BuyRestrictionMax":1}},{"_id":"rel1","_tpl":"tpl-rel","upd":{"UnlimitedCount":false,"StackObjectsCount":4,"BuyRestrictionMax":2}},{"_id":"mile1","_tpl":"tpl-mile","upd":{"UnlimitedCount":false,"StackObjectsCount":10,"BuyRestrictionMax":2}}],"loyal_level_items":{"base1":1,"rel1":3,"mile1":1}}
        """;
        const string frozenAssort = """
        {"items":[{"_id":"base1","_tpl":"tpl-base","upd":{"UnlimitedCount":false,"StackObjectsCount":4,"BuyRestrictionMax":1}},{"_id":"mile1","_tpl":"tpl-mile","upd":{"UnlimitedCount":false,"StackObjectsCount":10,"BuyRestrictionMax":2}}],"loyal_level_items":{"base1":1,"mile1":1}}
        """;
        const string questassort = """
        {"started":{},"success":{"mile1":"q1"},"fail":{}}
        """;
        const string quest = """
        {"_id":"q1","conditions":{"AvailableForStart":[{"conditionType":"Level","compareMethod":">=","value":12}]}}
        """;

        var frozen = AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy, baseline, frozenAssort, questassort, new[] { quest });
        Require(frozen.RelationshipStockAllowed && frozen.RelationshipOfferCount == 0 && frozen.Offers.Count == 2, "absent Relationship manifest must preserve frozen Gameplay Alpha behavior");
        MustFail("truncated frozen quest/offer surface", () => AdmiralTraderGameplayAlphaAdapter.ValidateFrozenReleaseShape(frozen, 1));

        var frozenRelease = frozen with
        {
            BaselineOfferCount = AdmiralTraderGameplayAlphaAdapter.FrozenBaselineOfferCount,
            RelationshipOfferCount = 0,
            MilestoneOfferCount = AdmiralTraderGameplayAlphaAdapter.FrozenMilestoneOfferCount,
            Offers = Enumerable.Range(0, AdmiralTraderGameplayAlphaAdapter.FrozenTotalOfferCount)
                .Select(index => frozen.Offers[index % frozen.Offers.Count])
                .ToArray(),
        };
        AdmiralTraderGameplayAlphaAdapter.ValidateFrozenReleaseShape(
            frozenRelease,
            AdmiralTraderGameplayAlphaAdapter.FrozenQuestCount);
        MustFail("Relationship materialization is outside frozen 0.1.0", () =>
            AdmiralTraderGameplayAlphaAdapter.ValidateFrozenReleaseShape(
                frozenRelease with { RelationshipOfferCount = 1 },
                AdmiralTraderGameplayAlphaAdapter.FrozenQuestCount));

        var contract = AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy, baseline, assort, questassort, new[] { quest }, relationship);
        var offers = contract.Offers;
        Require(contract.ProductName == "Admiral Trader" && contract.ModGuid == "com.admiralam.spt.admiraltrader", "product/owner identity mismatch");
        Require(contract.TraderId == "d5c27bb3169f8dfbc13f6b69" && contract.GameplayPolicySchemaVersion == 4, "trader/schema identity mismatch");
        Require(contract.RelationshipStockAllowed && contract.RelationshipOfferCount == 1, "explicit Relationship offer must be classified once");
        Require(!contract.SpecialWeaponsPermanentOfferAllowed && contract.SpecialWeaponsSampleOnly, "sample-only special-weapons semantics drifted");
        Require(contract.BaselineOfferCount == 1 && contract.RelationshipOfferCount == 1 && contract.MilestoneOfferCount == 1 && offers.Count == 3, "class counts mismatch");
        var b = offers.Single(x => x.StockClass == "Baseline");
        Require(b.GateKind == "None" && b.QuestGateId is null && b.EffectiveGate is null, "baseline must not fabricate quest gate");
        Require(b.Source.EarliestProgressionLevel == 1, "baseline first-contact progression must be explicit");
        var r = offers.Single(x => x.StockClass == "Relationship");
        Require(r.GateKind == "Loyalty" && r.QuestGateId is null && r.EffectiveGate is null, "Relationship offer must use loyalty, not quest, gating");
        Require(r.LoyaltyLevel == 3 && r.Source.EarliestProgressionLevel == 25, "Relationship effective progression level must come from Admiral LL3");
        Require(r.StockPerReset == 4 && r.BuyRestrictionPerReset == 2, "Relationship bounded supply mismatch");
        var m = offers.Single(x => x.StockClass == "Milestone");
        Require(m.GateKind == "Quest" && m.QuestGateId == "q1", "milestone quest gate missing");
        Require(m.Source.EarliestProgressionLevel == 12 && m.EffectiveGate is not null, "milestone effective progression gate mismatch");
        Require(offers.All(x => x.Source.ProvenanceClass == "ExplicitAdapter"), "explicit adapter provenance must survive");
        Require(offers.All(x => x.Capacity.SupplyBound == RenewableSupplyBound.Bounded), "all Gameplay Alpha permanent offers must stay bounded");

        MustFail("wrong mod owner", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign.Replace("com.admiralam.spt.admiraltrader", "other.owner"), identity, traderBase, policy, baseline, frozenAssort, questassort, new[] { quest }));
        MustFail("wrong product", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity.Replace("Admiral Trader", "Other Trader"), traderBase, policy, baseline, frozenAssort, questassort, new[] { quest }));
        MustFail("wrong trader id", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase.Replace("d5c27bb3169f8dfbc13f6b69", "aaaaaaaaaaaaaaaaaaaaaaaa"), policy, baseline, frozenAssort, questassort, new[] { quest }));
        MustFail("baseline in questassort", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy, baseline, frozenAssort, "{\"started\":{},\"success\":{\"base1\":\"q1\"},\"fail\":{}}", new[] { quest }));
        MustFail("unclassified relationship-like offer", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy, baseline, assort, questassort, new[] { quest }));
        MustFail("Relationship overlap with milestone", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy, baseline, assort, questassort, new[] { quest }, relationship.Replace("\"rel1\"", "\"mile1\"")));
        MustFail("special weapons made permanent", () => AdmiralTraderGameplayAlphaAdapter.Parse(campaign, identity, traderBase, policy.Replace("\"specialWeaponsPermanentOfferAllowed\":false", "\"specialWeaponsPermanentOfferAllowed\":true"), baseline, frozenAssort, questassort, new[] { quest }));
        MustFail("legacy schema v3 contract is outside frozen 0.1.0", () =>
            AdmiralTraderGameplayAlphaAdapter.Parse(
                campaign,
                identity,
                traderBase,
                policy.Replace("\"schemaVersion\":4", "\"schemaVersion\":3"),
                baseline,
                frozenAssort,
                questassort,
                new[] { quest }));
        Console.WriteLine("Economy Admiral Admiral Trader Gameplay Alpha + Relationship compatibility smoke PASS");
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void MustFail(string name, Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException($"Gameplay Alpha smoke expected '{name}' to fail.");
    }
}
