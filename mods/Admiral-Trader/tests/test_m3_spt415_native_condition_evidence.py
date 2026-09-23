import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"


def load_json(name: str):
    return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))


class M3Spt415NativeConditionEvidenceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.evidence = load_json("spt415-native-condition-evidence.json")
        cls.context = load_json("m3-operation-context-review.json")

    def test_exact_runtime_provenance_is_pinned(self):
        evidence = self.evidence
        self.assertEqual(evidence["targetSptVersion"], "4.1.5")
        self.assertFalse(evidence["runtimeMaterialize"])
        source = evidence["source"]
        self.assertEqual(source["archiveSha256"], "5cc04274c88115730fe982fd12c7525d57e5fc64b6b7271ab3929383e3ac4432")
        self.assertEqual(source["itemsDatabaseRecords"], 4673)
        self.assertEqual(source["questsDatabaseRecords"], 558)
        self.assertEqual(source["itemsDatabaseSha256"], "5093b348fd0d507cddc1e957114a77b44ff9358846299cbc98ac5e0ad4967a34")
        self.assertEqual(source["questsDatabaseSha256"], "81d3ffc69f009723d62e3da93d4c88f3da108fc9cfb0657ce827913efe359956")

    def test_required_native_primitives_are_observed(self):
        counts = self.evidence["nativeConditionCounts"]
        for condition in ("CounterCreator", "Kills", "Location", "ExitStatus", "VisitPlace", "Equipment", "FindItem", "HandoverItem"):
            with self.subTest(condition=condition):
                self.assertGreater(counts[condition], 0)

        locations = self.evidence["provenLocationTargets"]
        for target in ("Woods", "Shoreline", "bigmap", "RezervBase", "Lighthouse", "Interchange", "laboratory", "factory4_day", "factory4_night"):
            with self.subTest(target=target):
                self.assertGreater(locations[target], 0)

        self.assertEqual(self.evidence["provenVisitPlaceTargets"]["room206_water"], 1)
        self.assertGreater(self.evidence["provenKillTargets"]["Savage"], 0)
        self.assertGreater(self.evidence["provenKillTargets"]["AnyPmc"], 0)
        self.assertGreater(self.evidence["provenSavageRoleCandidates"]["exUsec"], 0)
        self.assertGreater(self.evidence["provenSavageRoleCandidates"]["pmcBot"], 0)

    def test_same_raid_kill_and_exit_is_not_overclaimed(self):
        negative = self.evidence["negativeEvidence"]
        self.assertFalse(negative["killsPlusExitStatusCounterObserved"])
        self.assertFalse(negative["killsPlusExitStatusPlusLocationCounterObserved"])
        self.assertFalse(negative["sameRaidKillAndSurviveSemanticsProven"])
        compositions = self.evidence["provenCounterCompositions"]
        self.assertIn("Kills+Location", compositions)
        self.assertIn("ExitStatus+Location", compositions)
        self.assertNotIn("Kills+ExitStatus", compositions)
        self.assertNotIn("ExitStatus+Kills+Location", compositions)

    def test_context_plan_consumes_exact_primitives_without_materializing(self):
        context = self.context
        self.assertEqual(context["nativeConditionEvidence"], "spt415-native-condition-evidence.json")
        self.assertFalse(context["runtimeMaterialize"])
        rows = context["contexts"]
        self.assertEqual(rows["borrowed-access"]["nativeLocationTarget"], "bigmap")
        self.assertEqual(rows["borrowed-access"]["nativeVisitPlaceTarget"], "room206_water")
        self.assertEqual(rows["contractor-intercept"]["nativeLocationTarget"], "RezervBase")
        self.assertEqual(rows["contractor-intercept"]["nativeKillTarget"], "AnyPmc")
        self.assertEqual(rows["internal-security"]["nativeLocationTarget"], "laboratory")
        self.assertEqual(rows["break-the-perimeter"]["nativeLocationTarget"], "Lighthouse")
        self.assertEqual(rows["heavy-assault"]["nativeLocationTargets"], ["factory4_day", "factory4_night"])

    def test_semantic_and_overlap_gates_remain_fail_closed(self):
        rows = self.context["contexts"]
        self.assertEqual(rows["break-the-perimeter"]["nativeSavageRoleCandidate"], "exUsec")
        self.assertIn("semantic", rows["break-the-perimeter"]["status"])
        self.assertEqual(rows["internal-security"]["nativeSavageRoleCandidate"], "pmcBot")
        self.assertIn("semantic", rows["internal-security"]["status"])
        self.assertIn("does not prove", rows["internal-security"]["sameRaidBoundary"])

        gate = self.context["materializationGate"]
        self.assertTrue(gate["requiresExactSpt415RoleAliases"])
        self.assertTrue(gate["requiresCurrentVanillaOverlapReview"])
        self.assertTrue(gate["requiresApprovedExternalOverlapReview"])
        self.assertTrue(gate["requiresSameRaidCouplingProofWherePromised"])
        self.assertFalse(gate["requiresUserPhysicalTestNow"])


if __name__ == "__main__":
    unittest.main()
