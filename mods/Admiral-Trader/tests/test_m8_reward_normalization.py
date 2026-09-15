import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ROUBLES = "5449016a4bdc2d6f028b456f"


def load(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8-sig"))


class M8RewardNormalizationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = {
            quest["_id"]: quest
            for path in (ROOT / "db/quests").glob("*.json")
            for quest in [json.loads(path.read_text(encoding="utf-8-sig"))]
        }
        cls.policy = load("manifests/reward-policy.json")["runtimeNormalization"]
        cls.runtime = load("manifests/story-campaign-runtime.json")
        cls.assort = load("db/assort.json")

    @staticmethod
    def physical_templates(quest):
        return {
            item["_tpl"]
            for reward in quest["rewards"]["Success"] if reward["type"] == "Item"
            for item in reward.get("items", []) if item["_tpl"] != ROUBLES
        }

    def test_known_high_value_items_are_unlock_only_not_free_rewards(self):
        forbidden = set(self.policy["removedFreeHighValueTemplates"])
        for quest in self.quests.values():
            self.assertTrue(self.physical_templates(quest).isdisjoint(forbidden), quest["_id"])

    def test_story_finals_give_one_sample_and_one_finite_unlock(self):
        roots = {row["_id"]: row for row in self.assort["items"] if row.get("parentId") == "hideout"}
        for row in self.runtime["assortmentUnlocks"]:
            quest = self.quests[row["questId"]]
            self.assertEqual(len(self.physical_templates(quest)), self.policy["storyFinaleFreeItemCount"])
            unlocks = [reward for reward in quest["rewards"]["Success"] if reward["type"] == "AssortmentUnlock"]
            self.assertEqual([reward["target"] for reward in unlocks], [row["offerId"]])
            offer = roots[row["offerId"]]
            self.assertFalse(offer["upd"]["UnlimitedCount"])
            self.assertGreater(offer["upd"]["BuyRestrictionMax"], 0)

    def test_container_unlocks_use_bounded_stock_and_non_exploit_prices(self):
        expected = {
            "3e6f816c41a9c73328116881": ("619cbf7d23893217ec30b689", 420000),
            "b5b55194b4a76ce0ce71a409": ("5d03794386f77420415576f5", 330000),
            "6b0d153489b984ae9b940adb": ("5d1b5e94d7ad1a2b865a96b0", 600000),
        }
        roots = {row["_id"]: row for row in self.assort["items"] if row.get("parentId") == "hideout"}
        for offer_id, (tpl, price) in expected.items():
            self.assertEqual(roots[offer_id]["_tpl"], tpl)
            self.assertEqual(roots[offer_id]["upd"]["StackObjectsCount"], 1)
            self.assertEqual(roots[offer_id]["upd"]["BuyRestrictionMax"], 1)
            self.assertEqual(self.assort["barter_scheme"][offer_id], [[{"count": price, "_tpl": ROUBLES}]])

    def test_final_classified_reward_is_a_usable_labs_card(self):
        finale = self.quests[self.policy["exceptionalFinale"]["questId"]]
        self.assertEqual(self.physical_templates(finale), {"5c94bbff86f7747ee735c08f"})
        final_unlock = self.runtime["assortmentUnlocks"][-1]
        self.assertEqual(final_unlock["tpl"], "5c94bbff86f7747ee735c08f")

    def test_optional_icebreaker_finale_exception_is_explicit_and_non_repeatable(self):
        exception = self.policy["optionalExceptionalFinale"]
        path = next((ROOT / "db/optional/icebreaker/quests").glob(f"*-{exception['questId']}.json"))
        quest = json.loads(path.read_text(encoding="utf-8-sig"))
        self.assertFalse(quest.get("restartable", False))
        self.assertEqual(self.physical_templates(quest), {"5d1b376e86f774252519444e"})
        self.assertFalse(any(reward["type"] == "AssortmentUnlock" for reward in quest["rewards"]["Success"]))

    def test_every_high_raw_reward_is_explicitly_reviewed(self):
        reviewed = self.policy["reviewedHighRewardQuests"]
        self.assertEqual(len(reviewed), 5)
        self.assertTrue(all(row["decision"] == "retain" and row["reason"] for row in reviewed))
        reviewed_ids = {row["questId"] for row in reviewed}
        observed_ids = set()
        for quest_id, quest in self.quests.items():
            rewards = quest["rewards"]["Success"]
            xp = next((float(row["value"]) for row in rewards if row["type"] == "Experience"), 0)
            rub = next((float(row["value"]) for row in rewards if row.get("items") and row["items"][0]["_tpl"] == ROUBLES), 0)
            standing = next((float(row["value"]) for row in rewards if row["type"] == "TraderStanding"), 0)
            if xp > 25000 or rub > 110000 or standing > 0.03:
                observed_ids.add(quest_id)
        self.assertEqual(observed_ids, reviewed_ids)


if __name__ == "__main__":
    unittest.main()
