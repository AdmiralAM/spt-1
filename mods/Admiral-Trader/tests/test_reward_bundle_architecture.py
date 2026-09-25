import json
import pathlib
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[1]


class RewardBundleArchitectureTests(unittest.TestCase):
    def test_policy_contains_authored_native_bundles_without_publishing_them(self):
        policy = json.loads((ROOT / "manifests" / "reward-bundle-policy.json").read_text(encoding="utf-8"))
        self.assertEqual(policy["schemaVersion"], 1)
        self.assertTrue(policy["enabled"])
        self.assertGreaterEqual(len(policy["catalog"]), 24)
        self.assertGreaterEqual(len(policy["bundles"]), 6)
        self.assertEqual({bundle["tier"] for bundle in policy["bundles"]}, {"common", "rare", "epic"})
        self.assertTrue(all(item["source"] == "spt" for item in policy["catalog"]))
        self.assertTrue(all(item["quantity"] > 0 for item in policy["catalog"]))
        self.assertTrue(all({slot["role"] for slot in bundle["slots"]} >= {"weapon", "ammunition"}
                            for bundle in policy["bundles"] if "weapon" in {slot["role"] for slot in bundle["slots"]}))
        self.assertTrue(all(next(slot for slot in bundle["slots"] if slot["role"] == "weapon").get("compatibilityFamily")
                            for bundle in policy["bundles"] if "weapon" in {slot["role"] for slot in bundle["slots"]}))

    def test_runtime_has_deterministic_and_fail_explicit_contracts(self):
        source = (ROOT / "server" / "RewardBundleEngine.cs").read_text(encoding="utf-8")
        self.assertIn("SHA256.HashData", source)
        self.assertIn('RewardBundleException("catalog"', source)
        self.assertIn('RewardBundleException("generation"', source)
        self.assertIn('RewardBundleException("validation"', source)
        self.assertIn("availableTemplates.Contains", source)
        self.assertIn("compatibilityFamily", source)
        self.assertIn("slot.CompatibilityFamily", source)
        self.assertNotIn("new Random", source)

    def test_publication_failure_rolls_back_before_error_is_reported(self):
        source = (ROOT / "server" / "RewardBundlePublication.cs").read_text(encoding="utf-8")
        self.assertLess(source.index("rollback();"), source.rindex('RewardBundleException("publication"'))
        self.assertIn("rollback also failed", source)

    def test_preflight_does_not_mutate_quests_assort_or_profiles(self):
        source = (ROOT / "server" / "RewardBundlePreflight.cs").read_text(encoding="utf-8")
        self.assertIn("RewardBundleEngine.ValidatePolicy", source)
        self.assertNotIn("templateTable.Quests", source)
        self.assertNotIn("TradersTable", source)
        self.assertNotIn("Profile", source)

    def test_policy_is_packaged(self):
        project = (ROOT / "server" / "AdmiralTrader.Server.csproj").read_text(encoding="utf-8")
        self.assertIn("reward-bundle-policy.json", project)
        self.assertIn("wtt-preset-catalog.json", project)
        self.assertIn("wtt-reward-trades.json", project)

    def test_wtt_preset_catalog_contains_complete_optional_trees(self):
        rows = json.loads((ROOT / "db" / "optional" / "wtt-preset-catalog.json").read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(rows), 60)
        self.assertEqual({row["source"] for row in rows}, {"wtt-armory", "wtt-content-backport"})
        for row in rows:
            self.assertEqual(row["items"][0]["_tpl"], row["rootTemplate"])
            self.assertGreater(row["valueRub"], 0)
            self.assertTrue(row["nameRu"])
            ids = {item["_id"] for item in row["items"]}
            self.assertEqual(len(ids), len(row["items"]))
            self.assertTrue(all(item.get("parentId") in ids for item in row["items"][1:]))

    def test_wtt_rewards_are_curated_complete_arsenal_presets(self):
        trades = json.loads((ROOT / "db" / "optional" / "wtt-reward-trades.json").read_text(encoding="utf-8"))
        self.assertEqual(len(trades), 24)
        all_item_ids = []
        for quest_id, trade in trades.items():
            quest = json.loads(next((ROOT / "db" / "quests").glob(f"*-{quest_id}.json")).read_text(encoding="utf-8"))
            self.assertIn("Арсенал", quest["QuestName"])
            self.assertGreaterEqual(trade["cashReductionRub"], 0)
            self.assertEqual(trade["minimumCashRub"], 10000)
            cash = next(reward["value"] for reward in quest["rewards"]["Success"]
                        if reward.get("items") and reward["items"][0].get("_tpl") == "5449016a4bdc2d6f028b456f")
            self.assertGreaterEqual(cash - trade["cashReductionRub"], trade["minimumCashRub"])
            items = trade["reward"]["items"]
            self.assertGreater(len(items), 1)
            ids = {item["_id"] for item in items}
            self.assertEqual(len(ids), len(items))
            self.assertTrue(all(item.get("parentId") in ids for item in items[1:]))
            all_item_ids.extend(ids)
        self.assertEqual(len(all_item_ids), len(set(all_item_ids)))


if __name__ == "__main__":
    unittest.main()
