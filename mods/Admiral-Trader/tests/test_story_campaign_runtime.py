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

    def test_ten_chains_overlap_through_authored_campaign_waves(self):
        self.assertEqual(len(self.authored["chains"]), 10)
        for chain in self.authored["chains"]:
            self.assertEqual(len(chain["quests"]), 10)
            for index, row in enumerate(chain["quests"]):
                quest = self.by_id[row["id"]]
                prerequisites = [c["target"] for c in quest["conditions"]["AvailableForStart"] if c["conditionType"] == "Quest"]
                expected = ([] if index == 0 else [chain["quests"][index - 1]["id"]])
                expected += [self.authored["chains"][x["chain"] - 1]["quests"][x["questOrder"] - 1]["id"] for x in row["crossChainPrerequisites"]]
                self.assertEqual(prerequisites, expected, row["id"])
        roots = [chain["quests"][0] for chain in self.authored["chains"][1:]]
        self.assertTrue(all(max(x["questOrder"] for x in row["crossChainPrerequisites"]) <= 5 for row in roots))
        self.assertTrue(all(x["questOrder"] < 10 for row in roots for x in row["crossChainPrerequisites"]))

    def test_single_map_story_quests_expose_their_map_in_client_metadata(self):
        expected = {
            "Эпицентр": "653e6760052c01c1c805532f",
            "Таможня": "56f40101d2720b2a4d8b45d6",
            "Лес": "5704e3c2d2720bac5b8b4567",
            "Развязка": "5714dbc024597771384a510d",
            "Берег": "5704e554d2720bac5b8b456e",
            "Резерв": "5704e5fad2720bc05b8b4567",
            "Маяк": "5704e4dad2720bb55b8b4567",
            "Улицы": "5714dc692459777137212e12",
            "Завод": "55f2d3fd4bdc2d5f408b4567",
            "Лаборатория": "5b0fc42d86f7744a585f9105",
        }
        for chain in self.authored["chains"]:
            for row in chain["quests"]:
                map_name = row.get("mapOverride", chain["map"])
                self.assertEqual(self.by_id[row["id"]]["location"], expected[map_name], row["id"])

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
            "placeOrMark": 26,
            "eliminate": 9,
            "surviveExtract": 18,
            "handover": 8,
            "possessAccessKey": 3,
        })

    def test_story_access_keys_are_owned_not_handed_over(self):
        expected = {
            "ed21744058dec7587de1081f": {
                "59387a4986f77401cc236e62", "59148c8a86f774197930e983",
                "5780cf942459777df90dcb72", "5780cfa52459777dfb276eb1",
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
            self.assertTrue(key_conditions[0]["onlyFoundInRaid"], quest_id)
            self.assertFalse(any(row["conditionType"] == "HandoverItem" and set(row["target"]) == targets for row in finish), quest_id)
            self.assertIn("ключ не сдаётся", self.ru[quest_id + " description"], quest_id)

    def test_factory_master_key_names_both_exact_allowed_keys_in_the_objective(self):
        quest_id = "30d087339ef8063ccd818036"
        required_names = ("ключ от передней двери насосной станции", "ключ от задней двери насосной станции")
        objective = self.ru["40c83192f54d44de08c3171f"].lower()
        for name in required_names:
            self.assertIn(name, self.ru[quest_id + " description"].lower())
        self.assertIn("передней или задней двери насосной станции", objective)
        self.assertIn("в рейде", objective)
        authored = next(q for chain in self.authored["chains"] for q in chain["quests"] if q["id"] == quest_id)
        self.assertIn("найти в рейде", authored["brief"].lower())
        self.assertTrue(authored["objectives"][0]["foundInRaid"])

    def test_dispatcher_key_uses_a_distinct_common_dorm_pool_and_explains_it(self):
        quest_id = "ed21744058dec7587de1081f"
        description = self.ru[quest_id + " description"]
        for room in ("114", "204", "214", "220"):
            self.assertIn(room, description)
        self.assertIn("Найти в рейде", description)
        self.assertIn("останется у тебя", description)

    def test_english_story_copy_contains_no_russian_map_names(self):
        for key, value in self.en.items():
            self.assertNotRegex(value, r"[А-Яа-яЁё]", key)

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
        self.assertEqual(self.runtime["totalFiniteOfferCount"], 82)
        roots = {row["_id"]: row for row in self.assort["items"] if row.get("parentId") == "hideout"}
        for row in unlocks:
            self.assertEqual(self.questassort["success"][row["offerId"]], row["questId"])
            self.assertEqual(roots[row["offerId"]]["_tpl"], row["tpl"])
            self.assertFalse(roots[row["offerId"]]["upd"]["UnlimitedCount"])
            quest = self.by_id[row["questId"]]
            self.assertIn(row["offerId"], [reward.get("target") for reward in quest["rewards"]["Success"] if reward["type"] == "AssortmentUnlock"])

    def test_story_copy_exposes_position_and_next_operation(self):
        for chain in self.authored["chains"]:
            for index, row in enumerate(chain["quests"]):
                description = self.ru[row["id"] + " description"]
                success = self.ru[row["id"] + " successMessageText"]
                self.assertIn(f"этап {row['order']} из 10", description, row["id"])
                self.assertIn("Оперативная сводка:\n" + row["brief"], description, row["id"])
                self.assertNotIn("Требования:", description, row["id"])
                self.assertNotIn("Награды:", description, row["id"])
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
                expected_find = []
                expected_handover = []
                for objective in row["objectives"]:
                    if objective["kind"] in {"retrieveQuestItem", "handover"}:
                        self.assertEqual(objective["quantity"], 1, row["id"])
                        self.assertRegex(objective.get("itemTpl", ""), r"^[0-9a-f]{24}$", row["id"])
                        expected_handover.append(objective["itemTpl"])
                        if objective["kind"] == "retrieveQuestItem":
                            expected_find.append(objective["itemTpl"])
                finish = self.by_id[row["id"]]["conditions"]["AvailableForFinish"]
                actual_find = [condition["target"][0] for condition in finish if condition["conditionType"] == "FindItem" and condition.get("onlyFoundInRaid") and len(condition.get("target", [])) == 1]
                actual_handover = [condition["target"][0] for condition in finish if condition["conditionType"] == "HandoverItem"]
                self.assertEqual(actual_find, expected_find, row["id"])
                self.assertEqual(actual_handover, expected_handover, row["id"])

    def test_opening_ground_zero_recoveries_use_accessible_early_items(self):
        opening = self.authored["chains"][0]["quests"]
        expected = {
            "e81e5d79bfdf40efc87cdf99": "5672cb124bdc2d1a0f8b4568",  # AA battery
            "4f3828ef74a66f154f6ac397": "5909e99886f7740c983b9984",  # USB adapter
            "52cbb69039f63f3cfbad321a": "590a386e86f77429692b27ab",  # damaged HDD
        }
        for row in opening:
            if row["id"] not in expected:
                continue
            recovery = next(objective for objective in row["objectives"] if objective["kind"] == "retrieveQuestItem")
            self.assertEqual(recovery["itemTpl"], expected[row["id"]])

    def test_opening_campaign_moves_with_the_early_vanilla_map_cadence(self):
        expected = {
            "e81e5d79bfdf40efc87cdf99": ("Эпицентр", ["Sandbox", "Sandbox_high"]),
            "4876c8bf7bb9677e3970b01c": ("Эпицентр", ["Sandbox", "Sandbox_high"]),
            "59230813b9f9e11ceed08033": ("Таможня", ["bigmap"]),
            "bb49cbdbae242ffef21f95b7": ("Лес", ["Woods"]),
            "ac7bd06524b05c40da6f56ef": ("Завод", ["factory4_night"]),
            "05c97b5823b0c42b7c25ccf9": ("Развязка", ["Interchange"]),
            "6d9fda8875aed2082b4da528": ("Таможня", ["bigmap"]),
        }
        authored = {q["id"]: q for q in self.authored["chains"][0]["quests"]}
        runtime = {q["_id"]: q for q in self.story}
        for quest_id, (map_name, runtime_locations) in expected.items():
            row = authored[quest_id]
            self.assertEqual(row.get("mapOverride", "Эпицентр"), map_name)
            nested_locations = {
                target
                for finish in runtime[quest_id]["conditions"]["AvailableForFinish"]
                for condition in finish.get("counter", {}).get("conditions", [])
                if condition.get("conditionType") == "Location"
                for target in condition["target"]
            }
            self.assertTrue(nested_locations <= set(runtime_locations), quest_id)
        self.assertEqual(sum(q.get("mapOverride", "Эпицентр") == "Эпицентр" for q in authored.values()), 5)

    def test_factory_control_marker_site_is_explicit_and_mapped_to_its_beacon_zone(self):
        quest_id = "ac7bd06524b05c40da6f56ef"
        quest = self.by_id[quest_id]
        finish = quest["conditions"]["AvailableForFinish"]
        visit_conditions = [
            condition
            for row in finish
            if row["conditionType"] == "CounterCreator"
            for condition in row["counter"]["conditions"]
            if condition["conditionType"] == "VisitPlace"
        ]
        beacon = next(row for row in finish if row["conditionType"] == "PlaceBeacon")
        self.assertEqual([row["target"] for row in visit_conditions], ["ter_017_area_1"])
        self.assertEqual(beacon["zoneId"], "ter_017_area_1")
        ru = self.ru
        description = ru[quest_id + " description"]
        self.assertIn("завод (ночь)", description.lower())
        self.assertIn("кабинет секретаря", description.lower())
        self.assertIn("пролом", description.lower())
        self.assertIn("всю комнату", description.lower())
        self.assertIn("dynamic maps", description.lower())
        self.assertIn("комнату за проломом", ru[finish[0]["id"]].lower())
        self.assertIn("кабинете секретаря", ru[beacon["id"]].lower())
        self.assertIn("MS2000", ru[beacon["id"]])

    def test_promised_field_actions_are_materialized(self):
        expected = {
            "837bdd0ab80a2a1382caeedd": {"CounterCreator", "FindItem", "HandoverItem"},
            "e74018c43dc3f47551445192": {"PlaceBeacon", "CounterCreator", "FindItem", "HandoverItem"},
            "aef99c7f97ca615cf101a654": {"PlaceBeacon", "CounterCreator", "FindItem", "HandoverItem"},
        }
        for quest_id, required in expected.items():
            finish = self.by_id[quest_id]["conditions"]["AvailableForFinish"]
            self.assertTrue(required <= {row["conditionType"] for row in finish}, quest_id)
        disrupted = self.by_id["837bdd0ab80a2a1382caeedd"]["conditions"]["AvailableForFinish"]
        nested_types = {
            condition["conditionType"]
            for row in disrupted if row["conditionType"] == "CounterCreator"
            for condition in row["counter"]["conditions"]
        }
        self.assertTrue({"Kills", "ExitStatus"} <= nested_types)

    def test_russian_story_locale_is_real_utf8_cyrillic(self):
        rendered = json.dumps(self.ru, ensure_ascii=False)
        self.assertNotIn("\ufffd", rendered)
        self.assertGreater(sum("\u0400" <= char <= "\u04ff" for char in rendered), 80000)

    def test_objective_labels_remain_readable_in_the_single_line_client_row(self):
        for quest in self.story:
            for row in quest["conditions"]["AvailableForFinish"]:
                label = self.ru.get(row["id"], "")
                self.assertLessEqual(len(label), 100, row["id"])

    def test_revised_early_route_sites_replace_exact_vanilla_overlaps(self):
        expected = {
            "e81e5d79bfdf40efc87cdf99": {"Sandbox_5_Office_exploration"},
            "e472a13c1478f7b9258522d3": {
                "Sandbox_1_MedicalArea_exploration", "Sandbox_5_DeadGroup_exploration",
                "Sandbox_5_Laborant_exploration", "nt2024_5_throtil_epicentr",
            },
            "4f3828ef74a66f154f6ac397": {"Sandbox_2_Kord_exploration"},
            "52cbb69039f63f3cfbad321a": {
                "Sandbox_5_Office_exploration", "Sandbox_5_Laborant_exploration",
            },
            "88c98ecb21d126970231b220": {"vremyan_case", "bomj_place", "exit777"},
            "5fd9397fbc40a37c1abf3790": {"gazel"},
            "3547b2765c993b58b04e6074": {"place_SADOVOD_03", "vremyan_case"},
            "70a745d426d2ba09157eb734": {"fuel1", "TerragroupBOX_4"},
            "743532fcd12b4a88ff7cd83f": {"dead_posylni"},
            "e9de043a8379c87027f762b3": {"vaz_feld"},
            "837bdd0ab80a2a1382caeedd": {"room206_water"},
            "94dfbf0ec29ab7391528f217": {"room114"},
            "bcf61611e9723c765e07a740": {"fuel3"},
            "c28a9b8837dcf639ba524cc3": {"huntsman_001", "Lost_caravan"},
            "1ca43f65c714ea7f9eec6e2a": {"Bunker_enter"},
            "b61a1cd8d6a7a82e81033dbc": {"meh_45_radio_area_mark_1"},
            "dcae6b2142ca93b7dd77df3e": {"Depo_Zone_1"},
            "37475cbab1174e5619c16939": {"Lost_caravan"},
            "9bf1b7ad200b0f5f59e9cbbd": {"Bunker_enter", "meh_45_radio_area_mark_2"},
            "f0874dd26a7b1f76d86aad88": {"huntsman_001", "meh_45_radio_area_mark_3"},
            "60df546a03bdc74ca45fb8ca": {"pr_scout_col"},
        }
        for quest_id, expected_zones in expected.items():
            finish = self.by_id[quest_id]["conditions"]["AvailableForFinish"]
            actual = set()
            for row in finish:
                if row["conditionType"] == "PlaceBeacon":
                    actual.add(row["zoneId"])
                elif row["conditionType"] == "CounterCreator":
                    actual.update(c["target"] for c in row["counter"]["conditions"] if c["conditionType"] == "VisitPlace")
            self.assertEqual(actual, expected_zones, quest_id)

    def test_early_map_objective_text_names_the_target_in_russian(self):
        targeted_ids = {
            "e81e5d79bfdf40efc87cdf99", "e472a13c1478f7b9258522d3",
            "4f3828ef74a66f154f6ac397", "52cbb69039f63f3cfbad321a",
            "88c98ecb21d126970231b220", "5fd9397fbc40a37c1abf3790",
            "3547b2765c993b58b04e6074", "70a745d426d2ba09157eb734",
            "743532fcd12b4a88ff7cd83f", "e9de043a8379c87027f762b3",
            "837bdd0ab80a2a1382caeedd", "94dfbf0ec29ab7391528f217",
            "bcf61611e9723c765e07a740", "c28a9b8837dcf639ba524cc3",
            "1ca43f65c714ea7f9eec6e2a", "b61a1cd8d6a7a82e81033dbc",
            "dcae6b2142ca93b7dd77df3e", "37475cbab1174e5619c16939",
            "9bf1b7ad200b0f5f59e9cbbd", "f0874dd26a7b1f76d86aad88",
            "60df546a03bdc74ca45fb8ca",
        }
        for quest_id in targeted_ids:
            for condition in self.by_id[quest_id]["conditions"]["AvailableForFinish"]:
                has_location = condition["conditionType"] == "PlaceBeacon" or (
                    condition["conditionType"] == "CounterCreator"
                    and any(c["conditionType"] == "VisitPlace" for c in condition["counter"]["conditions"])
                )
                if has_location:
                    label = self.ru[condition["id"]]
                    self.assertNotIn("оперативную точку", label, f"{quest_id}: {label}")
                    self.assertNotIn("назначенную точку", label, f"{quest_id}: {label}")


def test_revised_early_route_sites_replace_exact_vanilla_overlaps(self):
        expected = {
            "e81e5d79bfdf40efc87cdf99": {"Sandbox_5_Office_exploration"},
            "e472a13c1478f7b9258522d3": {
                "Sandbox_1_MedicalArea_exploration", "Sandbox_5_DeadGroup_exploration",
                "Sandbox_5_Laborant_exploration", "nt2024_5_throtil_epicentr",
            },
            "4f3828ef74a66f154f6ac397": {"Sandbox_2_Kord_exploration"},
            "52cbb69039f63f3cfbad321a": {
                "Sandbox_5_Office_exploration", "Sandbox_5_Laborant_exploration",
            },
            "88c98ecb21d126970231b220": {"vremyan_case", "bomj_place", "exit777"},
            "5fd9397fbc40a37c1abf3790": {"gazel"},
            "3547b2765c993b58b04e6074": {"place_SADOVOD_03", "vremyan_case"},
            "70a745d426d2ba09157eb734": {"fuel1", "TerragroupBOX_4"},
            "743532fcd12b4a88ff7cd83f": {"dead_posylni"},
            "e9de043a8379c87027f762b3": {"vaz_feld"},
            "837bdd0ab80a2a1382caeedd": {"room206_water"},
            "94dfbf0ec29ab7391528f217": {"room114"},
            "bcf61611e9723c765e07a740": {"fuel3"},
            "c28a9b8837dcf639ba524cc3": {"huntsman_001", "Lost_caravan"},
            "1ca43f65c714ea7f9eec6e2a": {"Bunker_enter"},
            "b61a1cd8d6a7a82e81033dbc": {"meh_45_radio_area_mark_1"},
            "dcae6b2142ca93b7dd77df3e": {"Depo_Zone_1"},
            "37475cbab1174e5619c16939": {"Lost_caravan"},
            "9bf1b7ad200b0f5f59e9cbbd": {"Bunker_enter", "meh_45_radio_area_mark_2"},
            "f0874dd26a7b1f76d86aad88": {"huntsman_001", "meh_45_radio_area_mark_3"},
            "60df546a03bdc74ca45fb8ca": {"pr_scout_col"},
        }
        for quest_id, expected_zones in expected.items():
            finish = self.by_id[quest_id]["conditions"]["AvailableForFinish"]
            actual = set()
            for row in finish:
                if row["conditionType"] == "PlaceBeacon":
                    actual.add(row["zoneId"])
                elif row["conditionType"] == "CounterCreator":
                    actual.update(c["target"] for c in row["counter"]["conditions"] if c["conditionType"] == "VisitPlace")
            self.assertEqual(actual, expected_zones, quest_id)

def test_early_map_objective_text_names_the_target_in_russian(self):
        targeted_ids = {
            "e81e5d79bfdf40efc87cdf99", "e472a13c1478f7b9258522d3",
            "4f3828ef74a66f154f6ac397", "52cbb69039f63f3cfbad321a",
            "88c98ecb21d126970231b220", "5fd9397fbc40a37c1abf3790",
            "3547b2765c993b58b04e6074", "70a745d426d2ba09157eb734",
            "743532fcd12b4a88ff7cd83f", "e9de043a8379c87027f762b3",
            "837bdd0ab80a2a1382caeedd", "94dfbf0ec29ab7391528f217",
            "bcf61611e9723c765e07a740", "c28a9b8837dcf639ba524cc3",
            "1ca43f65c714ea7f9eec6e2a", "b61a1cd8d6a7a82e81033dbc",
            "dcae6b2142ca93b7dd77df3e", "37475cbab1174e5619c16939",
            "9bf1b7ad200b0f5f59e9cbbd", "f0874dd26a7b1f76d86aad88",
            "60df546a03bdc74ca45fb8ca",
        }
        for quest_id in targeted_ids:
            for condition in self.by_id[quest_id]["conditions"]["AvailableForFinish"]:
                has_location = condition["conditionType"] == "PlaceBeacon" or (
                    condition["conditionType"] == "CounterCreator"
                    and any(c["conditionType"] == "VisitPlace" for c in condition["counter"]["conditions"])
                )
                if has_location:
                    label = self.ru[condition["id"]]
                    self.assertNotIn("оперативную точку", label, f"{quest_id}: {label}")
                    self.assertNotIn("назначенную точку", label, f"{quest_id}: {label}")

if __name__ == "__main__":
    unittest.main()
