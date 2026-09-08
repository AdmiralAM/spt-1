import hashlib
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class M3RuntimeMaterializationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.runtime = json.loads((ROOT / "manifests/m3-runtime-materialization.json").read_text(encoding="utf-8"))
        cls.progression = json.loads((ROOT / "manifests/m3-campaign-progression.json").read_text(encoding="utf-8"))
        cls.spec = json.loads((ROOT / "manifests/m3-campaign-product-spec.json").read_text(encoding="utf-8"))
        cls.quests = {}
        for path in (ROOT / "db/quests").glob("30-*.json"):
            quest = json.loads(path.read_text(encoding="utf-8"))
            cls.quests[quest["_id"]] = quest

    def test_exact_operation_set_and_deterministic_ids(self):
        expected = {
            key: hashlib.sha256(f"com.admiralam.spt.admiraltrader:m3:{key}:quest".encode()).hexdigest()[:24]
            for key in self.progression["levels"]
        }
        self.assertEqual(self.runtime["questIds"], expected)
        self.assertEqual(set(self.quests), set(expected.values()))
        self.assertEqual(len(self.quests), 12)

    def test_graph_and_external_prerequisites_are_materialized(self):
        ids = self.runtime["questIds"]
        for key, qid in ids.items():
            starts = self.quests[qid]["conditions"]["AvailableForStart"]
            level = next(c for c in starts if c["conditionType"] == "Level")
            self.assertEqual(level["value"], self.progression["levels"][key])
            actual = {c["target"] for c in starts if c["conditionType"] == "Quest"}
            internal = {ids[p] for p in self.progression["prerequisites"][key]}
            external = set(self.progression["externalPrerequisites"].get(key, []))
            self.assertEqual(actual, internal | external)
            self.assertTrue(all(c.get("status") == [4] for c in starts if c["conditionType"] == "Quest"))

    def test_reward_envelope_and_no_item_faucets(self):
        totals = {"xp": 0, "rub": 0, "standing": 0.0}
        for quest in self.quests.values():
            rewards = quest["rewards"]["Success"]
            self.assertEqual([r["type"] for r in rewards], ["Experience", "TraderStanding", "Item"])
            self.assertEqual(rewards[2]["items"][0]["_tpl"], "5449016a4bdc2d6f028b456f")
            totals["xp"] += rewards[0]["value"]
            totals["standing"] += rewards[1]["value"]
            totals["rub"] += rewards[2]["value"]
        self.assertEqual(totals["xp"], 106500)
        self.assertEqual(totals["rub"], 661000)
        self.assertAlmostEqual(totals["standing"], 0.157)

    def test_native_lifecycle_and_objective_locales(self):
        locales = {
            lang: json.loads((ROOT / f"db/locales/m3-{lang}.json").read_text(encoding="utf-8"))
            for lang in ("en", "ru")
        }
        for quest in self.quests.values():
            self.assertFalse(quest["instantComplete"])
            self.assertEqual(quest["acceptanceAndFinishingSource"], "eft")
            self.assertEqual(quest["progressSource"], "eft")
            self.assertEqual(quest["status"], 0)
            self.assertEqual(quest["rewards"]["Started"], [])
            for condition in quest["conditions"]["AvailableForFinish"]:
                for locale in locales.values():
                    self.assertIn(condition["id"], locale)
                    self.assertTrue(locale[condition["id"]].strip())

    def test_runtime_tpls_exist_in_exact_spt415_database_when_available(self):
        path = Path(r"C:\Users\amano\Documents\Codex\2026-09-06\new-chat\work\spt415\SPT_Runtime\SPT_Data\database\templates\items.json")
        if not path.exists():
            self.skipTest("exact local SPT 4.1.5 database is unavailable")
        items = json.loads(path.read_text(encoding="utf-8"))
        selected = set().union(*map(set, self.runtime["equipmentAllowlists"].values()))
        selected.update(["590c5bbd86f774785762df04", "57347c1124597737fb1379e3", "61bf83814088ec1a363d7097",
                         "572b7adb24597762ae139821", "56e33680d2720be2748b4576"])
        self.assertEqual(selected - set(items), set())


if __name__ == "__main__":
    unittest.main()
