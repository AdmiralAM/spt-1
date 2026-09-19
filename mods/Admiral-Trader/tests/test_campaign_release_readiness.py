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

    def test_all_172_quests_have_xp_rep_cash_and_a_useful_reward_layer(self):
        self.assertEqual(172, len(self.quests))
        overlay_ids = set()
        for filename in (
            "natalya-signature-replacements.json", "early-weapon-reward-trades.json",
            "field-support-reward-trades.json", "tactical-reward-trades.json",
            "belt-container-reward-trades.json",
        ):
            overlay_ids.update(load(f"db/rewards/{filename}"))
        overlay_ids.update(row["questId"] for row in load("manifests/weapon-rotation-rewards.json")["rewards"])
        overlay_ids.update(row["questId"] for row in load("manifests/campaign-polish-rewards.json")["rewards"])
        for quest_id, quest in self.quests.items():
            rewards = quest["rewards"]["Success"]
            self.assertTrue(any(row["type"] == "Experience" and row["value"] > 0 for row in rewards), quest_id)
            self.assertTrue(any(row["type"] == "TraderStanding" and row["value"] > 0 for row in rewards), quest_id)
            cash = next(row for row in rewards if row.get("items", [{}])[0].get("_tpl") == ROUBLES)
            self.assertGreaterEqual(cash["value"], 10000, quest_id)
            direct_item = any(row.get("type") == "Item" and row.get("items", [{}])[0].get("_tpl") != ROUBLES for row in rewards)
            unlocked_offer = any(row.get("type") == "AssortmentUnlock" for row in rewards)
            self.assertTrue(direct_item or unlocked_offer or quest_id in overlay_ids, quest_id)

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


if __name__ == "__main__":
    unittest.main()
