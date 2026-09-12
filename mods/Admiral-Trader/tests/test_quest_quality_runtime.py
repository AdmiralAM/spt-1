import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class QuestQualityRuntimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "db/quests").glob("*.json"))]
        cls.locales = {}
        for language, files in {"en": ("en.json", "arsenal-en.json", "m3-en.json", "m8-en.json", "story-en.json"), "ru": ("ru.json", "arsenal-ru.json", "m3-ru.json", "m8-ru.json", "story-ru.json")}.items():
            merged = {}
            for filename in files:
                payload = json.loads((ROOT / "db/locales" / filename).read_text(encoding="utf-8"))
                overlap = set(merged) & set(payload)
                if overlap:
                    raise AssertionError(f"duplicate {language} locale keys: {sorted(overlap)[:3]}")
                merged.update(payload)
            cls.locales[language] = merged

    def test_all_runtime_quests_use_native_panels_without_copy_duplication(self):
        self.assertEqual(len(self.quests), 172)
        for quest in self.quests:
            qid = quest["_id"]
            for language, locale in self.locales.items():
                description = locale[f"{qid} description"]
                started = locale[f"{qid} startedMessageText"]
                success = locale[f"{qid} successMessageText"]
                forbidden = ("Requirements:", "Требования:", "Rewards:", "Награды:")
                for text in (description, started, success, locale[f"{qid} acceptPlayerMessage"], locale[f"{qid} completePlayerMessage"]):
                    self.assertFalse(any(label in text for label in forbidden), qid)
                for condition in quest["conditions"]["AvailableForFinish"]:
                    objective = locale.get(condition["id"], "")
                    self.assertTrue(objective.strip(), f"{language}: {condition['id']}")
                    self.assertLessEqual(len(objective), 140, f"objective row is not UI-legible: {condition['id']}")
                    self.assertNotIn("\n", objective, condition["id"])

    def test_copy_pass_does_not_change_runtime_contracts(self):
        manifest = json.loads((ROOT / "manifests/quest-quality-runtime.json").read_text(encoding="utf-8"))
        self.assertEqual(manifest["questCount"], 43)
        historical = set(manifest["quests"])
        expansion = json.loads((ROOT / "manifests/m8-campaign-expansion-runtime.json").read_text(encoding="utf-8"))
        story = json.loads((ROOT / "manifests/story-campaign-runtime.json").read_text(encoding="utf-8"))
        self.assertEqual(historical | {q["id"] for q in expansion["quests"]} | {q["id"] for q in story["quests"]}, {q["_id"] for q in self.quests})
        for quest in (q for q in self.quests if q["_id"] in historical):
            self.assertEqual(
                manifest["quests"][quest["_id"]]["finishConditionIds"],
                [c["id"] for c in quest["conditions"]["AvailableForFinish"]],
            )


if __name__ == "__main__":
    unittest.main()
