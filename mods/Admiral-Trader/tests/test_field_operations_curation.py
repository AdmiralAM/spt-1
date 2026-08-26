import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLAN = ROOT / "manifests" / "field-operations-curation.json"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class FieldOperationsCurationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = load(PLAN)

    def test_legacy_numeric_ladders_are_not_runtime_design(self):
        principles = self.plan["principles"]
        self.assertFalse(principles["copyLegacyQuestTemplates"])
        self.assertTrue(principles["preserveThemeNotNumericLadder"])
        self.assertIsNone(principles["artificialQuestCountCap"])
        self.assertFalse(principles["genericCounterEscalationAllowed"])
        self.assertFalse(principles["sameObjectiveWithLargerNumberAllowed"])
        self.assertFalse(principles["sameObjectiveWithIncrementalDistanceAllowed"])
        self.assertFalse(principles["genericYouKnowTheDrillTextAllowed"])

    def test_required_legacy_domains_are_explicitly_reauthored(self):
        findings = {row["bundle"]: row for row in self.plan["legacyFindings"]}
        for bundle in ("Boss Hunt", "Cultists Hunt", "Sniper Life", "Survivalist"):
            self.assertIn(bundle, findings)
            self.assertEqual(findings[bundle]["decision"], "REAUTHOR")
            self.assertTrue(findings[bundle]["preserve"])
            self.assertTrue(findings[bundle]["reject"])

    def test_authored_archetypes_are_distinct_and_nonrepeatable(self):
        archetypes = self.plan["authoredArchetypes"]
        ids = [row["id"] for row in archetypes]
        self.assertEqual(len(ids), len(set(ids)))
        self.assertGreaterEqual(len(archetypes), 4)
        domains = {row["domain"] for row in archetypes}
        self.assertEqual(domains, {"Field Operations", "Special Operations"})
        for row in archetypes:
            self.assertFalse(row["repeatable"], row["id"])
            self.assertTrue(row["purpose"].strip(), row["id"])
            self.assertTrue(row["mechanicShape"], row["id"])
            self.assertTrue(row["rewardIntent"], row["id"])
            self.assertTrue(row["legacyThemes"], row["id"])

    def test_runtime_materialization_remains_fail_closed(self):
        gate = self.plan["runtimeGate"]
        self.assertFalse(gate["materializeInThisPlan"])
        requirements = gate["requirementsBeforeRuntime"]
        self.assertGreaterEqual(len(requirements), 5)
        joined = " ".join(requirements).lower()
        self.assertIn("spt 4.1.3", joined)
        self.assertIn("en/ru", joined)
        self.assertIn("economy admiral", joined)
        self.assertIn("legacy trader ids", joined)


if __name__ == "__main__":
    unittest.main()
