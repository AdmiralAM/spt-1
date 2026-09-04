import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]

class Post010CurationAdmissionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads((ROOT / "manifests" / "post-010-curation-admission.json").read_text(encoding="utf-8"))

    def test_curation_is_post_010_and_never_direct_port(self):
        self.assertIn(self.manifest["schemaVersion"], (2, 3, 4))
        self.assertEqual(self.manifest["status"], "post-0.1.0-research-only")
        policy = self.manifest["policy"]
        self.assertFalse(policy["directLegacyQuestCopyAllowed"])
        self.assertFalse(policy["materializeIntoFrozen010"])
        self.assertFalse(policy["quotaDrivenAdmission"])
        self.assertTrue(policy["requireWhyWhatContextPayoff"])
        self.assertTrue(policy["requireExactSpt413ConditionProof"])
        self.assertTrue(policy["requireVanillaScorpionArtemOverlapAudit"])
        self.assertTrue(policy["requireEconomyAdmiralReview"])
        self.assertTrue(policy["laterSpecificDispositionOverridesEarlierGenericRewriteCandidate"])

    def test_every_reviewed_source_remains_non_materialized(self):
        admissions = self.manifest["admissions"]
        self.assertGreaterEqual(len(admissions), 12)
        self.assertTrue(all(entry["runtimeMaterialize"] is False for entry in admissions))
        by_name = {entry["sourceBundle"]: entry for entry in admissions}
        self.assertEqual(by_name["Deep Pockets"]["decision"], "reject-direct-port")
        self.assertFalse(any(entry["decision"] == "pending-source-review" for entry in admissions))
        self.assertTrue(all("requiredRewriteBoundary" in entry or entry["sourceBundle"] == "Deep Pockets" for entry in admissions))

    def test_medical_umbrella_respects_later_specific_dispositions(self):
        by_name = {entry["sourceBundle"]: entry for entry in self.manifest["admissions"]}
        meds = by_name["Meds Proficiency"]
        stims = by_name["Stims Proficiency"]
        self.assertEqual(meds["decision"], "consolidated-single-deferred-capability")
        self.assertEqual(meds["futureUse"], "field-medicine-under-pressure")
        self.assertEqual(meds["supersedingAuthority"], "post-010-field-medicine-disposition.json")
        self.assertIn("no second ordinary-medicine quest", meds["requiredRewriteBoundary"].lower())
        self.assertEqual(stims["decision"], "reject-current-derived-operation-vanilla-semantic-collision")
        self.assertEqual(stims["supersedingAuthority"], "post-010-medical-capability-curation.json")
        self.assertIn("do not resurrect stimulant-conditioned combat", stims["requiredRewriteBoundary"].lower())
        sync = self.manifest["synchronization"]
        self.assertEqual(sync["medsIndependentCandidateCount"], 1)
        self.assertFalse(sync["stimsCurrentDerivedOperationAccepted"])
        self.assertFalse(sync["frozenRuntimeChanged"])

    def test_gear_umbrella_respects_existing_specific_operations(self):
        by_name = {entry["sourceBundle"]: entry for entry in self.manifest["admissions"]}
        expected = {
            "Iron Head": ("consolidated-existing-protection-operations", "protection-calibration,ballistic-head-test"),
            "Juggernaut": ("consolidated-existing-protection-operations", "heavy-assault-loadout"),
            "Tarkov Mule": ("consolidated-existing-expedition-operation", "expedition-loadout"),
            "Ultrasound": ("consolidated-existing-acoustic-operation", "acoustic-contact"),
        }
        for bundle, (decision, target) in expected.items():
            entry = by_name[bundle]
            self.assertEqual(entry["decision"], decision)
            self.assertEqual(entry["futureUse"], target)
            self.assertIn("supersedingAuthority", entry)
        self.assertEqual(self.manifest["synchronization"].get("remainingGenericGearRewriteCandidateCount"), 0)

    def test_combat_survival_umbrella_respects_specific_dispositions(self):
        if self.manifest["schemaVersion"] < 4:
            self.skipTest("specific combat/survival umbrella synchronization not materialized yet")
        by_name = {entry["sourceBundle"]: entry for entry in self.manifest["admissions"]}
        expected = {
            "Boss Hunt": ("deferred-specific-command-window", "high-value-target-window"),
            "Boss Follower Hunt": ("deferred-specific-command-window", "high-value-target-window"),
            "Cultists Hunt": ("deferred-specific-black-signal", "night-signal-disruption"),
            "Sniper Life": ("consolidated-existing-precision-operations", "precision-observation-window,precision-denial"),
            "Survivalist": ("consolidated-existing-survival-operation", "endurance-circuit"),
        }
        for bundle, (decision, target) in expected.items():
            entry = by_name[bundle]
            self.assertEqual(entry["decision"], decision)
            self.assertEqual(entry["futureUse"], target)
            self.assertIn("supersedingAuthority", entry)
        sync = self.manifest["synchronization"]
        self.assertEqual(sync["remainingGenericCombatSurvivalRewriteCandidateCount"], 0)
        self.assertEqual(sync["remainingGenericRewriteCandidateCount"], 0)

    def test_support_rewrites_do_not_recreate_storefront_conveyors(self):
        by_name = {entry["sourceBundle"]: entry for entry in self.manifest["admissions"]}
        ultrasound = by_name["Ultrasound"]
        self.assertEqual(ultrasound["futureUse"], "acoustic-contact")
        self.assertIn("no headset-by-headset", ultrasound["requiredRewriteBoundary"].lower())
        self.assertIn("no 72-quest", ultrasound["requiredRewriteBoundary"].lower())

    def test_frozen_runtime_counts_are_unchanged(self):
        quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)

if __name__ == "__main__":
    unittest.main()
