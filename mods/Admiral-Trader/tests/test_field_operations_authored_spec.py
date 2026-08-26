import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPEC = ROOT / "manifests" / "field-operations-authored-spec.json"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class FieldOperationsAuthoredSpecTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec = load(SPEC)
        cls.operations = {row["id"]: row for row in cls.spec["operations"]}

    def test_campaign_expansion_has_no_artificial_count_cap(self):
        self.assertIsNone(self.spec["policy"]["artificialQuestCountCap"])
        self.assertFalse(self.spec["policy"]["directLegacyTemplateCopy"])
        self.assertTrue(self.spec["policy"]["materializeOnlyAfterSpt413ConditionProof"])

    def test_first_hvt_is_authored_not_numeric_ladder(self):
        quest = self.operations["SO-HVT-01"]
        self.assertEqual(quest["target"]["role"], "bossGluhar")
        self.assertEqual(quest["mechanic"]["requiredCount"], 1)
        self.assertFalse(quest["mechanic"]["repeatable"])
        for field in ("why", "what", "context", "payoff"):
            self.assertTrue(quest[field].strip())
        self.assertNotIn("every boss", quest["what"].lower())

    def test_reward_cannot_degenerate_to_money_only(self):
        reward = self.operations["SO-HVT-01"]["reward"]
        self.assertFalse(reward["moneyOnlyAllowed"])
        self.assertFalse(reward["permanentHighEndAmmoFaucetAllowed"])
        self.assertIn("TraderStanding", reward["mustInclude"])
        self.assertTrue(any("item" in value.lower() or "stock" in value.lower() for value in reward["mustInclude"]))

    def test_runtime_stays_fail_closed_until_specific_413_proof(self):
        quest = self.operations["SO-HVT-01"]
        self.assertFalse(quest["runtime"]["materialize"])
        evidence = quest["conditionEvidence"]
        self.assertEqual(evidence["spt413SpecificSavageRoleRuntimeProof"], "pending")
        blockers = " ".join(quest["runtime"]["blockers"]).lower()
        self.assertIn("spt 4.1.3", blockers)
        self.assertIn("economy admiral", blockers)
        self.assertIn("en/ru", blockers)
        self.assertIn("vanilla", blockers)
        self.assertIn("scorpion", blockers)
        self.assertIn("artem", blockers)


if __name__ == "__main__":
    unittest.main()
