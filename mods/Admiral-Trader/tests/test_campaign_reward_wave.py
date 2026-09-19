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
        cls.field_support = load("db/rewards/field-support-reward-trades.json")
        cls.tactical = load("db/rewards/tactical-reward-trades.json")
        cls.belt = load("db/rewards/belt-container-reward-trades.json")
        cls.quests = {}
        for path in (ROOT / "db/quests").glob("*.json"):
            quest = json.loads(path.read_text(encoding="utf-8"))
            cls.quests[quest["_id"]] = quest

    def test_reward_wave_is_bounded_and_optional_belt_is_not_required(self):
        self.assertEqual(self.manifest["status"], "runtime-materialized")
        self.assertEqual(self.manifest["coreSignaturePresetRewards"], 10)
        self.assertEqual(self.manifest["earlyCompleteWeaponRewardTrades"], 4)
        self.assertEqual(self.manifest["nativeFieldSupportRewardTrades"], 20)
        self.assertEqual(self.manifest["nativeTacticalRewardTrades"], 11)
        self.assertEqual(self.manifest["optionalBeltEquipmentRewardTrades"], 32)
        self.assertEqual(self.manifest["directBeltProductRewardTrades"], 16)
        self.assertEqual(self.manifest["additionalBeltPackCandidateRewardTrades"], 16)
        self.assertFalse(self.manifest["requiredDependencies"])
        self.assertEqual(len(self.signature), 10)
        self.assertEqual(len(self.early), 4)
        self.assertEqual(len(self.field_support), 20)
        self.assertEqual(len(self.tactical), 11)
        self.assertEqual(len(self.belt), 32)
        self.assertEqual(
            self.manifest["fieldSupportTrades"],
            [
                {
                    "questId": quest_id,
                    "tpl": trade["reward"]["items"][0]["_tpl"],
                    "name": trade["name"],
                    "cashReductionRub": trade["cashReductionRub"],
                }
                for quest_id, trade in self.field_support.items()
            ],
        )

        early_levels = []
        for quest_id in self.belt:
            level = next(row["value"] for row in self.quests[quest_id]["conditions"]["AvailableForStart"] if row["conditionType"] == "Level")
            early_levels.append(int(level))
        self.assertGreaterEqual(sum(level <= 10 for level in early_levels), 9)
        self.assertGreaterEqual(sum(level <= 20 for level in early_levels), 24)

        weapon_levels = []
        for quest_id in self.early:
            level = next(row["value"] for row in self.quests[quest_id]["conditions"]["AvailableForStart"] if row["conditionType"] == "Level")
            weapon_levels.append(int(level))
        self.assertEqual(sorted(weapon_levels), [1, 3, 5, 7])

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
            self.assertGreaterEqual(cash["value"] - trade["cashReductionRub"], 10000)
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

    def test_field_support_rewards_are_native_bounded_cash_trades(self):
        expected_templates = {
            "544fb45d4bdc2dee738b4568", "545cdae64bdc2d39198b4568",
            "5648a69d4bdc2ded0b8b457b", "5648a7494bdc2d9d488b4583",
            "590c657e86f77412b013051d", "590c678286f77426c9660122",
            "5aa2ba71e5b5b000137b758f", "5b432b965acfc47a8774094e",
            "5c0e530286f7747fa1419862", "5c0e531d86f7747fa23f4d42",
            "5c0e533786f7747fa23f4d47", "5c0e534186f7747fa1419867",
            "5d02778e86f774203e7dedbe",
            "5d02797c86f774203f38e30a", "5e4bfc1586f774264f7582d3",
            "5e9dcf5986f7746c417435b3",
            "60098ad7c2240c0fe85c570a",
        }
        actual_templates = set()
        for quest_id, trade in self.field_support.items():
            quest = self.quests[quest_id]
            cash = next(row for row in quest["rewards"]["Success"] if row.get("items", [{}])[0].get("_tpl") == RUB)
            self.assertGreater(cash["value"], trade["cashReductionRub"])
            self.assertEqual(trade["reward"]["value"], 1)
            self.assertEqual(len(trade["reward"]["items"]), 1)
            self.assertEqual(trade["reward"]["items"][0]["_id"], trade["reward"]["target"])
            actual_templates.add(trade["reward"]["items"][0]["_tpl"])
        self.assertEqual(actual_templates, expected_templates)

    def test_tactical_rewards_replace_cash_with_useful_weapons_and_components(self):
        expected_templates = {
            "61657230d92c473c770213d7", "56e0598dd2720bb5668b45a6",
            "570fd6c2d2720bc6458b457f", "5c82342f2e221644f31c060e",
            "5c0505e00db834001b735073", "5c6165902e22160010261b28",
            "5c7d55de2e221644f31bff68", "57adff4f24597737f373b6e6",
            "669fa409933e898cce0c2166", "5b1fa9b25acfc40018633c01",
            "5c07dd120db834001c39092d",
        }
        self.assertEqual({row["reward"]["items"][0]["_tpl"] for row in self.tactical.values()}, expected_templates)
        for quest_id, trade in self.tactical.items():
            quest = self.quests[quest_id]
            cash = next(row for row in quest["rewards"]["Success"] if row.get("items", [{}])[0].get("_tpl") == RUB)
            self.assertGreater(cash["value"], trade["cashReductionRub"])
            self.assertGreaterEqual(cash["value"] - trade["cashReductionRub"], 10000)

    def test_reward_layers_do_not_compete_for_the_same_quest(self):
        layers = {
            "signature": set(self.signature),
            "early": set(self.early),
            "field-support": set(self.field_support),
            "tactical": set(self.tactical),
            "belt": set(self.belt),
        }
        for index, left in enumerate(layers):
            for right in list(layers)[index + 1:]:
                self.assertFalse(layers[left] & layers[right], f"{left} and {right} overlap")


if __name__ == "__main__":
    unittest.main()
