import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ROUBLES = "5449016a4bdc2d6f028b456f"
RETAINED_KEY_QUESTS = {
    "5d404ebd654de4efecef71d2", "1b92e4cf212d895be4f70b2c", "cba3374f11375c4e7eea9efe",
    "80c11744c2310e1bfe160a8b", "42ff30bc152b8212dc15b20a", "d737e3b990c371708db369f0",
    "9c438fa48f645044ddc75e8d", "59a36c5ee68210f9154d94d0", "a8b05eb5c5f0e67ee881df52",
    "68a6527a3c73b2e85977d7a1", "ed21744058dec7587de1081f", "30d087339ef8063ccd818036",
    "78143d5331afbc8ed5530c51",
}


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class QuestProgressAndBarterContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [json.loads(path.read_text(encoding="utf-8")) for path in (ROOT / "db/quests").glob("*.json")]
        cls.retained_key_tpls = {
            target
            for quest in cls.quests if quest["_id"] in RETAINED_KEY_QUESTS
            for row in quest["conditions"]["AvailableForFinish"] if row.get("conditionType") == "FindItem"
            for target in row.get("target") or []
        }

    def test_all_counter_and_finish_condition_ids_are_globally_unique(self):
        seen = set()
        for quest in self.quests:
            for condition in quest["conditions"]["AvailableForFinish"]:
                for value in (condition.get("id"), (condition.get("counter") or {}).get("id")):
                    if value:
                        self.assertNotIn(value, seen, f"reused progress identity {value}")
                        seen.add(value)

    def test_every_non_key_find_requirement_has_matching_handover(self):
        # Keys are deliberately retained for later access. Consumables, supplies and quest items are surrendered.
        for quest in self.quests:
            finish = quest["conditions"]["AvailableForFinish"]
            handovers = [row for row in finish if row.get("conditionType") == "HandoverItem"]
            for find in (row for row in finish if row.get("conditionType") == "FindItem"):
                targets = set(find.get("target") or [])
                if len(targets) > 1 and targets <= self.retained_key_tpls:
                    continue
                self.assertTrue(any(set(row.get("target") or []) == targets and row.get("value") == find.get("value") for row in handovers), quest["_id"])

    def test_selected_specialist_stock_uses_real_item_barters(self):
        policy = load("manifests/storefront-barter-policy.json")
        assort = load("db/assort.json")
        self.assertEqual(6, len(policy["offers"]))
        for offer in policy["offers"]:
            scheme = assort["barter_scheme"][offer["offerId"]]
            self.assertEqual(scheme, [[{"count": row["count"], "_tpl": row["tpl"]} for row in offer["requirements"]]])
            self.assertTrue(all(row["_tpl"] != ROUBLES for row in scheme[0]))

    def test_flir_barter_uses_radar_array_virtex_and_military_cable(self):
        policy = load("manifests/storefront-barter-policy.json")
        offer = next(row for row in policy["offers"] if row["offerId"] == "6b0d153489b984ae9b940adb")
        self.assertEqual(
            [
                {"tpl": "5d03775b86f774203e7e0c4b", "count": 1},
                {"tpl": "5c05308086f7746b2101e90b", "count": 1},
                {"tpl": "5d0375ff86f774186372f685", "count": 2},
            ],
            offer["requirements"],
        )


if __name__ == "__main__":
    unittest.main()
