import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class Post010VisitPlaceProxyProofTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.proof = json.loads(
            (ROOT / "manifests" / "post-010-visit-place-proxy-proof.json").read_text(encoding="utf-8")
        )
        cls.access_manifest = json.loads(
            (ROOT / "manifests" / "post-010-access-security-operations.json").read_text(encoding="utf-8")
        )
        cls.access = next(
            operation
            for operation in cls.access_manifest["operations"]
            if operation["key"] == "access-reconnaissance"
        )

    def test_visit_place_shape_is_pinned_to_spt_authority(self):
        authority = self.proof["authority"]
        self.assertEqual(authority["repository"], "sp-tarkov/wiki")
        self.assertEqual(authority["commit"], "6989394add62bdda19af7108cde2510288d3568b")
        self.assertEqual(authority["path"], "modding/references/quest-values.md")
        self.assertEqual(authority["section"], "Visit Place")

        shape = self.proof["provenShape"]
        self.assertEqual(shape["outerConditionType"], "CounterCreator")
        self.assertEqual(shape["innerConditionType"], "VisitPlace")
        self.assertEqual(shape["innerValue"], 1)
        self.assertTrue(shape["counterValueOwnsCompletionCount"])
        self.assertIn("visit-zone ID", shape["innerTargetSemantics"])

    def test_proxy_does_not_claim_key_or_door_semantics(self):
        boundary = self.proof["admiralBoundary"]
        rejected = " ".join(boundary["doesNotProve"]).lower()
        for concept in ("door was locked", "key was possessed", "key unlocked", "opened by the player", "same raid"):
            self.assertIn(concept, rejected)
        self.assertFalse(boundary["selectedRestrictedRouteProven"])
        self.assertFalse(boundary["sameRaidCouplingProven"])
        self.assertFalse(boundary["implementationAllowed"])
        self.assertFalse(self.proof["runtimeMaterialize"])

    def test_borrowed_access_remains_fail_closed_until_route_and_coupling_are_proven(self):
        readiness = self.access["conditionReadiness"]
        self.assertEqual(readiness["accessInteraction"]["status"], "unproven-fail-closed")
        self.assertEqual(readiness["sameRaidCoupling"]["status"], "unproven-fail-closed")
        gate_text = " ".join(self.proof["materializationGate"]).lower()
        self.assertIn("exact vanilla visit-zone id", gate_text)
        self.assertIn("restricted/keyed access route", gate_text)
        self.assertIn("same-raid", gate_text)
        self.assertIn("overlap", gate_text)
        self.assertIn("economy admiral", gate_text)
        self.assertFalse(self.access["runtimeMaterialize"])

    def test_visit_place_proxy_cannot_publish_false_opened_door_claim(self):
        rejected = self.proof["rejectedInference"].lower()
        self.assertIn("presence", rejected)
        self.assertIn("must never be described", rejected)
        self.assertIn("key opened a locked door", rejected)


if __name__ == "__main__":
    unittest.main()
