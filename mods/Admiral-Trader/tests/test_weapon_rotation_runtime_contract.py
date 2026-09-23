import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SCAVS = {"9a2efe4c869cd41beb667e29", "551415adc72a84fa9e6bb571"}
PMCS = {
    "5c7e60f203900e75aba3edc0", "de92651782ad58fce6457af9",
    "43d9544a09d068476a1a18df", "fc14500bbc2900a04647083d",
    "33810921ad5c893b866b3951", "f6e51dc4e50e47ee9af50a4d",
}
SURVIVE_AFTER = {
    "9620e5a3c17b30599df67730", "d7f67e37b0c244878aa685e0",
    "8d8d81032315f4fdc5a06798", "b15dc178e2982874350db164",
    "570d250679328757614dcbcb",
}
ONE_RAID = {"88118e994f26cab3bee1521d", "ffb63228a333c8b0755741ea"}
DISTANCES = {
    "3ccca027eeebaf0a4dcfbb6f": ("<=", 25),
    "de92651782ad58fce6457af9": ("<=", 25),
    "96ef708ef07d2b7bd8214653": ("<=", 60),
    "8cba3e2ec639a4aa2c26c4da": ("<=", 25),
    "88118e994f26cab3bee1521d": ("<=", 25),
    "5a9a0bb3ffddd538e6fe3842": (">=", 40),
    "a0d05e28971f1ba57639b97d": (">=", 100),
    "153839f368b80b6fbc36d29e": (">=", 200),
    "cd2641c70bede98dac3945d0": (">=", 300),
}


def walk(value):
    if isinstance(value, dict):
        if value.get("conditionType"):
            yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


class WeaponRotationRuntimeContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = json.loads((ROOT / "manifests/weapon-rotation-expansion-plan.json").read_text(encoding="utf-8"))
        cls.runtime = json.loads((ROOT / "manifests/weapon-rotation-runtime.json").read_text(encoding="utf-8"))
        cls.quests = {
            json.loads(path.read_text(encoding="utf-8"))["_id"]: json.loads(path.read_text(encoding="utf-8"))
            for path in (ROOT / "db/quests").glob("*.json")
        }

    def test_runtime_targets_match_scav_and_pmc_contracts(self):
        for quest_id in SCAVS:
            kill = next(node for node in walk(self.quests[quest_id]["conditions"]["AvailableForFinish"]) if node.get("conditionType") == "Kills")
            self.assertEqual("Savage", kill["target"], quest_id)
        for quest_id in PMCS:
            kill = next(node for node in walk(self.quests[quest_id]["conditions"]["AvailableForFinish"]) if node.get("conditionType") == "Kills")
            self.assertEqual("AnyPmc", kill["target"], quest_id)

    def test_distance_objectives_are_real_and_match_the_authored_threshold(self):
        for quest_id, expected in DISTANCES.items():
            kill = next(node for node in walk(self.quests[quest_id]["conditions"]["AvailableForFinish"]) if node.get("conditionType") == "Kills")
            self.assertEqual({"compareMethod": expected[0], "value": expected[1]}, kill["distance"], quest_id)

    def test_survival_objectives_require_a_successful_extraction_after_the_kill_stage(self):
        for quest_id in SURVIVE_AFTER:
            quest = self.quests[quest_id]
            elimination = next(node for node in quest["conditions"]["AvailableForFinish"] if node.get("conditionType") == "CounterCreator" and node.get("type") == "Elimination")
            completion = [node for node in quest["conditions"]["AvailableForFinish"] if node.get("conditionType") == "CounterCreator" and node.get("type") == "Completion"]
            self.assertEqual(1, len(completion), quest_id)
            self.assertEqual(elimination["id"], completion[0]["visibilityConditions"][0]["target"], quest_id)
            condition_types = {node.get("conditionType") for node in completion[0]["counter"]["conditions"]}
            self.assertEqual({"ExitStatus", "Location"}, condition_types, quest_id)
            status = next(node for node in completion[0]["counter"]["conditions"] if node["conditionType"] == "ExitStatus")
            self.assertEqual(["Survived"], status["status"], quest_id)

    def test_one_raid_and_night_suppression_requirements_are_enforced(self):
        for quest_id in ONE_RAID:
            quest = self.quests[quest_id]
            elimination = next(node for node in quest["conditions"]["AvailableForFinish"] if node.get("conditionType") == "CounterCreator" and node.get("type") == "Elimination")
            kill = next(node for node in walk(elimination) if node.get("conditionType") == "Kills")
            self.assertTrue(elimination["oneSessionOnly"], quest_id)
            self.assertTrue(kill["resetOnSessionEnd"], quest_id)
        quest_id = "59ca4829e098dfafa03888d2"
        kill = next(node for node in walk(self.quests[quest_id]["conditions"]["AvailableForFinish"]) if node.get("conditionType") == "Kills")
        self.assertEqual({"from": 22, "to": 5}, kill["daytime"])
        self.assertTrue(kill["weaponModsInclusive"], "night suppressed-sidearm quest has no compatible suppressors")

    def test_each_rotation_description_names_true_targets_maps_and_weapon_pool(self):
        for row in self.runtime["assignments"]:
            quest_id = row["id"]
            quest_file = next(path for path in (ROOT / "db/quests").glob("*.json") if json.loads(path.read_text(encoding="utf-8"))["_id"] == quest_id)
            stem = "m8" if quest_file.name.startswith("40-") else "arsenal"
            locale = json.loads((ROOT / f"db/locales/{stem}-ru.json").read_text(encoding="utf-8"))
            description = locale[f"{quest_id} description"]
            self.assertIn("Доступные карты:", description, quest_id)
            self.assertIn("Разрешённое оружие:", description, quest_id)
            self.assertNotIn("Убийства ЧВК", description, quest_id)
            self.assertTrue(locale[f"{quest_id} successMessageText"], quest_id)
            self.assertEqual(locale[f"{quest_id} successMessageText"], locale[f"{quest_id} completePlayerMessage"], quest_id)
        self.assertIn("Эпицентр", json.loads((ROOT / "db/locales/m8-ru.json").read_text(encoding="utf-8"))["eb93814dd020bdc131d526aa description"])


if __name__ == "__main__":
    unittest.main()
