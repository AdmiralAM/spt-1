import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class QuestQualityRuntimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "db/quests").glob("*.json"))]
        cls.locales = {}
        for language, files in {"en": ("en.json", "arsenal-en.json", "m3-en.json", "m8-en.json"), "ru": ("ru.json", "arsenal-ru.json", "m3-ru.json", "m8-ru.json")}.items():
            merged = {}
            for filename in files:
                payload = json.loads((ROOT / "db/locales" / filename).read_text(encoding="utf-8"))
                overlap = set(merged) & set(payload)
                if overlap:
                    raise AssertionError(f"duplicate {language} locale keys: {sorted(overlap)[:3]}")
                merged.update(payload)
            cls.locales[language] = merged

    def test_all_runtime_quests_have_explicit_requirements_rewards_and_objectives(self):
        self.assertEqual(len(self.quests), 72)
        for quest in self.quests:
            qid = quest["_id"]
            for language, locale in self.locales.items():
                description = locale[f"{qid} description"]
                started = locale[f"{qid} startedMessageText"]
                success = locale[f"{qid} successMessageText"]
                req = "Requirements:" if language == "en" else "Требования:"
                rewards = "Rewards:" if language == "en" else "Награды:"
                self.assertIn(req, description, qid)
                self.assertIn(rewards, description, qid)
                self.assertIn(f"{req}\n- ", description, qid)
                self.assertIn(f"{rewards}\n- ", description, qid)
                self.assertIn(req, started, qid)
                self.assertIn(rewards, success, qid)
                for condition in quest["conditions"]["AvailableForFinish"]:
                    objective = locale.get(condition["id"], "")
                    self.assertTrue(objective.strip(), f"{language}: {condition['id']}")
                    self.assertLessEqual(len(objective), 96, f"objective row is not UI-legible: {condition['id']}")
                    self.assertNotIn("\n", objective, condition["id"])
                    if condition["conditionType"] in ("FindItem", "HandoverItem"):
                        fir = "Found in raid:" if language == "en" else "Статус «Найдено в рейде»:"
                        self.assertIn(fir, description, condition["id"])
                    else:
                        location = ("on " if language == "en" else "на ")
                        self.assertIn(location, description.lower(), condition["id"])

    def test_copy_pass_does_not_change_runtime_contracts(self):
        manifest = json.loads((ROOT / "manifests/quest-quality-runtime.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["questCount"], 43)
        historical = set(manifest["quests"])
        expansion = json.loads((ROOT / "manifests/m8-campaign-expansion-runtime.json").read_text(encoding="utf-8"))
        self.assertEqual(historical | {q["id"] for q in expansion["quests"]}, {q["_id"] for q in self.quests})
        for quest in (q for q in self.quests if q["_id"] in historical):
            self.assertEqual(
                manifest["quests"][quest["_id"]]["finishConditionIds"],
                [c["id"] for c in quest["conditions"]["AvailableForFinish"]],
            )


if __name__ == "__main__":
    unittest.main()
