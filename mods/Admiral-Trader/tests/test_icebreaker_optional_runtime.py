import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class IcebreakerOptionalRuntimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads((ROOT / "manifests/icebreaker-runtime.json").read_text(encoding="utf-8"))
        cls.quests = [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "db/optional/icebreaker/quests").glob("*.json"))]
        cls.en = json.loads((ROOT / "db/optional/icebreaker/locales/en.json").read_text(encoding="utf-8"))
        cls.ru = json.loads((ROOT / "db/optional/icebreaker/locales/ru.json").read_text(encoding="utf-8"))

    def test_exact_runtime_identity_and_conditional_shape(self):
        self.assertFalse(self.manifest["requiredDependency"])
        self.assertEqual(self.manifest["coreQuestCount"], 172)
        self.assertEqual(self.manifest["optionalQuestCount"], 10)
        self.assertEqual(self.manifest["location"]["id"], "882b2fa04bbd616567022938")
        self.assertEqual(len(self.quests), 10)

    def test_chain_is_linear_and_owned_by_admiral(self):
        expected = self.manifest["entryPrerequisite"]["questId"]
        for quest in self.quests:
            starts = quest["conditions"]["AvailableForStart"]
            prereq = next(x["target"] for x in starts if x["conditionType"] == "Quest")
            self.assertEqual(prereq, expected)
            self.assertEqual(quest["traderId"], "d5c27bb3169f8dfbc13f6b69")
            expected = quest["_id"]

    def test_native_lifecycle_and_no_custom_item_ownership(self):
        forbidden_item_ids = {"699f0b877c23862b4b0ee19c", "6a63fa5726a91c3479b6c575", "699f09767852da66a7003061", "699f09de81a6c812900a77b7", "69bb435ff609db77390b0e25", "69bb43df99f3fda8f1072483"}
        for quest in self.quests:
            self.assertFalse(quest["instantComplete"])
            self.assertEqual(quest["acceptanceAndFinishingSource"], "eft")
            self.assertEqual(quest["rewards"]["Started"], [])
            text = json.dumps(quest)
            self.assertTrue(all(item_id not in text for item_id in forbidden_item_ids))

    def test_locale_copy_is_multiline_and_complete(self):
        fields = ("name", "description", "note", "startedMessageText", "successMessageText", "failMessageText", "acceptPlayerMessage", "declinePlayerMessage", "completePlayerMessage", "changeQuestMessageText")
        for quest in self.quests:
            for locale in (self.en, self.ru):
                for field in fields:
                    self.assertIn(f"{quest['_id']} {field}", locale)
                self.assertIn("\n", locale[f"{quest['_id']} description"])

    def test_core_graph_never_depends_on_optional_ids(self):
        optional_ids = {q["_id"] for q in self.quests}
        for path in (ROOT / "db/quests").glob("*.json"):
            self.assertTrue(optional_ids.isdisjoint(path.read_text(encoding="utf-8")))

    def test_resort_zone_is_bound_to_shoreline(self):
        quest = self.quests[4]
        conditions = quest["conditions"]["AvailableForFinish"][0]["counter"]["conditions"]
        self.assertIn("boreas_camp_resort", next(x for x in conditions if x["conditionType"] == "InZone")["zoneIds"])
        self.assertEqual(next(x for x in conditions if x["conditionType"] == "Location")["target"], ["Shoreline"])

    def test_even_steps_have_moderate_native_item_rewards(self):
        expected = {2:"5d02797c86f774203f38e30a",4:"617aa4dd8166f034d57de9c5",6:"5ed51652f6c34d2cc26336a1",8:"5c0e534186f7747fa1419867",10:"5d1b376e86f774252519444e"}
        for order, tpl in expected.items():
            items = [x for x in self.quests[order-1]["rewards"]["Success"] if x["type"] == "Item"]
            self.assertIn(tpl, {x["items"][0]["_tpl"] for x in items})


if __name__ == "__main__":
    unittest.main()
