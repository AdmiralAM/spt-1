import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class RelationshipStockContractTests(unittest.TestCase):
    def load(self, name):
        return json.loads((ROOT / "manifests" / name).read_text(encoding="utf-8"))

    def test_relationship_contract_matches_gameplay_policy(self):
        policy = self.load("gameplay-policy.json")
        relationship = self.load("relationship-stock.json")

        self.assertEqual(relationship["schemaVersion"], 1)
        self.assertEqual(relationship["stockClass"], "Relationship")
        self.assertTrue(policy["traderStock"]["relationshipStockAllowed"])
        self.assertFalse(relationship["authority"]["salesSumGateAllowed"])
        self.assertFalse(relationship["authority"]["questGateAllowed"])
        self.assertFalse(relationship["authority"]["capabilityAuthority"])
        self.assertTrue(relationship["authority"]["finiteStockRequired"])
        self.assertEqual(
            relationship["authority"]["standingThresholds"],
            policy["loyalty"]["expectedStandingThresholds"][1:],
        )

        tiers = relationship["tiers"]
        self.assertEqual([x["loyaltyLevel"] for x in tiers], [2, 3, 4])
        self.assertEqual([x["standing"] for x in tiers], policy["loyalty"]["expectedStandingThresholds"][1:])
        self.assertTrue(all(x["targetOfferCount"]["min"] > 0 for x in tiers))
        self.assertTrue(all(x["targetOfferCount"]["max"] >= x["targetOfferCount"]["min"] for x in tiers))

    def test_frozen_candidate_is_not_materialized_by_relationship_design(self):
        relationship = self.load("relationship-stock.json")
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        baseline = self.load("baseline-stock.json")
        questassort = json.loads((ROOT / "db" / "questassort.json").read_text(encoding="utf-8"))

        self.assertFalse(relationship["materialization"]["enabled"])
        self.assertEqual(len(assort["items"]), 11)
        self.assertEqual(len(baseline["offers"]), 4)
        self.assertEqual(len(questassort["success"]), 7)
        classified = {x["offerId"] for x in baseline["offers"]} | set(questassort["success"])
        self.assertEqual(classified, {x["_id"] for x in assort["items"]})

    def test_relationship_cannot_bypass_capability_boundaries(self):
        relationship = self.load("relationship-stock.json")
        rules = relationship["designRules"]
        self.assertFalse(rules["permanentHighEndAmmoAllowed"])
        self.assertFalse(rules["labsAccessAllowed"])
        self.assertFalse(rules["specialWeaponsSupplyAllowed"])
        self.assertFalse(rules["questMilestoneSubstituteAllowed"])
        self.assertEqual(rules["directOverlapAuditRequired"], ["vanilla", "Scorpion", "Artem"])
        self.assertTrue(rules["runtimeMaterializationRequiresItemTplProof"])
        self.assertTrue(rules["runtimeMaterializationRequiresEconomyReview"])


if __name__ == "__main__":
    unittest.main()
