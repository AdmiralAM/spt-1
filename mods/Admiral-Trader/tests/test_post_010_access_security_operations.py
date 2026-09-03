import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class Post010AccessSecurityOperationsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads(
            (ROOT / "manifests" / "post-010-access-security-operations.json").read_text(encoding="utf-8")
        )
        cls.by_key = {op["key"]: op for op in cls.manifest["operations"]}

    def test_specs_are_non_materialized_and_bound_to_frozen_base(self):
        self.assertIn(self.manifest["schemaVersion"], (1, 2, 3))
        self.assertEqual(self.manifest["status"], "post-0.1.0-authored-spec-only")
        self.assertEqual(
            self.manifest["frozen010Base"],
            "053a62ff5f1cb545f13bc89a96bba3acd319a823",
        )
        self.assertFalse(self.manifest["implementationAllowed"])
        self.assertTrue(all(op["runtimeMaterialize"] is False for op in self.manifest["operations"]))

    def test_expected_operations_are_bilingual_and_complete(self):
        self.assertEqual(
            set(self.by_key),
            {"access-reconnaissance", "labs-security-disruption", "hostile-operator-intercept"},
        )
        for operation in self.by_key.values():
            self.assertGreaterEqual(len(operation["objectiveIntent"]), 2)
            self.assertGreaterEqual(len(operation["proofGates"]), 4)
            self.assertTrue(operation["rewardDoctrine"].strip())
            for locale in ("en", "ru"):
                for field in ("description", "started", "success"):
                    self.assertGreater(len(operation["playerText"][locale][field].strip()), 30)

    def test_access_operation_cannot_become_key_collection_progression(self):
        access = self.by_key["access-reconnaissance"]["antiGrind"]
        self.assertEqual(access["maximumRequiredKeys"], 1)
        self.assertEqual(access["maximumRequiredSuccessfulRaids"], 1)
        self.assertTrue(access["noKeyCollectionLadder"])
        self.assertTrue(access["noPerMapCopies"])
        self.assertTrue(access["noPermanentKeyStorefront"])

    def test_security_operations_remain_bounded(self):
        labs = self.by_key["labs-security-disruption"]["antiGrind"]
        self.assertLessEqual(labs["maximumTargetCount"], 4)
        self.assertEqual(labs["maximumRequiredSuccessfulRaids"], 1)
        self.assertTrue(labs["noEscalatingSequels"])
        self.assertTrue(labs["noAccessUnlock"])
        self.assertTrue(labs["noLocationFreeFallback"])

        pmc = self.by_key["hostile-operator-intercept"]["antiGrind"]
        self.assertLessEqual(pmc["maximumTargetCount"], 4)
        self.assertEqual(pmc["maximumRequiredSuccessfulRaids"], 1)
        self.assertTrue(pmc["noEscalatingSequels"])
        self.assertTrue(pmc["noPerMapCopies"])
        self.assertTrue(pmc["noFactionKillLadder"])

        for operation in self.by_key.values():
            self.assertFalse(operation["antiGrind"].get("repeatable", False))

    def test_readiness_contract_when_schema_v2_is_present(self):
        if self.manifest["schemaVersion"] < 2:
            self.skipTest("schema v1 predates condition-readiness reconciliation")

        labs = self.by_key["labs-security-disruption"]["conditionReadiness"]
        self.assertEqual(labs["targetRole"]["status"], "proven-shape-only")
        self.assertEqual(labs["targetRole"]["savageRole"], ["pmcBot"])
        self.assertEqual(labs["location"]["status"], "proven-selected")
        self.assertEqual(labs["location"]["conditionType"], "Location")
        self.assertEqual(labs["location"]["target"], ["laboratory"])
        self.assertEqual(labs["location"]["locationId"], "5b0fc42d86f7744a585f9105")
        self.assertTrue(labs["location"]["selectedMapValueProven"])
        authority = " ".join(labs["location"]["authority"]).lower()
        self.assertIn("location-information.md", authority)
        self.assertIn("quest.json", authority)
        self.assertEqual(labs["survivedExtraction"]["status"], "proven-shape-only")
        self.assertEqual(labs["sameRaidCoupling"]["status"], "unproven-fail-closed")

        pmc = self.by_key["hostile-operator-intercept"]["conditionReadiness"]
        self.assertEqual(pmc["target"]["status"], "proven-shape-only")
        self.assertEqual(pmc["target"]["value"], "AnyPmc")
        self.assertEqual(pmc["location"]["status"], "proven-selected")
        self.assertEqual(pmc["location"]["target"], ["RezervBase"])
        self.assertEqual(pmc["location"]["locationId"], "5704e5fad2720bc05b8b4567")
        self.assertTrue(pmc["location"]["selectedMapValueProven"])
        self.assertEqual(pmc["location"]["authorityManifest"], "post-010-reserve-location-proof.json")
        self.assertEqual(pmc["survivedExtraction"]["status"], "proven-shape-only")
        self.assertEqual(pmc["sameRaidCoupling"]["status"], "unproven-fail-closed")
        self.assertFalse(pmc["factionSplitProven"])

        access = self.by_key["access-reconnaissance"]["conditionReadiness"]
        self.assertEqual(access["survivedExtraction"]["status"], "proven-shape-only")
        self.assertEqual(access["accessInteraction"]["status"], "proven-selected-proxy")
        self.assertEqual(access["accessInteraction"]["authorityManifest"], "post-010-visit-place-proxy-proof.json")
        self.assertEqual(access["accessInteraction"]["conditionType"], "VisitPlace")
        self.assertEqual(access["accessInteraction"]["locationTarget"], "bigmap")
        self.assertEqual(access["accessInteraction"]["target"], "room206_water")
        self.assertEqual(access["sameRaidCoupling"]["status"], "unproven-fail-closed")

    def test_labs_location_gate_is_closed_without_reopening_same_raid_gate(self):
        labs = self.by_key["labs-security-disruption"]
        gate_text = " ".join(labs["proofGates"]).lower()
        self.assertIn("exact labs location target laboratory", gate_text)
        self.assertIn("same-raid", gate_text)
        self.assertNotIn("select and validate the exact pinned spt 4.1.3 the labs location value", gate_text)
        self.assertFalse(labs["runtimeMaterialize"])

    def test_borrowed_access_proxy_is_reconciled_but_personal_key_use_is_not_claimed(self):
        access = self.by_key["access-reconnaissance"]
        readiness = access["conditionReadiness"]["accessInteraction"]
        self.assertEqual(readiness["map"], "Customs")
        self.assertEqual(readiness["target"], "room206_water")
        meaning = readiness["meaning"].lower()
        self.assertIn("not", meaning)
        self.assertIn("key", meaning)
        gate_text = " ".join(access["proofGates"]).lower()
        self.assertIn("room206_water", gate_text)
        self.assertIn("never infer personal key use", gate_text)
        self.assertIn("same-raid", gate_text)
        self.assertFalse(access["runtimeMaterialize"])

    def test_contractor_intercept_uses_selected_reserve_area_without_per_map_copies(self):
        pmc = self.by_key["hostile-operator-intercept"]
        location = pmc["conditionReadiness"]["location"]
        self.assertEqual(location["target"], ["RezervBase"])
        self.assertEqual(location["locationId"], "5704e5fad2720bc05b8b4567")
        self.assertTrue(location["selectedMapValueProven"])
        self.assertEqual(location["authorityManifest"], "post-010-reserve-location-proof.json")
        self.assertIn("reserve", pmc["mapSelectionRationale"].lower())
        self.assertIn("streets", pmc["mapSelectionRationale"].lower())
        self.assertIn("rejected", pmc["mapSelectionRationale"].lower())
        self.assertTrue(pmc["antiGrind"]["noPerMapCopies"])
        gate_text = " ".join(pmc["proofGates"]).lower()
        self.assertIn("rezervbase", gate_text)
        self.assertIn("same-raid", gate_text)
        self.assertIn("overlap", gate_text)
        self.assertIn("do not reopen the rejected streets variant", gate_text)
        self.assertFalse(pmc["runtimeMaterialize"])

    def test_specs_fail_closed_on_runtime_overlap_and_economy_gates(self):
        for operation in self.by_key.values():
            gate_text = " ".join(operation["proofGates"]).lower()
            self.assertIn("overlap", gate_text)
            self.assertIn("economy admiral", gate_text)

        if self.manifest["schemaVersion"] >= 2:
            access_gate_text = " ".join(self.by_key["access-reconnaissance"]["proofGates"]).lower()
            self.assertIn("post-010-visit-place-proxy-proof.json", access_gate_text)
            for key in ("labs-security-disruption", "hostile-operator-intercept"):
                gate_text = " ".join(self.by_key[key]["proofGates"]).lower()
                self.assertIn("same-raid", gate_text)

    def test_frozen_runtime_counts_are_unchanged(self):
        quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)


if __name__ == "__main__":
    unittest.main()
