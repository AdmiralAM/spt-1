import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class WeaponRotationExpansionPlanTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = json.loads(
            (ROOT / "manifests" / "weapon-rotation-expansion-plan.json").read_text(encoding="utf-8")
        )

    def test_two_twenty_assignment_lanes_are_authored(self):
        lanes = self.plan["lanes"]
        self.assertEqual(2, len(lanes))
        self.assertEqual([20, 20], sorted(len(entries) for entries in lanes.values()))
        for entries in lanes.values():
            self.assertEqual(list(range(1, 21)), [entry[0] for entry in entries])

    def test_every_assignment_uses_a_nonempty_exact_native_pool(self):
        pools = self.plan["pools"]
        referenced = []
        for entries in self.plan["lanes"].values():
            for _, pool_name, _, locations, _ in entries:
                self.assertIn(pool_name, pools)
                self.assertGreaterEqual(len(pools[pool_name]), 2)
                self.assertEqual(len(pools[pool_name]), len(set(pools[pool_name])))
                self.assertTrue(all(len(tpl) == 24 for tpl in pools[pool_name]))
                self.assertGreaterEqual(len(locations), 2)
                self.assertLessEqual(len(locations), 4)
                referenced.extend(pools[pool_name])
        self.assertGreaterEqual(len(set(referenced)), 100)

    def test_examples_and_broader_early_catalog_are_covered(self):
        early = []
        for entries in self.plan["lanes"].values():
            for _, pool_name, _, _, _ in entries[:8]:
                early.extend(self.plan["pools"][pool_name])
        expected = {
            "63171672192e68c5460cebc5",  # AUG A3
            "5ba26383d4351e00334c93d9",  # MP7A1
            "5bd70322209c4d00d7167b8f",  # MP7A2
            "57dc2fa62459775949412633",  # AKS-74U
            "5448bd6b4bdc2dfc2f8b4569",  # PM
            "576a581d2459771e7b1bc4f1",  # Grach
            "59e6152586f77473dc057aa1",  # VPO-136
            "574d967124597745970e7c94",  # SKS
            "5e870397991fd70db46995c8",  # Mossberg 590A1
        }
        self.assertTrue(expected.issubset(set(early)))

    def test_external_content_never_replaces_native_route(self):
        extension = self.plan["wttArmory"]
        self.assertFalse(extension["requiredDependency"])
        self.assertIn("native SPT weapon", extension["rule"])
        self.assertIn("No quest", extension["absenceBehavior"])

    def test_verified_wtt_sources_remain_optional_and_collision_free(self):
        candidates = json.loads(
            (ROOT / "manifests" / "optional-content-candidates.json").read_text(encoding="utf-8")
        )
        by_name = {entry["name"]: entry for entry in candidates["candidates"]}
        self.assertEqual("2.0.5", by_name["WTT Armory"]["runtime"]["version"])
        self.assertEqual("2.0.1", by_name["WTT Content Backport"]["runtime"]["version"])
        self.assertEqual("3.0.6", by_name["WTT CommonLib"]["runtime"]["version"])
        self.assertEqual(0, candidates["stableCampaignChanges"]["dependencies"])
        self.assertEqual(0, candidates["compatibilityEvidence"]["crossModTemplateIdCollisions"])
        self.assertEqual(0, candidates["compatibilityEvidence"]["nativeTemplateIdCollisions"])
        self.assertTrue(candidates["compatibilityEvidence"]["admiralRuntimeChangeRequired"])
        self.assertFalse(candidates["compatibilityEvidence"]["economyContract"]["foreignTemplateMutationAllowed"])


if __name__ == "__main__":
    unittest.main()
