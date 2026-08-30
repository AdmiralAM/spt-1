import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class Post010CoreBundleReviewTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.review = json.loads(
            (ROOT / "manifests" / "post-010-core-bundle-review.json").read_text(encoding="utf-8")
        )
        cls.by_name = {entry["sourceBundle"]: entry for entry in cls.review["reviews"]}

    def test_review_is_research_only_and_non_materialized(self):
        self.assertEqual(self.review["schemaVersion"], 1)
        self.assertEqual(self.review["status"], "post-0.1.0-research-only")
        self.assertGreaterEqual(len(self.review["reviews"]), 12)
        self.assertTrue(all(entry["runtimeMaterialize"] is False for entry in self.review["reviews"]))
        self.assertTrue(all(len(entry.get("evidence", [])) >= 3 for entry in self.review["reviews"]))
        self.assertTrue(all(entry.get("requiredRewriteBoundary") for entry in self.review["reviews"]))

    def test_duplicate_weapon_and_ammo_progression_is_rejected(self):
        for name in ("Ammo Proficiency", "Weapon Proficiency", "Weapon Mastery"):
            self.assertEqual(self.by_name[name]["decision"], "reject-direct-port")
        self.assertIn("existing authored munitions", self.by_name["Ammo Proficiency"]["requiredRewriteBoundary"].lower())
        self.assertIn("duplicate arsenal authority", self.by_name["Weapon Proficiency"]["requiredRewriteBoundary"].lower())
        self.assertIn("never materialize mastery by weapon enumeration", self.by_name["Weapon Mastery"]["requiredRewriteBoundary"].lower())

    def test_procurement_and_combat_templates_cannot_return_as_volume(self):
        self.assertEqual(self.by_name["Errand Boy"]["decision"], "reject-direct-port")
        self.assertIn("no generic requested-item queue", self.by_name["Errand Boy"]["requiredRewriteBoundary"].lower())
        self.assertIn("no 1-to-500 ladder", self.by_name["PMC Hunt"]["requiredRewriteBoundary"].lower())
        self.assertIn("no four-digit kill requirement", self.by_name["Scav Hunt"]["requiredRewriteBoundary"].lower())
        self.assertIn("no 1-to-100 raider ladder", self.by_name["Raider Hunt"]["requiredRewriteBoundary"].lower())
        self.assertIn("no 1-to-100 ladder", self.by_name["Rogue Hunt"]["requiredRewriteBoundary"].lower())

    def test_precision_and_legend_grids_are_not_second_progression_engines(self):
        self.assertEqual(self.by_name["Headless PMC"]["decision"], "reject-direct-port")
        self.assertEqual(self.by_name["Headless Scav"]["decision"], "reject-direct-port")
        legends = self.by_name["Deep Pockets Legend / Iron Head Legend / Juggernaut Legend / Ultrasound Legend"]
        self.assertEqual(legends["decision"], "reject-direct-port")
        self.assertIn("no gear-by-gear kill matrix", legends["requiredRewriteBoundary"].lower())

    def test_frozen_runtime_remains_31_quests_and_11_root_offers(self):
        quest_files = list((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)


if __name__ == "__main__":
    unittest.main()
