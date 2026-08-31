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
        cls.by_key = {op["key"]: op for op in cls.manifest["operations"]}

    def test_specs_are_non_materialized_and_bound_to_frozen_base(self):
        self.assertEqual(self.manifest["schemaVersion"], 3)
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

    def test_protective_specs_consume_proven_equipment_family_without_overclaiming(self):
        authority = self.manifest["equipmentConditionAuthority"]
        proof = self.equipment_proof
        self.assertEqual(authority["manifest"], "post-010-player-equipment-proof.json")
        self.assertEqual(authority["conditionType"], proof["proven"]["playerEquipmentConditionFamilyExists"]["conditionType"])
        self.assertFalse(authority["includeNotEquippedItems"])
        self.assertEqual(authority["selectionMode"], "explicit-validated-tpl-allowlist")
        forbidden = " ".join(authority["forbiddenInference"]).lower()
        self.assertIn("category", forbidden)
        self.assertIn("armor class", forbidden)
        for operation in self.manifest["operations"]:
            plan = operation["equipmentPlan"]
            self.assertEqual(plan["conditionType"], "Equipment")
            self.assertFalse(plan["IncludeNotEquippedItems"])
            self.assertFalse(plan["materializationReady"])
            self.assertIn("explicit", plan["equipmentInclusiveSource"].lower())
            self.assertIn("pinned spt 4.1.3", plan["remainingBlocker"].lower())

    def test_survived_extraction_shape_is_not_reopened_or_overclaimed(self):
        authority = self.manifest["survivedExtractionAuthority"]
        self.assertEqual(authority["status"], "proven-shape-only")
        self.assertEqual(authority["conditionShape"], ["Location", "ExitStatus(Survived)"])
        self.assertFalse(authority["sameRaidEquipmentCouplingProven"])
        self.assertIn("does not prove", authority["boundary"].lower())
        for operation in self.manifest["operations"]:
            readiness = operation["conditionReadiness"]
            self.assertEqual(readiness["equipmentShape"], "proven")
            self.assertEqual(readiness["survivedExtractionShape"], "proven")
            self.assertEqual(
                readiness["equipmentToExtractionSameRaidCoupling"],
                "unproven-not-required-for-initial-materialization",
            )

    def test_armored_transit_rejects_legacy_equipment_ladders(self):
        operation = self.by_key["armored-transit"]
        self.assertEqual(operation["sourceBundles"], ["Iron Head", "Juggernaut"])
        anti = operation["antiGrind"]
        self.assertEqual(anti["maximumRequiredSuccessfulRaids"], 1)
        self.assertLessEqual(anti["maximumTargetCount"], 4)
        self.assertTrue(anti["noArmorCollection"])
        self.assertTrue(anti["noEscalatingProtectionClasses"])
        self.assertTrue(anti["noPerItemUnlockChain"])
        self.assertFalse(anti["repeatable"])

    def test_acoustic_contact_rejects_ultrasound_storefront_and_legend_grid(self):
        operation = self.by_key["acoustic-contact"]
        self.assertEqual(operation["sourceBundles"], ["Ultrasound"])
        anti = operation["antiGrind"]
        self.assertEqual(anti["maximumRequiredSuccessfulRaids"], 1)
        self.assertLessEqual(anti["maximumTargetCount"], 3)
        self.assertTrue(anti["noHeadsetByHeadsetLadder"])
        self.assertTrue(anti["noLegendGearKillExpansion"])
        self.assertTrue(anti["noSequentialStorefrontUnlocks"])
        self.assertFalse(anti["repeatable"])

    def test_specs_fail_closed_on_runtime_overlap_and_economy_gates(self):
        proof_text = " ".join(
            gate.lower()
            for operation in self.manifest["operations"]
            for gate in operation["proofGates"]
        )
        self.assertIn("pinned spt 4.1.3", proof_text)
        self.assertIn("overlap", proof_text)
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
