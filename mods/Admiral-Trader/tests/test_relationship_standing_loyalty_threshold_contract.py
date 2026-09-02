import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class RelationshipStandingLoyaltyThresholdContractTests(unittest.TestCase):
    def load_json(self, relative):
        return json.loads((ROOT / relative).read_text(encoding="utf-8"))

    def test_contract_matches_trader_loyalty_source_and_uplift_envelope(self):
        contract = self.load_json("manifests/relationship-standing-loyalty-threshold-contract.json")
        base = self.load_json("db/base.json")
        uplift = self.load_json("manifests/relationship-standing-stock-uplift.json")

        self.assertEqual(contract["authority"]["loyaltySource"], "db/base.json")
        self.assertFalse(contract["authority"]["runtimeMaterialize"])
        self.assertFalse(contract["authority"]["standingOnlyTierSelectionAllowed"])
        self.assertFalse(contract["authority"]["levelOnlyTierSelectionAllowed"])
        self.assertFalse(contract["authority"]["salesVolumeRequirementAllowed"])

        expected = []
        for loyalty_level, row in enumerate(base["loyaltyLevels"], start=1):
            self.assertEqual(row["minSalesSum"], 0)
            expected.append((loyalty_level, row["minLevel"], row["minStanding"]))

        actual = [
            (row["loyaltyLevel"], row["minPmcLevel"], row["minStanding"])
            for row in contract["tierEligibility"]
        ]
        self.assertEqual(actual, expected)

        uplift_by_ll = {row["loyaltyLevel"]: row for row in uplift["upliftPlan"]["tiers"]}
        for row in contract["tierEligibility"]:
            envelope = uplift_by_ll[row["loyaltyLevel"]]
            self.assertEqual(row["minStanding"], envelope["standing"])
            self.assertEqual(row["stockPerReset"], envelope["stockPerReset"])
            self.assertEqual(row["buyRestriction"], envelope["buyRestriction"])
            self.assertEqual(row["eligibility"], "minPmcLevel AND minStanding")

    def test_policy_and_resolver_keep_level_and_standing_coupled(self):
        contract = self.load_json("manifests/relationship-standing-loyalty-threshold-contract.json")
        policy = (ROOT / contract["authority"]["policyImplementation"]).read_text(encoding="utf-8")
        resolver = (ROOT / contract["authority"]["profileResolver"]).read_text(encoding="utf-8")

        for level, standing in [(15, "0.10"), (25, "0.30"), (35, "0.55")]:
            self.assertIn(f"playerLevel >= {level}", policy)
            self.assertIn(f"standing >= {standing}", policy)

        self.assertIn("pmcProfile.Info.Level", resolver)
        self.assertIn("traderInfo.Standing", resolver)
        self.assertIn("high standing must never compensate for insufficient PMC level", contract["selectionRules"])
        self.assertIn("high PMC level must never compensate for insufficient standing", contract["selectionRules"])

    def test_frozen_boundary_and_physical_checkpoint_remain_untouched(self):
        contract = self.load_json("manifests/relationship-standing-loyalty-threshold-contract.json")
        boundary = contract["frozenBoundary"]
        diagnostic = contract["diagnosticBoundary"]

        self.assertEqual(boundary["questCount"], 31)
        self.assertEqual(boundary["rootOfferCount"], 11)
        self.assertEqual(boundary["relationshipRuntimeOffers"], 0)
        self.assertFalse(boundary["runtimeMaterializationEnabled"])
        self.assertFalse(diagnostic["physicalCheckpointRequestedNow"])
        self.assertIn("high-standing profile below the required PMC level", diagnostic["futureFailStandingOnly"])
        self.assertIn("high-level profile below the required standing", diagnostic["futureFailLevelOnly"])


if __name__ == "__main__":
    unittest.main()
