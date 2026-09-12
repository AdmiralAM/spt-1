import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class StoryCampaignRuntimeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.authored = json.loads((ROOT / "manifests/campaign-authored-100-review.json").read_text(encoding="utf-8"))
        cls.runtime = json.loads((ROOT / "manifests/story-campaign-runtime.json").read_text(encoding="utf-8"))
        cls.story_files = sorted((ROOT / "db/quests").glob("60-*.json"))
        cls.story = [json.loads(path.read_text(encoding="utf-8")) for path in cls.story_files]
        cls.by_id = {quest["_id"]: quest for quest in cls.story}
        cls.en = json.loads((ROOT / "db/locales/story-en.json").read_text(encoding="utf-8"))
        cls.ru = json.loads((ROOT / "db/locales/story-ru.json").read_text(encoding="utf-8"))
        cls.assort = json.loads((ROOT / "db/assort.json").read_text(encoding="utf-8"))
        cls.questassort = json.loads((ROOT / "db/questassort.json").read_text(encoding="utf-8"))

    def test_exact_story_shape_and_identity(self):
        self.assertEqual(len(self.story), 100)
        self.assertEqual(len(self.by_id), 100)
        self.assertEqual(self.runtime["storyQuestCount"], 100)
        self.assertEqual(self.runtime["totalQuestCount"], 172)
        self.assertEqual({row["id"] for row in self.runtime["quests"]}, set(self.by_id))
        self.assertTrue(all(q["traderId"] == "d5c27bb3169f8dfbc13f6b69" for q in self.story))

    def test_ten_linear_chains_have_authored_cross_chain_gates(self):
        self.assertEqual(len(self.authored["chains"]), 10)
        for chain in self.authored["chains"]:
            self.assertEqual(len(chain["quests"]), 10)
            for index, row in enumerate(chain["quests"]):
                quest = self.by_id[row["id"]]
                prerequisites = [c["target"] for c in quest["conditions"]["AvailableForStart"] if c["conditionType"] == "Quest"]
                expected = ([] if index == 0 else [chain["quests"][index - 1]["id"]])
                expected += [self.authored["chains"][x["chain"] - 1]["quests"][-1]["id"] for x in row["crossChainPrerequisites"]]
                self.assertEqual(prerequisites, expected, row["id"])

    def test_objective_mix_is_story_led_and_bounded(self):
        kinds = {kind: 0 for kind in ("visit", "retrieveQuestItem", "placeOrMark", "eliminate", "surviveExtract", "handover")}
        for chain in self.authored["chains"]:
            for row in chain["quests"]:
                for objective in row["objectives"]:
                    kinds[objective["kind"]] += 1
                    if objective["kind"] == "eliminate":
                        self.assertLessEqual(objective["quantity"], 12)
        self.assertGreaterEqual(kinds["visit"], 30)
        self.assertGreaterEqual(kinds["retrieveQuestItem"], 35)
        self.assertGreaterEqual(kinds["placeOrMark"], 25)
        self.assertLessEqual(kinds["eliminate"], 12)

    def test_runtime_uses_only_supported_native_condition_types(self):
        allowed = {"CounterCreator", "FindItem", "HandoverItem", "PlaceBeacon"}
        for quest in self.story:
            finish = quest["conditions"]["AvailableForFinish"]
            self.assertTrue(finish, quest["_id"])
            self.assertTrue({row["conditionType"] for row in finish} <= allowed, quest["_id"])
            for row in finish:
                if row["conditionType"] == "PlaceBeacon":
                    self.assertEqual(row["target"], ["5991b51486f77447b112d44f"])
                    self.assertTrue(row["zoneId"])

    def test_markers_are_unique_per_quest_and_recoveries_require_a_site_visit(self):
        authored_by_id = {q["id"]: q for chain in self.authored["chains"] for q in chain["quests"]}
        for quest in self.story:
            finish = quest["conditions"]["AvailableForFinish"]
            marker_zones = [row["zoneId"] for row in finish if row["conditionType"] == "PlaceBeacon"]
            self.assertEqual(len(marker_zones), len(set(marker_zones)), quest["_id"])
            if any(row["kind"] == "retrieveQuestItem" for row in authored_by_id[quest["_id"]]["objectives"]):
                nested = [condition for row in finish if row["conditionType"] == "CounterCreator" for condition in row["counter"]["conditions"]]
                self.assertTrue(any(row["conditionType"] == "VisitPlace" for row in nested), quest["_id"])

    def test_natalya_is_integrated_without_second_trader_or_dependency(self):
        natalya = [row for row in self.runtime["quests"] if row["natalya"]]
        self.assertEqual(len(natalya), 18)
        self.assertEqual(self.runtime["natalyaMode"], "specialist-inside-admiral-no-second-trader")
        for row in natalya:
            self.assertIn("Наталья", self.ru[row["id"] + " description"])
        project = (ROOT / "server/AdmiralTrader.Server.csproj").read_text(encoding="utf-8")
        self.assertNotIn("Natalya", project)

    def test_reward_pressure_and_final_unlock_intent_are_bounded(self):
        authored = [q for chain in self.authored["chains"] for q in chain["quests"]]
        self.assertEqual(sum(q["rewards"]["assortmentUnlock"] is not None for q in authored), 10)
        self.assertLessEqual(max(q["rewards"]["xp"] for q in authored), 30000)
        self.assertLessEqual(max(q["rewards"]["roubles"] for q in authored), 110000)
        for quest in self.story:
            self.assertEqual(quest["rewards"]["Started"], [])
            self.assertEqual(quest["rewards"]["Fail"], [])

    def test_each_story_finale_materializes_one_finite_unlock(self):
        unlocks = self.runtime["assortmentUnlocks"]
        self.assertEqual(len(unlocks), 10)
        self.assertEqual(self.runtime["totalFiniteOfferCount"], 51)
        roots = {row["_id"]: row for row in self.assort["items"] if row.get("parentId") == "hideout"}
        for row in unlocks:
            self.assertEqual(self.questassort["success"][row["offerId"]], row["questId"])
            self.assertEqual(roots[row["offerId"]]["_tpl"], row["tpl"])
            self.assertFalse(roots[row["offerId"]]["upd"]["UnlimitedCount"])
            quest = self.by_id[row["questId"]]
            self.assertIn(row["offerId"], [reward.get("target") for reward in quest["rewards"]["Success"] if reward["type"] == "AssortmentUnlock"])
            self.assertIn("Открыта покупка:", self.ru[row["questId"] + " successMessageText"])


if __name__ == "__main__":
    unittest.main()
