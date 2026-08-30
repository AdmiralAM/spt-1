import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class Post010CurationAdmissionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads(
            (ROOT / "manifests" / "post-010-curation-admission.json").read_text(encoding="utf-8")
        )

    def test_curation_is_post_010_and_never_direct_port(self):
        self.assertEqual(self.manifest["schemaVersion"], 1)
        self.assertEqual(self.manifest["status"], "post-0.1.0-research-only")
        policy = self.manifest["policy"]
        self.assertFalse(policy["directLegacyQuestCopyAllowed"])
        self.assertFalse(policy["materializeIntoFrozen010"])
        self.assertFalse(policy["quotaDrivenAdmission"])
        self.assertTrue(policy["requireWhyWhatContextPayoff"])
        self.assertTrue(policy["requireExactSpt413ConditionProof"])
        self.assertTrue(policy["requireVanillaScorpionArtemOverlapAudit"])
        self.assertTrue(policy["requireEconomyAdmiralReview"])

    def test_every_reviewed_source_remains_non_materialized(self):
        admissions = self.manifest["admissions"]
        self.assertGreaterEqual(len(admissions), 12)
        self.assertTrue(all(entry["runtimeMaterialize"] is False for entry in admissions))

        by_name = {entry["sourceBundle"]: entry for entry in admissions}
        self.assertEqual(by_name["Deep Pockets"]["decision"], "reject-direct-port")
        for rewrite in (
            "Iron Head",
            "Meds Proficiency",
            "Juggernaut",
            "Tarkov Mule",
            "Ultrasound",
            "Stims Proficiency",
            "Boss Hunt",
            "Boss Follower Hunt",
            "Cultists Hunt",
            "Sniper Life",
            "Survivalist",
        ):
            self.assertEqual(by_name[rewrite]["decision"], "rewrite-candidate")
            self.assertIn("requiredRewriteBoundary", by_name[rewrite])
            self.assertGreaterEqual(len(by_name[rewrite].get("evidence", [])), 3)
        self.assertFalse(any(entry["decision"] == "pending-source-review" for entry in admissions))

    def test_support_rewrites_do_not_recreate_storefront_conveyors(self):
        by_name = {entry["sourceBundle"]: entry for entry in self.manifest["admissions"]}
        ultrasound = by_name["Ultrasound"]
        stims = by_name["Stims Proficiency"]

        self.assertEqual(ultrasound["futureUse"], "acoustic-awareness field doctrine")
        self.assertIn("no headset-by-headset", ultrasound["requiredRewriteBoundary"].lower())
        self.assertIn("no 72-quest", ultrasound["requiredRewriteBoundary"].lower())

        self.assertEqual(stims["futureUse"], "controlled stimulant field doctrine")
        self.assertIn("avoid repeated-use grind", stims["requiredRewriteBoundary"].lower())
        self.assertIn("economy admiral review", stims["requiredRewriteBoundary"].lower())

    def test_operations_are_compact_rewrites_not_count_ladders(self):
        by_name = {entry["sourceBundle"]: entry for entry in self.manifest["admissions"]}
        boundaries = {
            name: by_name[name]["requiredRewriteBoundary"].lower()
            for name in ("Boss Hunt", "Boss Follower Hunt", "Cultists Hunt", "Sniper Life", "Survivalist")
        }

        self.assertIn("no repeated boss-count ladder", boundaries["Boss Hunt"])
        self.assertIn("no follower farming ladder", boundaries["Boss Follower Hunt"])
        self.assertIn("no cumulative cultist ladder", boundaries["Cultists Hunt"])
        self.assertIn("never expand by repetitive 10 m", boundaries["Sniper Life"])
        self.assertIn("no per-map five-quest template", boundaries["Survivalist"])

        self.assertEqual(by_name["Boss Hunt"]["futureUse"], "single high-value-target operations")
        self.assertEqual(by_name["Survivalist"]["futureUse"], "expedition survival and extraction proof")

    def test_frozen_runtime_counts_are_unchanged(self):
        quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)


if __name__ == "__main__":
    unittest.main()
