import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"


def load_json(name: str):
    return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))


class M3BaselineConsolidationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.debt = load_json("m3-baseline-quality-debt.json")
        cls.plan = load_json("m3-baseline-consolidation-plan.json")
        cls.editorial = load_json("m3-baseline-consolidation-editorial.json")
        cls.keys = load_json("keys-authored-spec.json")
        cls.weapon = load_json("weapon-ammo-runtime-plan.json")

    def test_all_consolidation_artifacts_are_design_only(self):
        self.assertFalse(self.debt["runtimeMaterialize"])
        self.assertFalse(self.plan["runtimeMaterialize"])
        self.assertFalse(self.editorial["runtimeMaterialize"])
        self.assertTrue(self.plan["materializationGate"]["requiresM1LifecyclePass"])
        self.assertTrue(self.plan["materializationGate"]["requiresM2ExistingCampaignPass"])
        self.assertTrue(self.debt["postM2QualityGate"]["existingQuestIdsMayBeRetiredOnlyWithMigrationPlan"])

    def test_debt_review_matches_current_baseline_shape(self):
        boundary = self.debt["baselineBoundary"]
        self.assertEqual(boundary["historicalQuestCount"], 31)
        self.assertEqual(boundary["accessQuestCount"], 10)
        self.assertEqual(boundary["arsenalQuestCount"], 21)
        self.assertEqual(len(self.keys["quests"]), 10)
        self.assertEqual(len(self.weapon["quests"]), 21)
        self.assertFalse(boundary["m2AcceptanceMeansFinalContentQuality"])

    def test_access_retirement_candidates_are_real_current_quests(self):
        current_ids = {row["id"] for row in self.keys["quests"]}
        current_slugs = {row["slug"] for row in self.keys["quests"]}
        candidates = self.plan["accessConsolidation"]["retirementCandidates"]
        ids = [row["questId"] for row in candidates]
        slugs = [row["slug"] for row in candidates]
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(len(slugs), len(set(slugs)))
        self.assertTrue(set(ids).issubset(current_ids))
        self.assertTrue(set(slugs).issubset(current_slugs))

    def test_access_consolidation_keeps_no_map_by_map_quota(self):
        access = self.plan["accessConsolidation"]
        self.assertFalse(access["countIsQuota"])
        self.assertLess(access["candidateCount"], access["currentQuestCount"])
        decisions = {row["key"]: row["decision"] for row in access["candidateOperations"]}
        self.assertEqual(
            decisions,
            {
                "access-fundamentals": "KEEP-ONE-INTRO-IF-NEEDED",
                "borrowed-access": "SUPERSEDE-WITH-M3-ROUTE-USE",
                "restricted-site-clearance": "MERGE-LABS-READINESS-AND-CLEARANCE",
            },
        )

    def test_arsenal_consolidation_does_not_preserve_three_stage_counter_ladder(self):
        arsenal = self.plan["arsenalConsolidation"]
        self.assertEqual(arsenal["currentQuestCount"], 21)
        self.assertFalse(arsenal["countIsQuota"])
        minimum, maximum = arsenal["candidateCountRange"]
        self.assertGreaterEqual(minimum, 1)
        self.assertLess(maximum, arsenal["currentQuestCount"])
        decisions = [row["decision"] for row in arsenal["candidateOperations"]]
        self.assertTrue(any("COLLAPSE" in decision for decision in decisions))
        self.assertIn("MERGE-WITH-AUTHORED-PRECISION-PATH", decisions)

    def test_editorial_concepts_cover_non_m3_consolidation_candidates(self):
        editorial_keys = [row["key"] for row in self.editorial["concepts"]]
        self.assertEqual(len(editorial_keys), len(set(editorial_keys)))
        self.assertNotIn("borrowed-access", editorial_keys)
        expected = {
            "access-fundamentals",
            "restricted-site-clearance",
            "sidearm-contingency",
            "close-quarter-control",
            "general-purpose-rifle",
            "stand-off-control",
            "specialist-platform",
        }
        self.assertEqual(set(editorial_keys), expected)

    def test_consolidation_copy_is_bilingual_and_natural_language(self):
        forbidden = (
            "bounded",
            "allowlist",
            "semantic overlap",
            "materialization",
            "exact tpl",
            "x2/x5/x10",
        )
        for concept in self.editorial["concepts"]:
            self.assertTrue(concept["title"]["en"].strip())
            self.assertTrue(concept["title"]["ru"].strip())
            self.assertGreaterEqual(len(concept["briefing"]["en"]), 120)
            self.assertGreaterEqual(len(concept["briefing"]["ru"]), 120)
            self.assertGreaterEqual(len(concept["success"]["en"]), 35)
            self.assertGreaterEqual(len(concept["success"]["ru"]), 35)
            player_text = "\n".join(
                [
                    concept["briefing"]["en"],
                    concept["briefing"]["ru"],
                    concept["success"]["en"],
                    concept["success"]["ru"],
                ]
            ).casefold()
            for phrase in forbidden:
                self.assertNotIn(phrase, player_text, concept["key"])

    def test_removed_quest_reward_totals_are_not_treated_as_budget(self):
        self.assertEqual(
            self.plan["arsenalConsolidation"]["rewardRule"],
            "Recalculate rewards for final consolidated difficulty. Historical 21-quest XP/RUB/standing totals are not a budget to preserve.",
        )
        self.assertFalse(self.plan["migrationSafety"]["standingAndRewardCompensationForRemovedUncompletedQuestsRequired"])

    def test_candidate_total_is_explicitly_not_a_target(self):
        shape = self.plan["candidatePostM2Shape"]
        self.assertEqual(shape["accessOperations"], 3)
        self.assertEqual(shape["m3AuthoredOperations"], 12)
        self.assertEqual(shape["illustrativeTotalRange"], [19, 21])
        self.assertIn("not a target", shape["warning"].lower())


if __name__ == "__main__":
    unittest.main()
