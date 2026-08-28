import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUEST_DIR = ROOT / "db" / "quests"
LOCALE_DIR = ROOT / "db" / "locales"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class ObjectiveLocaleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [load(path) for path in sorted(QUEST_DIR.glob("*.json"))]
        cls.en = load(LOCALE_DIR / "objectives-en.json")
        cls.ru = load(LOCALE_DIR / "objectives-ru.json")

    def test_every_finish_condition_has_exact_en_ru_locale(self):
        ids = []
        for quest in self.quests:
            finish = quest["conditions"]["AvailableForFinish"]
            self.assertEqual(len(finish), 1, quest["_id"])
            ids.append(finish[0]["id"])

        self.assertEqual(len(ids), 31)
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(set(self.en), set(ids))
        self.assertEqual(set(self.ru), set(ids))

    def test_objective_text_is_player_facing_not_raw_ids(self):
        for locale in (self.en, self.ru):
            for condition_id, text in locale.items():
                self.assertTrue(text.strip(), condition_id)
                self.assertNotEqual(text.strip().lower(), condition_id.lower())
                self.assertNotIn("tpl", text.lower())
                self.assertNotIn("condition", text.lower())

    def test_qualification_objectives_describe_one_combat_proof(self):
        qualifications = [
            quest for quest in self.quests
            if quest["QuestName"].startswith("Arsenal Protocol:") and quest["QuestName"].endswith("- Qualification")
        ]
        self.assertEqual(len(qualifications), 7)
        for quest in qualifications:
            condition = quest["conditions"]["AvailableForFinish"][0]
            self.assertEqual(condition["conditionType"], "CounterCreator")
            self.assertEqual(int(condition["value"]), 1)
            self.assertIn("Eliminate 1", self.en[condition["id"]])
            self.assertIn("Устраните 1", self.ru[condition["id"]])

    def test_combat_objectives_match_runtime_counter_values(self):
        combat = [quest for quest in self.quests if quest["QuestName"].startswith("Arsenal Protocol:")]
        self.assertEqual(len(combat), 21)
        for quest in combat:
            condition = quest["conditions"]["AvailableForFinish"][0]
            self.assertEqual(condition["conditionType"], "CounterCreator")
            expected = str(condition["value"])
            self.assertIn(expected, self.en[condition["id"]], quest["_id"])
            self.assertIn(expected, self.ru[condition["id"]], quest["_id"])


if __name__ == "__main__":
    unittest.main()
