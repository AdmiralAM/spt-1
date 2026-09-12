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
        kinds = {kind: 0 for kind in ("visit", "retrieveQuestItem", "placeOrMark", "eliminate", "surviveExtract", "handover", "possessAccessKey")}
        for chain in self.authored["chains"]:
            for row in chain["quests"]:
                for objective in row["objectives"]:
                    kinds[objective["kind"]] += 1
                    if objective["kind"] == "eliminate":
                        self.assertLessEqual(objective["quantity"], 12)
        self.assertEqual(kinds, {
            "visit": 30,
            "retrieveQuestItem": 45,
            "placeOrMark": 24,
            "eliminate": 8,
            "surviveExtract": 17,
            "handover": 8,
            "possessAccessKey": 3,
        })

    def test_story_access_keys_are_owned_not_handed_over(self):
        expected = {
            "ed21744058dec7587de1081f": {
                "5672c92d4bdc2d180f8b4567", "5780cda02459777b272ede61",
                "5780cf692459777de4559321", "5780cf722459777a5108b9a1",
            },
            "30d087339ef8063ccd818036": {
                "57a349b2245977762b199ec7", "593858c486f774253a24cb52",
            },
            "78143d5331afbc8ed5530c51": {
                "5a0dc45586f7742f6b0b73e3", "5a0dc95c86f77452440fc675",
                "5a0ea64786f7741707720468", "5a0ea79b86f7741d4a35298e",
            },
        }
        for quest_id, targets in expected.items():
            finish = self.by_id[quest_id]["conditions"]["AvailableForFinish"]
            key_conditions = [row for row in finish if row["conditionType"] == "FindItem" and set(row["target"]) == targets]
            self.assertEqual(len(key_conditions), 1, quest_id)
            self.assertFalse(key_conditions[0]["onlyFoundInRaid"], quest_id)
            self.assertFalse(any(row["conditionType"] == "HandoverItem" and set(row["target"]) == targets for row in finish), quest_id)
            self.assertIn("ключ не сдаётся", self.ru[quest_id + " description"], quest_id)

    def test_authored_recovery_beats_require_real_recovery_conditions(self):
        corrected = {
            "bb49cbdbae242ffef21f95b7", "837bdd0ab80a2a1382caeedd",
            "e74018c43dc3f47551445192", "78143d5331afbc8ed5530c51",
            "aef99c7f97ca615cf101a654", "fcc999aeb0be8899307d5c11",
            "42d9c21068539c9855e42daf", "cbf1ff74b68b7026ebae3bf6",
        }
        for quest_id in corrected:
            kinds = [row["conditionType"] for row in self.by_id[quest_id]["conditions"]["AvailableForFinish"]]
            self.assertIn("FindItem", kinds, quest_id)
            self.assertIn("HandoverItem", kinds, quest_id)
        nested = [row for row in self.by_id["e74018c43dc3f47551445192"]["conditions"]["AvailableForFinish"] if row["conditionType"] == "CounterCreator"]
        self.assertTrue(any(any(c["conditionType"] == "ExitStatus" for c in row["counter"]["conditions"]) for row in nested))

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

    def test_story_copy_exposes_position_and_next_operation(self):
        for chain in self.authored["chains"]:
            for index, row in enumerate(chain["quests"]):
                description = self.ru[row["id"] + " description"]
                success = self.ru[row["id"] + " successMessageText"]
                self.assertIn(f"этап {row['order']} из 10", description, row["id"])
                self.assertIn("Оперативная сводка:\n" + row["brief"], description, row["id"])
                self.assertIn("Требования:\n- ", description, row["id"])
                if index + 1 < len(chain["quests"]):
                    self.assertIn(f"Следующая операция: «{chain['quests'][index + 1]['name']}»", success, row["id"])
                else:
                    self.assertIn("Расследование закрыто", success, row["id"])

    def test_authored_briefs_do_not_claim_unenforced_session_or_time_constraints(self):
        unsupported_claims = ("в одном рейде", "в том же рейде", "в ночное время")
        for chain in self.authored["chains"]:
            for row in chain["quests"]:
                brief = row["brief"].lower()
                self.assertFalse(any(claim in brief for claim in unsupported_claims), row["id"])

    def test_item_objective_quantities_match_the_native_materializer(self):
        for chain in self.authored["chains"]:
            for row in chain["quests"]:
                for objective in row["objectives"]:
                    if objective["kind"] in {"retrieveQuestItem", "handover"}:
                        self.assertEqual(objective["quantity"], 1, row["id"])

    def test_russian_story_locale_is_real_utf8_cyrillic(self):
        rendered = json.dumps(self.ru, ensure_ascii=False)
        self.assertNotIn("\ufffd", rendered)
        self.assertGreater(sum("\u0400" <= char <= "\u04ff" for char in rendered), 100000)


if __name__ == "__main__":
    unittest.main()
