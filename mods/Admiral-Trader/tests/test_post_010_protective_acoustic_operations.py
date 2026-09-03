import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class Post010ProtectiveAcousticOperationsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads(
            (ROOT / "manifests" / "post-010-protective-acoustic-operations.json").read_text(encoding="utf-8")
        )
        cls.equipment_proof = json.loads(
            (ROOT / "manifests" / "post-010-player-equipment-proof.json").read_text(encoding="utf-8")
        )
        cls.allowlist_proof = json.loads(
            (ROOT / "manifests" / "post-010-protective-acoustic-equipment-allowlist-proof.json").read_text(encoding="utf-8")
        )
        cls.by_key = {op["key"]: op for op in cls.manifest["operations"]}

    def test_specs_are_non_materialized_and_bound_to_frozen_base(self):
        self.assertEqual(self.manifest["schemaVersion"], 6)
        self.assertEqual(self.manifest["status"], "post-0.1.0-authored-spec-only")
        self.assertEqual(
            self.manifest["frozen010Base"],
            "053a62ff5f1cb545f13bc89a96bba3acd319a823",
        )
        self.assertFalse(self.manifest["implementationAllowed"])
        self.assertEqual(set(self.by_key), {"armored-transit", "acoustic-contact"})
        self.assertTrue(all(op["runtimeMaterialize"] is False for op in self.manifest["operations"]))

    def test_player_copy_is_bilingual_and_authored(self):
        for operation in self.manifest["operations"]:
            self.assertGreaterEqual(len(operation["objectiveIntent"]), 2)
            self.assertGreaterEqual(len(operation["proofGates"]), 4)
            self.assertTrue(operation["rewardDoctrine"].strip())
            for locale in ("en", "ru"):
                for field in ("description", "started", "success"):
                    self.assertGreater(len(operation["playerText"][locale][field].strip()), 30)

    def test_protective_specs_consume_resolved_explicit_allowlists_without_overclaiming(self):
        authority = self.manifest["equipmentConditionAuthority"]
        proof = self.equipment_proof
        self.assertEqual(authority["manifest"], "post-010-player-equipment-proof.json")
        self.assertEqual(authority["conditionType"], proof["proven"]["playerEquipmentConditionFamilyExists"]["conditionType"])
        self.assertFalse(authority["includeNotEquippedItems"])
        self.assertEqual(authority["selectionMode"], "explicit-validated-tpl-allowlist")
        self.assertEqual(authority["allowlistProofManifest"], "post-010-protective-acoustic-equipment-allowlist-proof.json")
        self.assertTrue(authority["exactPinnedTplAllowlistsResolved"])
        forbidden = " ".join(authority["forbiddenInference"]).lower()
        self.assertIn("category", forbidden)
        self.assertIn("armor class", forbidden)
        for operation in self.manifest["operations"]:
            plan = operation["equipmentPlan"]
            allowlist = self.allowlist_proof["operations"][operation["key"]]
            self.assertEqual(plan["conditionType"], "Equipment")
            self.assertFalse(plan["IncludeNotEquippedItems"])
            self.assertFalse(plan["materializationReady"])
            self.assertIn("explicit", plan["equipmentInclusiveSource"].lower())
            self.assertTrue(plan["exactPinnedTplAllowlistResolved"])
            self.assertEqual(plan["allowlistAuthority"], "post-010-protective-acoustic-equipment-allowlist-proof.json")
            self.assertEqual(plan["equipmentInclusive"], allowlist["equipmentInclusive"])
            self.assertEqual(plan["explicitTplCount"], allowlist["explicitTplCount"])
            self.assertNotIn("tpl allowlist", plan["remainingBlocker"].lower())

    def test_survived_extraction_shape_is_not_reopened_or_overclaimed(self):
        authority = self.manifest["survivedExtractionAuthority"]
        self.assertEqual(authority["status"], "proven-shape-only")
        self.assertEqual(authority["conditionShape"], ["Location", "ExitStatus(Survived)"])
        self.assertFalse(authority["sameRaidEquipmentCouplingProven"])
        self.assertIn("shape only", authority["boundary"].lower())
        self.assertIn("same raid", authority["boundary"].lower())
        for operation in self.manifest["operations"]:
            readiness = operation["conditionReadiness"]
            self.assertEqual(readiness["equipmentShape"], "proven")
            self.assertEqual(readiness["survivedExtractionShape"], "proven")
            self.assertEqual(readiness["boundedMapContactSelection"], "selected")
            self.assertEqual(
                readiness["equipmentToExtractionSameRaidCoupling"],
                "unproven-required-before-runtime-materialization",
            )

    def test_bounded_operation_shapes_are_selected_without_runtime_overclaim(self):
        armored = self.by_key["armored-transit"]["boundedOperation"]
        self.assertEqual(armored["location"], "factory4_day")
        self.assertEqual(armored["contact"], {"target": "AnyPmc", "count": 2})
        self.assertEqual(armored["extraction"], {"location": "factory4_day", "exitStatus": "Survived"})
        self.assertEqual(armored["selectionStatus"], "selected-pending-overlap-economy-and-same-raid-proof")

        acoustic = self.by_key["acoustic-contact"]["boundedOperation"]
        self.assertEqual(acoustic["location"], "woods")
        self.assertEqual(acoustic["contact"], {"target": "Savage", "count": 2})
        self.assertEqual(acoustic["extraction"], {"location": "woods", "exitStatus": "Survived"})
        self.assertEqual(acoustic["selectionStatus"], "selected-pending-overlap-economy-and-same-raid-proof")

    def test_same_raid_coupling_blocks_runtime_materialization(self):
        policy = self.manifest["sameRaidCouplingMaterializationPolicy"]
        self.assertTrue(policy["requiredBeforeRuntimeMaterialization"])
        self.assertEqual(policy["authority"], "post-010-same-raid-coupling-proof.json")
        self.assertTrue(policy["failClosed"])
        for operation in self.manifest["operations"]:
            blockers = " ".join(operation["materializationBlockedBy"]).lower()
            self.assertIn("same-raid", blockers)
            self.assertNotIn("tpl allowlist", blockers)
            proof_text = " ".join(operation["proofGates"]).lower()
            self.assertIn("same-raid coupling before runtime materialization", proof_text)
            self.assertIn("different raids", proof_text)
            self.assertNotIn("not-required-for-initial-materialization", operation["conditionReadiness"]["equipmentToExtractionSameRaidCoupling"])

    def test_armored_transit_rejects_legacy_equipment_ladders(self):
        operation = self.by_key["armored-transit"]
        self.assertEqual(operation["sourceBundles"], ["Iron Head", "Juggernaut"])
        anti = operation["antiGrind"]
        self.assertEqual(anti["maximumRequiredSuccessfulRaids"], 1)
        self.assertEqual(anti["maximumTargetCount"], 2)
        self.assertTrue(anti["noArmorCollection"])
        self.assertTrue(anti["noEscalatingProtectionClasses"])
        self.assertTrue(anti["noPerItemUnlockChain"])
        self.assertFalse(anti["repeatable"])

    def test_acoustic_contact_rejects_ultrasound_storefront_and_legend_grid(self):
        operation = self.by_key["acoustic-contact"]
        self.assertEqual(operation["sourceBundles"], ["Ultrasound"])
        anti = operation["antiGrind"]
        self.assertEqual(anti["maximumRequiredSuccessfulRaids"], 1)
        self.assertEqual(anti["maximumTargetCount"], 2)
        self.assertTrue(anti["noHeadsetByHeadsetLadder"])
        self.assertTrue(anti["noLegendGearKillExpansion"])
        self.assertTrue(anti["noSequentialStorefrontUnlocks"])
        self.assertFalse(anti["repeatable"])

    def test_specs_fail_closed_on_remaining_runtime_overlap_and_economy_gates(self):
        proof_text = " ".join(
            gate.lower()
            for operation in self.manifest["operations"]
            for gate in operation["proofGates"]
        )
        self.assertIn("pinned spt 4.1.3", proof_text)
        self.assertIn("allowlist is resolved", proof_text)
        self.assertIn("vanilla overlap", proof_text)
        self.assertIn("economy admiral", proof_text)
        self.assertIn("same-raid coupling", proof_text)
        self.assertNotIn("prove survived/extraction semantics", proof_text)
        self.assertNotIn("prove exact spt 4.1.3 equipment-condition shape", proof_text)

    def test_frozen_runtime_counts_are_unchanged(self):
        quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)


if __name__ == "__main__":
    unittest.main()
