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


if __name__ == "__main__":
    unittest.main()
