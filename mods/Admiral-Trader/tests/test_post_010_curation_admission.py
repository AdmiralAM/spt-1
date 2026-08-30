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
        self.assertGreaterEqual(len(admissions), 7)
        self.assertTrue(all(entry["runtimeMaterialize"] is False for entry in admissions))

        by_name = {entry["sourceBundle"]: entry for entry in admissions}
        self.assertEqual(by_name["Deep Pockets"]["decision"], "reject-direct-port")
        self.assertEqual(by_name["Iron Head"]["decision"], "rewrite-candidate")
        self.assertEqual(by_name["Meds Proficiency"]["decision"], "rewrite-candidate")
        for pending in ("Juggernaut", "Tarkov Mule", "Ultrasound", "Stims Proficiency"):
            self.assertEqual(by_name[pending]["decision"], "pending-source-review")

    def test_frozen_runtime_counts_are_unchanged(self):
        quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)


if __name__ == "__main__":
    unittest.main()
