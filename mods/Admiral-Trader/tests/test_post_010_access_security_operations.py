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
        self.assertEqual(self.manifest["schemaVersion"], 1)
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

    def test_specs_fail_closed_on_runtime_overlap_and_economy_gates(self):
        for operation in self.by_key.values():
            gate_text = " ".join(operation["proofGates"]).lower()
            self.assertIn("exact spt 4.1.3", gate_text)
            self.assertIn("overlap", gate_text)
            self.assertIn("economy admiral", gate_text)

    def test_frozen_runtime_counts_are_unchanged(self):
        quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)


if __name__ == "__main__":
    unittest.main()
