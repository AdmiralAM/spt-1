import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPEC = ROOT / "manifests" / "field-operations-authored-spec.json"
QUESTS = ROOT / "db" / "quests"
LOCALES = ROOT / "db" / "locales"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class FieldOperationsAuthoredSpecTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec = load(SPEC)
        cls.operations = {row["id"]: row for row in cls.spec["operations"]}
        cls.hvt = cls.operations["SO-HVT-01"]
        cls.quest = load(QUESTS / "32-a971ba710c8bc517f8b227c7.json")
        cls.en = load(LOCALES / "operations-en.json")
        cls.ru = load(LOCALES / "operations-ru.json")

    def test_campaign_expansion_has_no_artificial_count_cap(self):
        self.assertIsNone(self.spec["policy"]["artificialQuestCountCap"])
        self.assertFalse(self.spec["policy"]["directLegacyTemplateCopy"])
        self.assertTrue(self.spec["policy"]["materializeOnlyAfterSpt413ConditionProof"])

    def test_first_hvt_is_authored_not_numeric_ladder(self):
        self.assertEqual(self.hvt["target"]["role"], "bossGluhar")
        self.assertEqual(self.hvt["mechanic"]["requiredCount"], 1)
        self.assertFalse(self.hvt["mechanic"]["repeatable"])
        for field in ("why", "what", "context", "payoff"):
            self.assertTrue(self.hvt[field].strip())
        self.assertNotIn("every boss", self.hvt["what"].lower())

    def test_broken_command_is_materialized_as_runtime_quest(self):
        self.assertTrue(self.hvt["runtime"]["materialize"])
        self.assertEqual(self.quest["_id"], self.hvt["runtimeQuestId"])
        self.assertEqual(self.quest["QuestName"], "Field Operation: Broken Command")
        self.assertEqual(self.quest["traderId"], "d5c27bb3169f8dfbc13f6b69")
        self.assertEqual(self.quest["type"], "Elimination")
        self.assertFalse(self.quest["restartable"])
        finish = self.quest["conditions"]["AvailableForFinish"]
        self.assertEqual(len(finish), 1)
        self.assertEqual(finish[0]["conditionType"], "CounterCreator")
        self.assertEqual(finish[0]["value"], 1)
        kills = finish[0]["counter"]["conditions"]
        self.assertEqual(len(kills), 1)
        self.assertEqual(kills[0]["conditionType"], "Kills")
        self.assertEqual(kills[0]["target"], "Savage")
        self.assertEqual(kills[0]["savageRole"], ["bossGluhar"])

    def test_operation_is_progression_gated_after_reserve_access(self):
        start = self.quest["conditions"]["AvailableForStart"]
        levels = [row for row in start if row["conditionType"] == "Level"]
        prereqs = [row for row in start if row["conditionType"] == "Quest"]
        self.assertEqual(levels[0]["value"], 25)
        self.assertEqual(prereqs[0]["target"], "9c438fa48f645044ddc75e8d")
        self.assertEqual(prereqs[0]["status"], [4])

    def test_reward_is_bounded_and_not_money_only(self):
        reward = self.hvt["reward"]
        self.assertFalse(reward["moneyOnlyAllowed"])
        self.assertFalse(reward["permanentHighEndAmmoFaucetAllowed"])
        self.assertEqual(reward["experience"], 9500)
        self.assertEqual(reward["standing"], 0.06)
        self.assertEqual(reward["itemTpl"], "5c12613b86f7743bbe2c3f76")
        success = self.quest["rewards"]["Success"]
        self.assertEqual([row["type"] for row in success], ["Experience", "TraderStanding", "Item"])

    def test_operation_locales_cover_runtime_and_objective_keys(self):
        fields = {
            "name", "description", "note", "startedMessageText", "successMessageText",
            "failMessageText", "acceptPlayerMessage", "declinePlayerMessage",
            "completePlayerMessage", "changeQuestMessageText"
        }
        qid = self.quest["_id"]
        objective_id = self.quest["conditions"]["AvailableForFinish"][0]["id"]
        expected = {f"{qid} {field}" for field in fields} | {objective_id}
        self.assertEqual(set(self.en), expected)
        self.assertEqual(set(self.ru), expected)
        self.assertTrue(self.en[objective_id].strip())
        self.assertTrue(self.ru[objective_id].strip())

    def test_specific_role_runtime_evidence_remains_explicitly_pending_final_pass(self):
        evidence = self.hvt["conditionEvidence"]
        self.assertEqual(evidence["spt413SpecificSavageRoleRuntimeProof"], "candidate-pending-final-content-runtime-pass")
        self.assertIn("physical SPT 4.1.3", evidence["gateA"])


if __name__ == "__main__":
    unittest.main()
