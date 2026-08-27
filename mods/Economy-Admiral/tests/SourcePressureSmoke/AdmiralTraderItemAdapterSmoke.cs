using System.Runtime.CompilerServices;
using SPTEconomy;

internal static class AdmiralTraderItemAdapterSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        const string policyJson = """
        {
          "schemaVersion": 3,
          "productRole": "capability-broker",
          "logistics": {
            "expectedPermanentOfferCount": 7,
            "expectedAmmoPermanentOfferCount": 6,
            "maximumPermanentOfferStockPerReset": 80,
            "maximumAmmoUnitsAcrossPermanentOffersPerReset": 400,
            "maximumAmmoFullResetSpendRub": 300000,
            "maximumReferencePriceMultiplier": 1.3,
            "offersMustBeQuestGated": true,
            "offersMustBeFinite": true,
            "questUnlockLoyaltyLevel": 1,
            "specialWeaponsPermanentOfferAllowed": false,
            "specialWeaponsSampleOnly": true
          },
          "loyalty": {
            "role": "relationship-status-only",
            "capabilityAuthority": false,
            "standingMayBypassQuestGates": false,
            "salesSumMayGateProgression": false
          }
        }
        """;

        const string assortJson = """
        {
          "items": [
            {"_id":"ad1000000000000000000001","_tpl":"5c94bbff86f7747ee735c08f","upd":{"UnlimitedCount":false,"StackObjectsCount":1,"BuyRestrictionMax":1}},
            {"_id":"6cf0fc22a55417075c5af23e","_tpl":"5cc80f53e4a949000e1ea4f8","upd":{"UnlimitedCount":false,"StackObjectsCount":80,"BuyRestrictionMax":80}},
            {"_id":"67d5501fb925a7836b99f112","_tpl":"5efb0cabfb3e451d70735af5","upd":{"UnlimitedCount":false,"StackObjectsCount":80,"BuyRestrictionMax":80}},
            {"_id":"ece0342652b331ac065d2e5a","_tpl":"5d6e68a8a4b9360b6c0d54e2","upd":{"UnlimitedCount":false,"StackObjectsCount":40,"BuyRestrictionMax":40}},
            {"_id":"b71182859e5958fd12c02e89","_tpl":"59e6906286f7746c9f75e847","upd":{"UnlimitedCount":false,"StackObjectsCount":80,"BuyRestrictionMax":80}},
            {"_id":"07efd6dee267ec18ed830dd6","_tpl":"5a608bf24f39f98ffc77720e","upd":{"UnlimitedCount":false,"StackObjectsCount":60,"BuyRestrictionMax":60}},
            {"_id":"731e65964d324bc545a1b839","_tpl":"5fc382c1016cce60e8341b20","upd":{"UnlimitedCount":false,"StackObjectsCount":40,"BuyRestrictionMax":40}}
          ],
          "loyal_level_items": {
            "ad1000000000000000000001":1,"6cf0fc22a55417075c5af23e":1,"67d5501fb925a7836b99f112":1,
            "ece0342652b331ac065d2e5a":1,"b71182859e5958fd12c02e89":1,"07efd6dee267ec18ed830dd6":1,"731e65964d324bc545a1b839":1
          }
        }
        """;

        const string questAssortJson = """
        {
          "started": {},
          "success": {
            "ad1000000000000000000001":"68a6527a3c73b2e85977d7a1",
            "6cf0fc22a55417075c5af23e":"8cba3e2ec639a4aa2c26c4da",
            "67d5501fb925a7836b99f112":"43d9544a09d068476a1a18df",
            "ece0342652b331ac065d2e5a":"8d8d81032315f4fdc5a06798",
            "b71182859e5958fd12c02e89":"f6e51dc4e50e47ee9af50a4d",
            "07efd6dee267ec18ed830dd6":"7564e60e4c1c2f1b67a594a4",
            "731e65964d324bc545a1b839":"cd2641c70bede98dac3945d0"
          },
          "fail": {}
        }
        """;

        var policy = AdmiralTraderAdapterEvidence.ParseGameplayPolicy(policyJson);
        var offers = AdmiralTraderItemAdapter.ParseOffers(assortJson, questAssortJson, policy);
        Require(offers.Count == 7, "expected seven maintained permanent offers");
        Require(offers.All(offer => offer.LoyaltyLevel == 1), "all capability offers must retain LL1 metadata");
        Require(offers.All(offer => offer.StockPerReset > 0 && offer.StockPerReset <= 80), "all offers must remain within maintained stock cap");
        Require(offers.All(offer => offer.BuyRestrictionPerReset > 0), "all offers must remain buy-limited");
        Require(offers.All(offer => offer.Source.Renewable), "permanent offers must be renewable acquisition sources");
        Require(offers.All(offer => offer.Source.EarliestProgressionLevel is null), "quest progression must not be fabricated before quest graph evidence is supplied");
        Require(offers.All(offer => offer.Source.ProvenanceClass == "ExplicitAdapter"), "adapter evidence must carry explicit attribution confidence");
        Require(offers.All(offer => offer.Capacity.SupplyBound == RenewableSupplyBound.Bounded), "all maintained offers must map to bounded capacity evidence");
        Require(offers.All(offer => !string.IsNullOrWhiteSpace(offer.QuestGateId)), "every offer must have explicit success quest gate");
        Require(offers.Single(offer => offer.OfferId == "ad1000000000000000000001").QuestGateId == "68a6527a3c73b2e85977d7a1", "Labs access gate mapping mismatch");

        var sourcePressure = SourcePressureEvidenceAnalyzer.Analyze(offers.Select(offer => offer.Source));
        var bounded = BoundedSupplyEvidenceAnalyzer.Analyze(offers.Select(offer => offer.Source), offers.Select(offer => offer.Capacity));
        Require(sourcePressure.Count == 7, "each maintained offer item should enter source pressure evidence");
        Require(bounded.Count == 7 && bounded.All(item => item.HasOnlyKnownBoundedRenewablePaths), "maintained Admiral Trader offers must remain bounded-only in isolated adapter evidence");

        MustFail("PascalCase questassort regression", () => AdmiralTraderItemAdapter.ParseOffers(assortJson, questAssortJson.Replace("\"success\"", "\"Success\"", StringComparison.Ordinal), policy));
        MustFail("unlimited drift", () => AdmiralTraderItemAdapter.ParseOffers(assortJson.Replace("\"UnlimitedCount\":false", "\"UnlimitedCount\":true", StringComparison.Ordinal), questAssortJson, policy));
        MustFail("missing quest gate", () => AdmiralTraderItemAdapter.ParseOffers(assortJson, questAssortJson.Replace("\"ad1000000000000000000001\":\"68a6527a3c73b2e85977d7a1\",", "", StringComparison.Ordinal), policy));
        MustFail("loyalty drift", () => AdmiralTraderItemAdapter.ParseOffers(assortJson.Replace("\"ad1000000000000000000001\":1", "\"ad1000000000000000000001\":2", StringComparison.Ordinal), questAssortJson, policy));
        MustFail("stock cap drift", () => AdmiralTraderItemAdapter.ParseOffers(assortJson.Replace("\"StackObjectsCount\":80", "\"StackObjectsCount\":81", StringComparison.Ordinal), questAssortJson, policy));
        MustFail("extra quest mapping", () => AdmiralTraderItemAdapter.ParseOffers(assortJson, questAssortJson.Replace("\"success\": {", "\"success\": {\"orphan\":\"quest\",", StringComparison.Ordinal), policy));

        Console.WriteLine("Economy Admiral Admiral Trader item adapter smoke PASS");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"Economy Admiral Admiral Trader item adapter smoke: {message}");
    }

    private static void MustFail(string name, Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException($"Economy Admiral Admiral Trader item adapter smoke expected '{name}' to fail.");
    }
}
