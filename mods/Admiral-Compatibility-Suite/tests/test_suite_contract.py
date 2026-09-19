import importlib.util
import json
import unittest
from pathlib import Path

MODULE = Path(__file__).resolve().parents[1]
ROOT = MODULE.parents[1]

class SuiteContractTests(unittest.TestCase):
    def test_manifest_boundaries_and_sources(self):
        data = json.loads((MODULE / "manifests/compatibility-ownership.json").read_text(encoding="utf-8"))
        rows = {row["id"]: row for row in data["components"]}
        self.assertEqual(rows["item-intelligence-sense"]["disposition"], "retain-owner")
        self.assertEqual(rows["trader-external-consolidation"]["disposition"], "retain-owner")
        self.assertEqual(rows["foldables-extended"]["disposition"], "suite-component")
        self.assertEqual(rows["stackable-armor-plates"]["disposition"], "suite-component")
        for row in rows.values():
            source = row["sourcePath"]
            if not source.startswith("external-worktree:"):
                self.assertTrue((ROOT / source).exists(), source)

    def test_audit_finds_known_external_boundaries(self):
        spec = importlib.util.spec_from_file_location("audit", MODULE / "tools/audit_compatibility.py")
        audit = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(audit)
        result = audit.audit(ROOT)
        targets = {target for row in result["findings"] for target in row["foreignTargets"]}
        self.assertTrue({"amands-sense", "foldables", "merge-consumables", "packnstrap", "tgc", "use-items-anywhere"} <= targets)
        self.assertGreater(result["signalFileCounts"].get("harmony_patch", 0), 0)
        self.assertGreater(result["signalFileCounts"].get("reflection", 0), 0)

    def test_use_items_anywhere_adapter_is_suite_owned_and_non_destructive(self):
        source = (MODULE / "client/UseItemsAnywhereAdapter.cs").read_text(encoding="utf-8")
        plugin = (MODULE / "client/Plugin.cs").read_text(encoding="utf-8")
        self.assertIn('UpstreamPluginGuid = "com.cj.useFromAnywhere"', source)
        self.assertIn("DedicatedBeltSlotValue = 15", source)
        self.assertIn('Enum.Parse(equipmentSlot, "ArmBand", false)', source)
        self.assertIn("entry.BoxedValue = list", source)
        self.assertIn("alreadyExtended", source)
        self.assertIn("if (eligible == 0)", source)
        self.assertIn("SlotAccessSynchronizer.EnsureFollower", source)
        self.assertNotIn("File.Write", source)
        self.assertNotIn("Harmony", source)
        self.assertIn("BepInDependency(UseItemsAnywhereAdapter.UpstreamPluginGuid", plugin)

if __name__ == "__main__":
    unittest.main()
