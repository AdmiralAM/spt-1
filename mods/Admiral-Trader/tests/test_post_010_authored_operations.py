import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class Post010AuthoredOperationsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.manifest = json.loads((ROOT / "manifests" / "post-010-authored-operations.json").read_text(encoding="utf-8"))

    def test_specs_are_non_materialized_and_bound_to_frozen_base(self):
        self.assertEqual(self.manifest["schemaVersion"], 1)
        self.assertEqual(self.manifest["status"], "post-0.1.0-authored-spec-only")
        self.assertEqual(self.manifest["frozen010Base"], "053a62ff5f1cb545f13bc89a96bba3acd319a823")
        self.assertFalse(self.manifest["implementationAllowed"])
        self.assertTrue(all(op["runtimeMaterialize"] is False for op in self.manifest["operations"]))

    def test_operations_are_authored_and_bilingual(self):
        operations = self.manifest["operations"]
        self.assertGreaterEqual(len(operations), 3)
        keys = {op["key"] for op in operations}
        self.assertEqual(len(keys), len(operations))
        self.assertNotIn("expedition-discipline", keys)
        self.assertNotIn("field-medicine-under-pressure", keys)
        self.assertTrue({"rogue-interdiction", "high-value-target-window", "night-signal-disruption"}.issubset(keys))
        for operation in operations:
            self.assertGreaterEqual(len(operation["objectiveIntent"]), 2)
            self.assertGreaterEqual(len(operation["proofGates"]), 4)
            self.assertTrue(operation["rewardDoctrine"].strip())
            for locale in ("en", "ru"):
                text = operation["playerText"][locale]
                for field in ("description", "started", "success"):
                    self.assertGreater(len(text[field].strip()), 30)

    def test_no_source_template_grind_is_reintroduced(self):
        by_key = {op["key"]: op for op in self.manifest["operations"]}
        rogue = by_key["rogue-interdiction"]
        self.assertLessEqual(rogue["antiGrind"]["maximumTargetCount"], 6)
        self.assertTrue(rogue["antiGrind"]["noEscalatingSequels"])
        self.assertTrue(rogue["antiGrind"]["noLocationFreeFallback"])
        for operation in self.manifest["operations"]:
            anti_grind = operation["antiGrind"]
            if "maximumTargetCount" in anti_grind:
                self.assertLessEqual(anti_grind["maximumTargetCount"], 6)
            self.assertFalse(anti_grind.get("repeatable", False))

    def test_specs_fail_closed_on_unproven_runtime_semantics(self):
        proof_text = " ".join(gate.lower() for operation in self.manifest["operations"] for gate in operation["proofGates"])
        self.assertIn("exact spt 4.1.3", proof_text)
        self.assertIn("overlap", proof_text)
        self.assertIn("economy admiral", proof_text)

    def test_frozen_runtime_counts_are_unchanged(self):
        quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)


if __name__ == "__main__":
    unittest.main()
