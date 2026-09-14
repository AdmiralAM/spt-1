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

    def test_russian_arsenal_copy_is_localized_and_names_every_allowed_weapon(self):
        russian = self.locales["ru"]
        weapon_quests = []
        for quest in self.quests:
            conditions = quest["conditions"]["AvailableForFinish"]
            if not conditions or conditions[0].get("conditionType") != "CounterCreator":
                continue
            nested = conditions[0].get("counter", {}).get("conditions", [])
            if any(row.get("weapon") for row in nested):
                weapon_quests.append(quest)
        self.assertEqual(len(weapon_quests), 40)
        for quest in weapon_quests:
            qid = quest["_id"]
            self.assertNotRegex(russian[qid + " name"], r"^(Arsenal Rotation|Operation:|Loadout:)")
            self.assertIn("Уточнение:", russian[qid + " description"])
            self.assertIn("Разрешённое оружие:", russian[qid + " description"])

    def test_m8_descriptions_keep_only_the_detail_missing_from_native_objective_rows(self):
        russian = self.locales["ru"]
        expansion = json.loads((ROOT / "manifests/m8-campaign-expansion-runtime.json").read_text(encoding="utf-8"))
        for row in expansion["quests"]:
            description = russian[row["id"] + " description"]
            self.assertNotIn("Награды", description)
            self.assertNotIn("Прогресс сохраняется между рейдами", description)
            if row["kind"] == "weapon":
                self.assertIn("Разрешённое оружие:", description)
                self.assertNotIn("Устранить ", description)

    def test_questname_fallbacks_match_russian_titles(self):
        russian = self.locales["ru"]
        for quest in self.quests:
            title = russian[quest["_id"] + " name"]
            self.assertEqual(quest["QuestName"], title)
            self.assertRegex(title, r"[А-Яа-яЁё]")

    def test_equipment_qualifications_require_combat_and_explain_exact_gear(self):
        ids = {
            "4a8f533e1ed458e83b41c01f", "4ab0b49478adb233ae900b33",
            "ca33fab8b9cc5f5f5ad322c0", "9c35b3ac22ede1a5a79118bc",
            "ee813142de655daf2dedfebc", "47480d824cea0b80917cafa5",
        }
        by_id = {quest["_id"]: quest for quest in self.quests}
        for qid in ids:
            counter = by_id[qid]["conditions"]["AvailableForFinish"][0]
            nested = {row["conditionType"] for row in counter["counter"]["conditions"]}
            self.assertTrue({"Equipment", "Kills", "Location", "ExitStatus"} <= nested, qid)
            self.assertTrue(counter["oneSessionOnly"], qid)
            self.assertIn("Уточнение:", self.locales["ru"][qid + " description"], qid)
            self.assertIn("Задача:", self.locales["ru"][qid + " description"], qid)

    def test_acoustic_discipline_names_all_five_allowed_headsets_and_woods(self):
        qid = "8dad0d354ac000b7bbf05b9a"
        quest = next(q for q in self.quests if q["_id"] == qid)
        self.assertEqual(quest["location"], "Woods")
        description = self.locales["ru"][qid + " description"]
        for name in ("ГСШ-01", "Peltor Tactical Sport", "Walker’s Razor Digital", "OPSMEN Earmor M32", "Peltor ComTac IV Hybrid"):
            self.assertIn(name, description)

    def test_single_map_runtime_conditions_are_not_presented_as_any_location(self):
        aliases = {"Sandbox": "Sandbox", "Sandbox_high": "Sandbox", "factory4_day": "factory4_day", "factory4_night": "factory4_day"}
        for quest in self.quests:
            locations = []
            def visit(value):
                if isinstance(value, dict):
                    if value.get("conditionType") == "Location": locations.extend(value.get("target", []))
                    for child in value.values(): visit(child)
                elif isinstance(value, list):
                    for child in value: visit(child)
            visit(quest["conditions"])
            logical = {aliases.get(value, value) for value in locations}
            if len(logical) == 1:
                self.assertNotEqual(quest["location"], "any", quest["_id"])


if __name__ == "__main__":
    unittest.main()
