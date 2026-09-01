import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class RelationshipWeaponRepairKitResolutionTests(unittest.TestCase):
    def setUp(self):
        self.resolution = json.loads(
            (ROOT / "manifests" / "relationship-weapon-repair-kit-resolution.json").read_text(encoding="utf-8")
        )
        self.candidates = json.loads(
            (ROOT / "manifests" / "relationship-stock-candidates.json").read_text(encoding="utf-8")
        )
        self.relationship = json.loads(
            (ROOT / "manifests" / "relationship-stock.json").read_text(encoding="utf-8")
        )
        self.assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))

    def test_resolution_closes_previous_hold_without_materialization(self):
        r = self.resolution
        self.assertEqual(r["status"], "ResolvedPreMaterialization")
        self.assertEqual(r["candidate"]["tpl"], "5910968f86f77425cf569c32")
        self.assertEqual(r["sourceBaseline"]["sourceCandidateDecision"], "hold-pinned-vanilla-overlap")
        self.assertTrue(r["evidence"]["pinnedVanillaDirectOffer"])
        self.assertEqual(r["evidence"]["pinnedVanillaStock"], "unlimited")
        self.assertEqual(r["evidence"]["sourceCandidateOverlapValue"], "ref-direct-unlimited")
        self.assertTrue(r["policy"]["unlimitedVanillaDirectOfferDisqualifies"])
        self.assertFalse(r["policy"]["economyApprovalCanOverrideGameplayPurposeGate"])
        self.assertEqual(r["decision"]["state"], "reject-redundant-pinned-vanilla")
        self.assertFalse(r["decision"]["materialize"])
        self.assertTrue(r["conclusion"]["previousHoldResolved"])
        self.assertFalse(r["conclusion"]["relationshipOfferAdded"])
        self.assertTrue(r["conclusion"]["relationshipMaterializationStillDisabled"])

    def test_resolution_is_consistent_with_source_evidence_and_empty_tier_policy(self):
        source = {x["tpl"]: x for x in self.candidates["candidates"]}["5910968f86f77425cf569c32"]
        self.assertEqual(source["decision"], self.resolution["sourceBaseline"]["sourceCandidateDecision"])
        self.assertEqual(source["overlap"]["vanillaPinnedAssort"], "ref-direct-unlimited")
        self.assertTrue(self.relationship["designRules"]["emptyTierAllowedWhenNoCandidatePasses"])
        self.assertFalse(self.relationship["designRules"]["tierFillQuotaAllowed"])
        self.assertFalse(self.resolution["conclusion"]["replacementCandidateRequired"])

    def test_rejected_repair_kit_is_not_in_frozen_runtime_assort(self):
        live_tpls = {item["_tpl"] for item in self.assort["items"]}
        self.assertNotIn(self.resolution["candidate"]["tpl"], live_tpls)
        self.assertEqual(len(self.assort["items"]), 11)
        self.assertFalse(self.relationship["materialization"]["enabled"])


if __name__ == "__main__":
    unittest.main()
