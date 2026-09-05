import json
import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
QUEST_DIR = ROOT / "db" / "quests"
SERVER_DIR = ROOT / "server"


class M1NativeLifecycleContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = []
        for path in sorted(QUEST_DIR.glob("*.json")):
            cls.quests.append((path, json.loads(path.read_text(encoding="utf-8"))))

    def test_all_31_templates_require_explicit_native_accept_and_complete(self):
        self.assertEqual(len(self.quests), 31)
        for path, quest in self.quests:
            with self.subTest(path=path.name):
                self.assertIs(quest.get("instantComplete"), False)
                self.assertEqual(quest.get("acceptanceAndFinishingSource"), "eft")
                self.assertNotIn("status", quest)
                self.assertNotIn("sptStatus", quest)
                self.assertIs(quest.get("restartable"), False)

    def test_templates_do_not_encode_automatic_lifecycle_state_conditions(self):
        for path, quest in self.quests:
            conditions = quest.get("conditions") or {}
            with self.subTest(path=path.name):
                self.assertEqual(conditions.get("Started"), [])
                self.assertEqual(conditions.get("Success"), [])
                self.assertEqual(conditions.get("Fail"), [])
                self.assertTrue(conditions.get("AvailableForStart"))
                self.assertTrue(conditions.get("AvailableForFinish"))

    def test_no_rewards_are_issued_at_accept_time(self):
        for path, quest in self.quests:
            rewards = quest.get("rewards") or {}
            with self.subTest(path=path.name):
                self.assertEqual(rewards.get("Started"), [])
                self.assertEqual(rewards.get("Fail"), [])
                self.assertTrue(rewards.get("Success"))

    def test_server_registration_layer_has_no_profile_lifecycle_mutation_calls(self):
        source = "\n".join(
            path.read_text(encoding="utf-8")
            for path in (
                SERVER_DIR / "RuntimeFoundation.cs",
                SERVER_DIR / "TraderRegistration.cs",
                SERVER_DIR / "QuestRegistration.cs",
            )
        )
        forbidden = (
            r"\bAcceptQuest\s*\(",
            r"\bCompleteQuest\s*\(",
            r"\bResetQuestState\s*\(",
            r"\bGetQuestReadyForProfile\s*\(",
            r"\bpmcData\.Quests\b",
            r"QuestStatusEnum\.(Started|AvailableForFinish|Success)",
        )
        for pattern in forbidden:
            with self.subTest(pattern=pattern):
                self.assertIsNone(re.search(pattern, source), pattern)


if __name__ == "__main__":
    unittest.main()
