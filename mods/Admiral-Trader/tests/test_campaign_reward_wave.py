import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUB = "5449016a4bdc2d6f028b456f"


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class CampaignRewardWaveTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = load("manifests/campaign-reward-wave.json")
        cls.signature = load("db/rewards/natalya-signature-replacements.json")
        cls.early = load("db/rewards/early-weapon-reward-trades.json")
        cls.belt = load("db/rewards/belt-container-reward-trades.json")
        cls.quests = {}
        for path in (ROOT / "db/quests").glob("*.json"):
            quest = json.loads(path.read_text(encoding="utf-8"))
            cls.quests[quest["_id"]] = quest

    def test_reward_wave_is_bounded_and_optional_belt_is_not_required(self):
        self.assertEqual(self.manifest["status"], "runtime-materialized")
        self.assertEqual(self.manifest["coreSignaturePresetRewards"], 10)
        self.assertEqual(self.manifest["earlyCompleteWeaponRewardTrades"], 4)
        self.assertEqual(self.manifest["optionalBeltEquipmentRewardTrades"], 32)
        self.assertEqual(self.manifest["directBeltProductRewardTrades"], 16)
        self.assertEqual(self.manifest["additionalBeltPackCandidateRewardTrades"], 16)
        self.assertFalse(self.manifest["requiredDependencies"])
        self.assertEqual(len(self.signature), 10)
        self.assertEqual(len(self.early), 4)
        self.assertEqual(len(self.belt), 32)

        early_levels = []
        for quest_id in self.belt:
            level = next(row["value"] for row in self.quests[quest_id]["conditions"]["AvailableForStart"] if row["conditionType"] == "Level")
            early_levels.append(int(level))
        self.assertGreaterEqual(sum(level <= 20 for level in early_levels), 24)

    def test_signature_rewards_are_complete_unique_item_trees(self):
        item_ids = set()
        for quest_id, reward in self.signature.items():
            self.assertIn(quest_id, self.quests)
            self.assertEqual(reward["value"], 1)
            self.assertEqual(reward["items"][0]["_id"], reward["target"])
            local_ids = {row["_id"] for row in reward["items"]}
            self.assertEqual(len(local_ids), len(reward["items"]))
            self.assertFalse(item_ids & local_ids)
            item_ids |= local_ids
            self.assertGreater(len(reward["items"]), 1)
            self.assertTrue(all(row.get("parentId") is None or row["parentId"] in local_ids for row in reward["items"]))

    def test_belt_rewards_trade_cash_instead_of_stacking_value(self):
        for quest_id, trade in self.belt.items():
            quest = self.quests[quest_id]
            cash = next(row for row in quest["rewards"]["Success"] if row.get("items", [{}])[0].get("_tpl") == RUB)
            self.assertGreater(cash["value"], trade["cashReductionRub"])
            reward = trade["reward"]
            self.assertEqual(reward["value"], 1)
            self.assertEqual(len(reward["items"]), 1)
            self.assertEqual(reward["items"][0]["_id"], reward["target"])

    def test_direct_belt_products_are_front_loaded_and_use_stable_ids(self):
        stable_belt_ids = {
            "68ac00000000000000000001",
            "68ac00000000000000000006",
            "68ac0000000000000000000c",
            "68ac0000000000000000000f",
            "68ac00000000000000000013",
        }
        direct = [trade for trade in self.belt.values() if trade["reward"]["items"][0]["_tpl"] in stable_belt_ids]
        self.assertEqual(len(direct), 16)
        self.assertTrue(any(trade["reward"]["items"][0]["_tpl"] == "68ac00000000000000000006" for trade in direct))
        self.assertTrue(any(trade["reward"]["items"][0]["_tpl"] == "68ac0000000000000000000c" for trade in direct))
        self.assertTrue(any(trade["reward"]["items"][0]["_tpl"] == "68ac0000000000000000000f" for trade in direct))

    def test_runtime_skips_pack_trade_without_template_and_preserves_cash(self):
        source = (ROOT / "server/OptionalContentRegistration.cs").read_text(encoding="utf-8")
        self.assertIn("ApplyCashTrades", source)
        self.assertIn("if (reward.Items.Any(item => !templateTable.Items.ContainsKey(item.Template)))", source)
        self.assertIn("continue;", source)
        self.assertIn("cash.Value = reduced", source)
        self.assertIn("success.Add(reward)", source)
        self.assertIn('[JsonPropertyName("cashReductionRub")]', source)
        self.assertIn('[JsonPropertyName("reward")]', source)

    def test_early_weapon_rewards_are_complete_cash_trades(self):
        for quest_id, trade in self.early.items():
            quest = self.quests[quest_id]
            cash = next(row for row in quest["rewards"]["Success"] if row.get("items", [{}])[0].get("_tpl") == RUB)
            self.assertGreater(cash["value"], trade["cashReductionRub"])
            self.assertGreater(len(trade["reward"]["items"]), 1)


if __name__ == "__main__":
    unittest.main()
