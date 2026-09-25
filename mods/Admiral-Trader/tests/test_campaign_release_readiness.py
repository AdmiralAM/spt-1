import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ROUBLES = "5449016a4bdc2d6f028b456f"


def load(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8"))


class CampaignReleaseReadinessTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = {}
        for path in (ROOT / "db/quests").glob("*.json"):
            quest = json.loads(path.read_text(encoding="utf-8"))
            cls.quests[quest["_id"]] = quest

    def test_all_172_quests_have_xp_rep_cash_and_a_native_item_reward(self):
        self.assertEqual(172, len(self.quests))
        for quest_id, quest in self.quests.items():
            rewards = quest["rewards"]["Success"]
            self.assertTrue(any(row["type"] == "Experience" and row["value"] > 0 for row in rewards), quest_id)
            self.assertTrue(any(row["type"] == "TraderStanding" and row["value"] > 0 for row in rewards), quest_id)
            cash = next(row for row in rewards if row.get("items", [{}])[0].get("_tpl") == ROUBLES)
            self.assertGreaterEqual(cash["value"], 10000, quest_id)
            direct_item = any(row.get("type") == "Item" and row.get("items", [{}])[0].get("_tpl") != ROUBLES for row in rewards)
            self.assertTrue(direct_item, quest_id)

    def test_loyalty_uses_level_reputation_and_vanilla_style_sales_gates(self):
        levels = load("db/base.json")["loyaltyLevels"]
        self.assertEqual([row["minLevel"] for row in levels], [1, 15, 25, 35])
        self.assertEqual([row["minStanding"] for row in levels], [0, 0.1, 0.3, 0.55])
        self.assertEqual([row["minSalesSum"] for row in levels], [0, 500000, 1200000, 2200000])
        available_rep = []
        for quest in self.quests.values():
            minimum_level = int(next(row["value"] for row in quest["conditions"]["AvailableForStart"] if row["conditionType"] == "Level"))
            standing = float(next(row["value"] for row in quest["rewards"]["Success"] if row["type"] == "TraderStanding"))
            available_rep.append((minimum_level, standing))
        for level_cap, required in ((14, 0.1), (24, 0.3), (34, 0.55)):
            self.assertGreaterEqual(sum(value for level, value in available_rep if level <= level_cap), required)

    def test_story_maps_open_in_parallel_instead_of_waiting_for_prior_finales(self):
        story = load("manifests/story-campaign-runtime.json")["quests"]
        by_position = {(row["chain"], row["order"]): row["id"] for row in story}
        expected_openers = {
            2: by_position[(1, 3)],
            3: by_position[(1, 5)],
            4: by_position[(2, 3)],
            5: by_position[(3, 3)],
            9: by_position[(2, 5)],
            6: by_position[(3, 5)],
            8: by_position[(4, 3)],
            7: by_position[(6, 3)],
            10: by_position[(7, 3)],
        }
        for chain, parent in expected_openers.items():
            opener = self.quests[by_position[(chain, 1)]]
            prerequisites = {row["target"] for row in opener["conditions"]["AvailableForStart"] if row["conditionType"] == "Quest"}
            self.assertIn(parent, prerequisites, f"story chain {chain}")
            previous_finale = by_position[(chain - 1, 10)]
            self.assertNotIn(previous_finale, prerequisites, f"story chain {chain} waits for a full prior finale")

    def test_store_has_four_meaningful_tiers_and_bounded_unlocks(self):
        roots = []
        loyalty = []
        for filename in ("db/assort.json", "db/natalya-signature-assort.json"):
            assort = load(filename)
            root_ids = {row["_id"] for row in assort["items"] if row.get("parentId") == "hideout"}
            roots.extend(root_ids)
            loyalty.extend(int(assort["loyal_level_items"][offer_id]) for offer_id in root_ids)
        self.assertEqual(82, len(roots))
        self.assertEqual(82, len(set(roots)))
        for tier in range(1, 5):
            self.assertGreaterEqual(loyalty.count(tier), 5, f"LL{tier}")
        quest_assort = load("db/questassort.json")
        self.assertEqual(18, len(quest_assort["success"]))
        self.assertEqual({}, quest_assort["started"])
        self.assertEqual({}, quest_assort["fail"])

    def test_embedded_artem_clothing_is_visible_through_admiral(self):
        self.assertTrue(load("db/base.json")["customization_seller"])
        clothing = load("db/CustomClothing/Artem Clothes.json")
        self.assertEqual(64, len(clothing))
        self.assertEqual({"66bf757f27d0b097db0acea5"}, {row["traderId"] for row in clothing})

    def test_every_equipment_objective_has_explicit_runtime_copy(self):
        contract = load("manifests/quest-quality-runtime.json")
        rows = contract["equipmentObjectives"]
        self.assertEqual(8, contract["equipmentObjectiveCount"])
        self.assertEqual(8, len(rows))

        locales = {"en": {}, "ru": {}}
        for lang in locales:
            for filename in (f"{lang}.json", f"arsenal-{lang}.json", f"m3-{lang}.json", f"m8-{lang}.json", f"story-{lang}.json"):
                locales[lang].update(load(f"db/locales/{filename}"))

        authored_keys = {(row["questId"], row["conditionId"]) for row in rows}
        runtime_rows = {}
        for quest_id, quest in self.quests.items():
            for condition in quest["conditions"]["AvailableForFinish"]:
                if condition.get("conditionType") != "CounterCreator":
                    continue
                inner = condition.get("counter", {}).get("conditions", [])
                equipment = next((row for row in inner if row.get("conditionType") == "Equipment"), None)
                if equipment and (quest_id, condition["id"]) in authored_keys:
                    runtime_rows[(quest_id, condition["id"])] = equipment["equipmentInclusive"]

        self.assertEqual(8, len(runtime_rows))
        for row in rows:
            key = (row["questId"], row["conditionId"])
            self.assertEqual(runtime_rows[key], row["equipmentInclusive"], key)
            self.assertEqual(row["en"], locales["en"][row["conditionId"]], key)
            self.assertEqual(row["ru"], locales["ru"][row["conditionId"]], key)
            self.assertNotIn("назначенн", row["ru"].lower(), key)

        self.assertNotIn("208db81b5ce195bf0c176852", {row["questId"] for row in rows})


def test_story_rewards_include_authored_items_without_breaching_cash_floor(self):
        authored = load("manifests/campaign-authored-100-review.json")
        policy = load("manifests/reward-bundle-policy.json")
        catalog = {row["templateId"]: row for row in policy["catalog"]}
        layered_ids = set()
        for path in (ROOT / "db/rewards").glob("*.json"):
            if path.name == "natalya-signature-replacements.json":
                continue
            layered_ids.update(json.loads(path.read_text(encoding="utf-8")))
        story_ids = set()
        for chain in authored["chains"]:
            for row in chain["quests"]:
                story_ids.add(row["id"])
                field_item = row["rewards"].get("fieldItem")
                if row["order"] in (3, 6, 10):
                    continue  # These stages already award their authored thematic item.
                quest = self.quests[row["id"]]
                items = [item for reward in quest["rewards"]["Success"] if reward.get("type") == "Item" for item in reward.get("items", [])]
                if row["id"] in layered_ids:
                    self.assertIsNone(field_item, row["id"])  # Avoid stacking independent campaign rewards.
                    self.assertTrue(any(item.get("_tpl") != ROUBLES for item in items), row["id"])
                else:
                    self.assertIsNotNone(field_item, row["id"])
                    self.assertIn(field_item["templateId"], catalog, row["id"])
                    self.assertEqual("spt", catalog[field_item["templateId"]]["source"], row["id"])
                    self.assertGreater(field_item["quantity"], 0, row["id"])
                    self.assertGreater(field_item["valueRub"], 0, row["id"])
                    self.assertTrue(any(item.get("_tpl") == field_item["templateId"] for item in items), row["id"])
                cash = next(reward["value"] for reward in quest["rewards"]["Success"] if reward.get("items", [{}])[0].get("_tpl") == ROUBLES)
                self.assertGreaterEqual(cash, 10000, row["id"])
        self.assertEqual(100, len(story_ids))

if __name__ == "__main__":
    unittest.main()
