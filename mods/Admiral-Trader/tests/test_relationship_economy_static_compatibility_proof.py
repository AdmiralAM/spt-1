import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]


class RelationshipEconomyCompatibilityProofTests(unittest.TestCase):
    def test_profile_projection_preserves_economy_static_baseline_contract(self):
        proof = json.loads((ROOT / "manifests" / "relationship-economy-static-compatibility-proof.json").read_text(encoding="utf-8"))
        baseline = json.loads((ROOT / "manifests" / "baseline-stock.json").read_text(encoding="utf-8"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        economy = (REPO / "mods" / "Economy-Admiral" / "server" / "AdmiralTraderGameplayAlphaAdapter.cs").read_text(encoding="utf-8")
        router = (ROOT / "server" / "RelationshipStandingAssortDynamicRouter.cs").read_text(encoding="utf-8")
        projection = (ROOT / "server" / "RelationshipStandingAssortProjection.cs").read_text(encoding="utf-8")

        result = proof["compatibilityResult"]
        self.assertTrue(result["economyStaticBaselineAuditRemainsValid"])
        self.assertFalse(result["dynamicStandingProjectionRequiresEconomyAdapterRewrite"])
        self.assertTrue(result["crossModuleStaticCompatibilityProven"])
        self.assertFalse(result["economyEnvelopeApproved"])
        self.assertFalse(result["runtimeMaterializationAllowed"])

        # This invariant exists on both the frozen staging base and current origin/main:
        # Economy Admiral audits immutable source-assort Baseline capacity exactly.
        self.assertIn('baseline.GetProperty("stockPerReset").GetInt32() == stock', economy)
        self.assertIn('baseline capacity drift', economy)

        # Admiral's future uplift is deliberately downstream of that source audit and
        # mutates only the already-produced request response, never TradersTable/source assort.
        self.assertIn('OnLoadOrder.Routers + 1', router)
        self.assertIn('if (!RuntimeMaterializationEnabled)', router)
        self.assertIn('coordinator.Project(sessionId, response.Data);', router)
        self.assertIn('marker.Upd.StackObjectsCount = tier.StockPerReset;', projection)
        self.assertIn('marker.Upd.BuyRestrictionMax = tier.BuyRestriction;', projection)
        self.assertNotIn('TradersTable', projection)

        baseline_ids = {offer["offerId"] for offer in baseline["offers"]}
        assort_ids = {item["_id"] for item in assort["items"]}
        self.assertTrue(baseline_ids.issubset(assort_ids))
        self.assertEqual(len(assort["items"]), 11)
        self.assertEqual(proof["frozenBoundary"], {
            "questCount": 31,
            "rootOfferCount": 11,
            "relationshipRuntimeOffers": 0,
            "runtimeMutationInThisSlice": False,
        })


if __name__ == "__main__":
    unittest.main()
