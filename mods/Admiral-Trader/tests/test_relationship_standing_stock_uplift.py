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
        self.assertEqual(approved[0]["decision"], "approve-uplift-economy-reviewed")
        self.assertTrue(all(reviewed[offer_id]["decision"].startswith("no-uplift-") for offer_id in {
            "ad2000000000000000000001", "ad2000000000000000000002", "ad2000000000000000000003"
        }))

    def test_tiers_are_finite_monotonic_and_standing_aligned(self):
        uplift = self.load("relationship-standing-stock-uplift.json")
        relationship = self.load("relationship-stock.json")
        baseline = self.load("baseline-stock.json")
        tiers = uplift["upliftPlan"]["tiers"]

        self.assertEqual([tier["loyaltyLevel"] for tier in tiers], [1, 2, 3, 4])
        self.assertEqual([tier["standing"] for tier in tiers], [0.0] + relationship["authority"]["standingThresholds"])
        self.assertEqual(tiers[0]["stockPerReset"], baseline["offers"][3]["stockPerReset"])
        self.assertEqual(tiers[0]["buyRestriction"], baseline["offers"][3]["buyRestriction"])
        self.assertEqual([tier["stockPerReset"] for tier in tiers], [12, 16, 20, 24])
        self.assertEqual([tier["buyRestriction"] for tier in tiers], [4, 6, 8, 10])
        self.assertEqual([tier["state"] for tier in tiers[1:]], ["post-0.1.0-economy-approved"] * 3)
        self.assertTrue(all(a < b for a, b in zip([tier["stockPerReset"] for tier in tiers], [tier["stockPerReset"] for tier in tiers][1:])))
        self.assertTrue(all(a < b for a, b in zip([tier["buyRestriction"] for tier in tiers], [tier["buyRestriction"] for tier in tiers][1:])))
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
        self.assertFalse(uplift["authority"]["economyReviewRequiredBeforeMaterialization"])
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
        self.assertFalse(gates["requiresEconomyAdmiralApproval"])
        self.assertTrue(gates["requiresRuntimeLoyaltyVisibilityProof"])
        self.assertFalse(gates["requiresAssortMutationContract"])
        self.assertTrue(gates["requiresFrozen010PhysicalGate"])
        self.assertFalse(gates["physicalCheckpointRequestedNow"])

    def test_economy_review_approves_exact_finite_envelope_only(self):
        uplift = self.load("relationship-standing-stock-uplift.json")
        review = uplift["economyReview"]
        plan = uplift["upliftPlan"]

        self.assertEqual(review["decision"], "approved-with-bounds")
        self.assertTrue(review["finiteCapacityApproved"])
        self.assertFalse(review["priceChangeApproved"])
        self.assertFalse(review["newOfferApproved"])
        self.assertFalse(review["capabilityGateBypassApproved"])
        self.assertFalse(review["renewableHighValueFaucetApproved"])
        self.assertEqual(review["reviewedPriceRub"], plan["priceRub"])
        self.assertEqual(
            [(tier["loyaltyLevel"], tier["stockPerReset"], tier["buyRestriction"]) for tier in review["tierEnvelopeApproved"]],
            [(tier["loyaltyLevel"], tier["stockPerReset"], tier["buyRestriction"]) for tier in plan["tiers"]],
        )

        boundary = review["economicBoundary"]
        ll1, ll4 = plan["tiers"][0], plan["tiers"][-1]
        self.assertEqual(boundary["maximumPersonalUnitsPerReset"], ll4["buyRestriction"])
        self.assertEqual(boundary["maximumPersonalSpendRubPerReset"], ll4["buyRestriction"] * plan["priceRub"])
        self.assertEqual(boundary["maximumIncrementalPersonalUnitsVsLl1"], ll4["buyRestriction"] - ll1["buyRestriction"])
        self.assertEqual(boundary["maximumIncrementalPersonalSpendRubVsLl1"], (ll4["buyRestriction"] - ll1["buyRestriction"]) * plan["priceRub"])
        self.assertEqual(boundary["maximumGlobalUnitsPerReset"], ll4["stockPerReset"])
        self.assertEqual(boundary["maximumIncrementalGlobalUnitsVsLl1"], ll4["stockPerReset"] - ll1["stockPerReset"])
        self.assertLess(ll4["buyRestriction"] * 2, ll4["stockPerReset"])
        self.assertIn("LL4 stock exceeds 24 or personal buy restriction exceeds 10", review["invalidatesApprovalIf"])
        self.assertIn("the offer becomes unlimited", review["invalidatesApprovalIf"])
        self.assertIn("the uplift is extended to another TPL without a separate economy review", review["invalidatesApprovalIf"])

    def test_assort_mutation_contract_is_narrow_and_preserves_spt_purchase_state(self):
        uplift = self.load("relationship-standing-stock-uplift.json")
        contract = uplift["materializationArchitecture"]["assortMutationContract"]
        source = (ROOT / contract["implementedFile"].replace("server/", "server/")).read_text(encoding="utf-8")

        self.assertEqual(contract["proofState"], "implemented-and-regression-locked")
        self.assertEqual(contract["targetOfferId"], uplift["upliftPlan"]["offerId"])
        self.assertEqual(contract["targetTpl"], uplift["upliftPlan"]["tpl"])
        self.assertEqual(contract["requiredRootSlotId"], "hideout")
        self.assertEqual(set(contract["allowedWrites"]), {"Upd.StackObjectsCount", "Upd.BuyRestrictionMax", "Upd.UnlimitedCount"})
        self.assertIn("Upd.BuyRestrictionCurrent", contract["requiredPreservation"])
        self.assertFalse(contract["unlimitedCountForced"])
        self.assertFalse(contract["globalSourceAssortAccepted"])
        self.assertEqual(set(contract["failClosedOn"]), {
            "missing marker offer", "marker template mismatch", "non-hideout root shape", "missing Upd"
        })

        self.assertIn("marker.Upd.StackObjectsCount = tier.StockPerReset;", source)
        self.assertIn("marker.Upd.BuyRestrictionMax = tier.BuyRestriction;", source)
        self.assertIn("marker.Upd.UnlimitedCount = false;", source)
        self.assertNotIn("marker.Upd.BuyRestrictionCurrent =", source)
        self.assertIn("marker.Template.ToString() != RelationshipStandingStockPolicy.MarkerTpl", source)
        self.assertIn("marker.SlotId, \"hideout\"", source)


if __name__ == "__main__":
    unittest.main()
