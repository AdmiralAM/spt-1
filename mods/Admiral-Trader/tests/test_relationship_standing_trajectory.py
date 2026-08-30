import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
EPSILON = 1e-9


class RelationshipStandingTrajectoryTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.contract = json.loads(
            (ROOT / "manifests" / "relationship-standing-trajectory.json").read_text(encoding="utf-8")
        )
        cls.base = json.loads((ROOT / "db" / "base.json").read_text(encoding="utf-8"))
        cls.keys = json.loads(
            (ROOT / "manifests" / "keys-authored-spec.json").read_text(encoding="utf-8")
        )
        cls.arsenal = json.loads(
            (ROOT / "manifests" / "weapon-ammo-authored-spec.json").read_text(encoding="utf-8")
        )

    def authored_rewards(self):
        rewards = []
        for quest in self.keys["quests"]:
            rewards.append((quest["minimumLevel"], quest["rewardBudget"]["standing"]))
        for family in self.arsenal["families"]:
            for stage in family["stages"]:
                rewards.append((stage["minimumLevel"], stage["standing"]))
        return rewards

    def test_contract_matches_runtime_loyalty_thresholds(self):
        tiers = self.contract["tiers"]
        runtime = self.base["loyaltyLevels"]
        self.assertEqual(len(tiers), len(runtime))
        for expected_level, (tier, actual) in enumerate(zip(tiers, runtime), start=1):
            self.assertEqual(tier["loyaltyLevel"], expected_level)
            self.assertEqual(tier["minimumPlayerLevel"], actual["minLevel"])
            self.assertAlmostEqual(tier["requiredStanding"], actual["minStanding"])
            self.assertEqual(actual["minSalesSum"], 0)
        self.assertFalse(self.contract["policy"]["salesSumGateAllowed"])

    def test_each_tier_is_reachable_by_its_own_level_gate(self):
        rewards = self.authored_rewards()
        for tier in self.contract["tiers"]:
            level = tier["minimumPlayerLevel"]
            available = sum(standing for minimum_level, standing in rewards if minimum_level <= level)
            self.assertAlmostEqual(available, tier["maximumAuthoredStandingAvailableByLevel"])
            self.assertGreaterEqual(available + EPSILON, tier["requiredStanding"])
            self.assertAlmostEqual(
                available - tier["requiredStanding"],
                tier["headroom"],
            )
            self.assertTrue(tier["reachableByOwnLevelGate"])

    def test_campaign_standing_totals_match_authored_specs(self):
        access_total = sum(quest["rewardBudget"]["standing"] for quest in self.keys["quests"])
        arsenal_total = sum(
            stage["standing"]
            for family in self.arsenal["families"]
            for stage in family["stages"]
        )
        campaign = self.contract["campaignStanding"]
        self.assertAlmostEqual(access_total, campaign["accessDomainTotal"])
        self.assertAlmostEqual(arsenal_total, campaign["arsenalDomainTotal"])
        self.assertAlmostEqual(access_total + arsenal_total, campaign["authoredTotal"])
        self.assertAlmostEqual(campaign["authoredTotal"], 0.65)
        self.assertAlmostEqual(
            campaign["authoredTotal"] - campaign["ll4Requirement"],
            campaign["ll4CompletionHeadroom"],
        )

    def test_ll2_ll3_ll4_have_positive_standing_headroom(self):
        for tier in self.contract["tiers"]:
            if tier["loyaltyLevel"] == 1:
                continue
            self.assertGreater(tier["headroom"], 0)

    def test_slice_does_not_mutate_frozen_runtime_rewards(self):
        self.assertEqual(
            self.contract["frozen010Base"],
            "053a62ff5f1cb545f13bc89a96bba3acd319a823",
        )
        self.assertFalse(self.contract["policy"]["runtimeRewardMutationInThisSlice"])
        self.assertTrue(self.contract["changeControl"]["standingReductionRequiresTrajectoryRecalculation"])
        self.assertTrue(self.contract["changeControl"]["futureRelationshipOfferMustNotCompensateForUnreachableTier"])


if __name__ == "__main__":
    unittest.main()
