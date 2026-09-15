import json
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]


class M5RelationshipRuntimeTests(unittest.TestCase):
    def load(self, relative):
        return json.loads((ROOT / relative).read_text(encoding="utf-8"))

    def test_relationship_offers_are_finite_native_loyalty_stock(self):
        relationship = self.load("manifests/relationship-stock.json")
        assort = self.load("db/assort.json")
        questassort = self.load("db/questassort.json")
        offers = {row["offerId"]: row for row in relationship["offers"]}
        roots = {row["_id"]: row for row in assort["items"]}

        self.assertTrue(relationship["materialization"]["enabled"])
        self.assertEqual(len(offers), 3)
        self.assertEqual([row["loyaltyLevel"] for row in relationship["offers"]], [2, 3, 4])
        self.assertTrue(set(offers).isdisjoint(questassort["success"]))
        for offer_id, policy in offers.items():
            item = roots[offer_id]
            self.assertEqual(item["_tpl"], policy["tpl"])
            self.assertFalse(item["upd"]["UnlimitedCount"])
            self.assertEqual(item["upd"]["StackObjectsCount"], policy["stockPerReset"])
            self.assertEqual(item["upd"]["BuyRestrictionMax"], policy["buyRestriction"])
            self.assertEqual(assort["loyal_level_items"][offer_id], policy["loyaltyLevel"])
            self.assertEqual(assort["barter_scheme"][offer_id][0][0], {
                "count": policy["priceRub"], "_tpl": "5449016a4bdc2d6f028b456f"
            })

    def test_signal_stock_excludes_extraction_airdrop_and_combat_capabilities(self):
        relationship = self.load("manifests/relationship-stock.json")
        evidence = self.load("manifests/m5-spt415-item-evidence.json")
        tpls = {row["tpl"] for row in relationship["offers"]}
        self.assertEqual(tpls, {
            "624c09da2cec124eb67c1046",
            "624c09e49b98e019a3315b66",
            "66d97834d2985e11480d5c1e",
        })
        self.assertNotIn("624c0570c9b794431568f5d5", tpls)  # green extraction
        self.assertNotIn("624c09cfbc2e27219346d955", tpls)  # red airdrop
        self.assertEqual(set(relationship["itemEvidence"]["directVanillaRootOfferHits"].values()), {0})
        evidence_by_tpl = {row["tpl"]: row for row in evidence["items"]}
        self.assertEqual(set(evidence_by_tpl), tpls)
        self.assertTrue(all(not row["directVanillaRootOffers"] for row in evidence_by_tpl.values()))
        self.assertTrue(all(row["explosionStrength"] == 0 and row["penetrationPower"] == 0 for row in evidence_by_tpl.values()))

    def test_loyalty_progression_matches_existing_trader_contract(self):
        relationship = self.load("manifests/relationship-stock.json")
        base = self.load("db/base.json")
        tiers = relationship["standingStockUplift"]["tiers"]
        self.assertEqual(len(tiers), len(base["loyaltyLevels"]))
        for tier, loyalty in zip(tiers, base["loyaltyLevels"]):
            self.assertEqual(tier["minimumPlayerLevel"], loyalty["minLevel"])
            self.assertEqual(tier["requiredStanding"], loyalty["minStanding"])
            self.assertEqual(loyalty["minSalesSum"], 0)
        self.assertEqual([x["stockPerReset"] for x in tiers], [12, 16, 20, 24])
        self.assertEqual([x["buyRestriction"] for x in tiers], [4, 6, 8, 10])

    def test_projection_is_requester_scoped_and_preserves_offer_identity(self):
        router = (ROOT / "server/RelationshipStandingAssortDynamicRouter.cs").read_text(encoding="utf-8")
        resolver = (ROOT / "server/RelationshipStandingProfileResolver.cs").read_text(encoding="utf-8")
        projection = (ROOT / "server/RelationshipStandingAssortProjection.cs").read_text(encoding="utf-8")
        self.assertIn('$"/client/trading/api/getTraderAssort/{RuntimeIdentity.TraderId}"', router)
        self.assertIn("OnLoadOrder.Routers - 1", router)
        self.assertIn("TraderCallbacks traderCallbacks", router)
        self.assertIn("await traderCallbacks.GetAssort(url, request, sessionId)", router)
        self.assertIn("ProfileHelper", resolver)
        self.assertIn("GetPmcProfile(sessionId)", resolver)
        self.assertIn("marker.Upd.StackObjectsCount = tier.StockPerReset", projection)
        self.assertIn("marker.Upd.BuyRestrictionMax = tier.BuyRestriction", projection)
        self.assertNotIn("TradersTable tradersTable", router + resolver + projection)
        self.assertNotIn("marker.Id =", projection)
        self.assertNotIn("marker.Template =", projection)

    def test_frozen_authority_and_quest_graph_remain_unchanged(self):
        provenance = self.load("manifests/m5-relationship-runtime.json")
        quests = list((ROOT / "db/quests").glob("*.json"))
        self.assertEqual(provenance["historicalFrozenAuthority"]["sourceHead"], "053a62ff5f1cb545f13bc89a96bba3acd319a823")
        self.assertFalse(provenance["historicalFrozenAuthority"]["modified"])
        self.assertGreaterEqual(len(quests), 43)
        self.assertFalse(provenance["boundaries"]["questIdsChanged"])
        self.assertFalse(provenance["boundaries"]["questGraphChanged"])
        self.assertFalse(provenance["boundaries"]["secondTrader"])
        self.assertFalse(provenance["boundaries"]["externalNatalyaDependency"])


if __name__ == "__main__":
    unittest.main()
