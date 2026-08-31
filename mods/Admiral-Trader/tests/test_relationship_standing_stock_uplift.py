import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class RelationshipStandingStockUpliftTests(unittest.TestCase):
    def load(self, name):
        return json.loads((ROOT / "manifests" / name).read_text(encoding="utf-8"))

    def test_uplift_uses_only_existing_baseline_offer_and_adds_no_root_offer(self):
        uplift = self.load("relationship-standing-stock-uplift.json")
        baseline = self.load("baseline-stock.json")
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))

        self.assertFalse(uplift["authority"]["runtimeMaterialize"])
        self.assertEqual(uplift["authority"]["newRootOffersAdded"], 0)
        self.assertEqual(len(assort["items"]), 11)
        self.assertEqual(uplift["frozenBoundary"]["rootOfferCount"], 11)
        self.assertEqual(uplift["frozenBoundary"]["relationshipRuntimeOffers"], 0)
        self.assertFalse(uplift["frozenBoundary"]["baselineRuntimeMutationInThisSlice"])

        baseline_by_id = {offer["offerId"]: offer for offer in baseline["offers"]}
        plan = uplift["upliftPlan"]
        self.assertIn(plan["offerId"], baseline_by_id)
        self.assertEqual(plan["tpl"], baseline_by_id[plan["offerId"]]["tpl"])
        self.assertEqual(plan["priceRub"], baseline_by_id[plan["offerId"]]["priceRub"])

    def test_only_consumable_field_marking_offer_is_eligible(self):
        uplift = self.load("relationship-standing-stock-uplift.json")
        reviewed = {entry["offerId"]: entry for entry in uplift["reviewedBaselineOffers"]}

        self.assertEqual(set(reviewed), {
            "ad2000000000000000000001",
            "ad2000000000000000000002",
            "ad2000000000000000000003",
            "ad2000000000000000000004",
        })
        approved = [entry for entry in reviewed.values() if entry["decision"].startswith("approve-")]
        self.assertEqual(len(approved), 1)
        self.assertEqual(approved[0]["offerId"], "ad2000000000000000000004")
        self.assertEqual(approved[0]["role"], "field-marking")
        self.assertTrue(all(
            reviewed[offer_id]["decision"].startswith("no-uplift-")
            for offer_id in {
                "ad2000000000000000000001",
                "ad2000000000000000000002",
                "ad2000000000000000000003",
            }
        ))

    def test_tiers_are_finite_monotonic_and_standing_aligned(self):
        uplift = self.load("relationship-standing-stock-uplift.json")
        relationship = self.load("relationship-stock.json")
        baseline = self.load("baseline-stock.json")
        plan = uplift["upliftPlan"]
        tiers = plan["tiers"]

        self.assertEqual([tier["loyaltyLevel"] for tier in tiers], [1, 2, 3, 4])
        self.assertEqual(
            [tier["standing"] for tier in tiers],
            [0.0] + relationship["authority"]["standingThresholds"],
        )
        self.assertEqual(tiers[0]["stockPerReset"], baseline["offers"][3]["stockPerReset"])
        self.assertEqual(tiers[0]["buyRestriction"], baseline["offers"][3]["buyRestriction"])
        self.assertEqual([tier["stockPerReset"] for tier in tiers], [12, 16, 20, 24])
        self.assertEqual([tier["buyRestriction"] for tier in tiers], [4, 6, 8, 10])
        self.assertTrue(all(a < b for a, b in zip(
            [tier["stockPerReset"] for tier in tiers],
            [tier["stockPerReset"] for tier in tiers][1:],
        )))
        self.assertTrue(all(a < b for a, b in zip(
            [tier["buyRestriction"] for tier in tiers],
            [tier["buyRestriction"] for tier in tiers][1:],
        )))
        self.assertTrue(all(tier["buyRestriction"] <= tier["stockPerReset"] for tier in tiers))

    def test_economic_delta_and_capability_boundaries_are_explicit(self):
        uplift = self.load("relationship-standing-stock-uplift.json")
        plan = uplift["upliftPlan"]
        delta = plan["ll1ToLl4Delta"]
        bounds = plan["bounds"]
        first, last = plan["tiers"][0], plan["tiers"][-1]

        self.assertFalse(uplift["authority"]["priceDiscountAllowed"])
        self.assertFalse(uplift["authority"]["questGateBypassAllowed"])
        self.assertFalse(uplift["authority"]["capabilityOfferUpliftAllowed"])
        self.assertTrue(uplift["authority"]["economyReviewRequiredBeforeMaterialization"])
        self.assertFalse(plan["priceChangesAcrossTiers"])

        self.assertEqual(delta["stockUnitsPerReset"], last["stockPerReset"] - first["stockPerReset"])
        self.assertEqual(delta["personalBuyUnitsPerReset"], last["buyRestriction"] - first["buyRestriction"])
        self.assertEqual(delta["personalSpendCapacityRub"], delta["personalBuyUnitsPerReset"] * plan["priceRub"])
        self.assertEqual(delta["globalStockValueRub"], delta["stockUnitsPerReset"] * plan["priceRub"])
        self.assertLessEqual(last["stockPerReset"], bounds["maximumStockPerReset"])
        self.assertLessEqual(last["buyRestriction"], bounds["maximumBuyRestriction"])
        self.assertLessEqual(delta["personalSpendCapacityRub"], bounds["maximumPersonalSpendCapacityIncreaseRub"])
        self.assertLessEqual(delta["globalStockValueRub"], bounds["maximumGlobalStockValueIncreaseRub"])

        gates = uplift["materializationGates"]
        self.assertFalse(gates["implementationAllowed"])
        self.assertTrue(gates["requiresEconomyAdmiralApproval"])
        self.assertTrue(gates["requiresRuntimeLoyaltyVisibilityProof"])
        self.assertTrue(gates["requiresAssortMutationContract"])
        self.assertTrue(gates["requiresFrozen010PhysicalGate"])
        self.assertFalse(gates["physicalCheckpointRequestedNow"])


if __name__ == "__main__":
    unittest.main()
