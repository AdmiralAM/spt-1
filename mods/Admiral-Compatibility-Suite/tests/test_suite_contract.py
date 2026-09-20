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
        self.assertGreater(result["scopeFileCounts"].get("runtime", 0), 0)
        self.assertTrue(all(row["scope"] in {"runtime", "test", "documentation", "build-tool", "other"} for row in result["findings"]))

    def test_tgc_and_packnstrap_migration_is_bound_to_reviewed_runtime_seams(self):
        data = json.loads((MODULE / "manifests/compatibility-ownership.json").read_text(encoding="utf-8"))
        rows = {row["id"]: row for row in data["components"]}
        for component in ("belt-tgc", "belt-packnstrap"):
            row = rows[component]
            self.assertEqual(row["status"], "belt-runtime-owner-api-extraction-in-progress")
            self.assertEqual(len(row["reviewedBeltHead"]), 40)
            self.assertGreater(len(row["runtimeSeams"]), 2)
            self.assertTrue(all(path.endswith(".cs") for path in row["runtimeSeams"]))
            self.assertIn("Belt", row["ownershipRule"])

    def test_use_items_anywhere_adapter_is_suite_owned_and_non_destructive(self):
        source = (MODULE / "client/UseItemsAnywhereAdapter.cs").read_text(encoding="utf-8")
        plugin = (MODULE / "client/Plugin.cs").read_text(encoding="utf-8")
        self.assertIn('UpstreamPluginGuid = "com.cj.useFromAnywhere"', source)
        self.assertIn("DedicatedBeltSlotValue = 15", source)
        self.assertIn("entry.BoxedValue = list", source)
        self.assertIn("alreadyExtended", source)
        self.assertIn("if (!list.Contains(belt))", source)
        self.assertIn("list.Add(belt)", source)
        self.assertIn("if (eligible == 0)", source)
        self.assertNotIn("File.Write", source)
        self.assertNotIn("Harmony", source)
        self.assertIn("BepInDependency(UseItemsAnywhereAdapter.UpstreamPluginGuid", plugin)

    def test_posters_use_foldables_compound_item_size_path(self):
        source = (ROOT / "mods/SPT-Foldables-Extended/client/FoldableTypes.cs").read_text(encoding="utf-8")
        self.assertIn("FoldablePosterTemplate : CompoundItemTemplate", source)
        self.assertIn("FoldablePoster : CompoundItem, IFoldable", source)

    def test_plate_drop_patch_targets_exact_item_overload(self):
        source = (ROOT / "mods/SPT-Stackable-Armor-Plates/client/Plugin.cs").read_text(encoding="utf-8")
        signature = "new[] { typeof(ItemContext), typeof(Item), typeof(bool), typeof(bool) }"
        self.assertGreaterEqual(source.count(signature), 2)
        self.assertIn("Harmony.GetPatchInfo(plateDropTarget)", source)
        self.assertIn("Armor plate item-on-item action patch was not installed", source)

    def test_ui_fixes_adapter_consumes_versioned_belt_api_and_fails_closed(self):
        source = (MODULE / "client/UiFixesBeltAdapter.cs").read_text(encoding="utf-8")
        self.assertIn('UpstreamPluginGuid = "com.tyfon.uifixes"', source)
        self.assertIn('FindUniqueType("SPTBeltArmbandInventory.BeltAccessApi")', source)
        self.assertIn("RequiredBeltContractVersion = 1", source)
        self.assertIn('"TryEnumerateBeltSources"', source)
        self.assertIn("rewrittenCalls != 1", source)
        self.assertIn("harmony?.UnpatchSelf()", source)
        self.assertNotIn("NativeBeltActions", source)
        self.assertNotIn("IntegratedBeltAccess", source)

    def test_external_claim_is_narrow_and_runs_before_belt_awake(self):
        source = (MODULE / "client/ExternalCompatibilityClaims.cs").read_text(encoding="utf-8")
        plugin = (MODULE / "client/Plugin.cs").read_text(encoding="utf-8")
        self.assertIn('"SPTBeltArmbandInventory.ExternalCompatibilityApi"', source)
        self.assertIn('"TryClaimTgc300"', source)
        self.assertIn('"TryClaimPackNStrap211"', source)
        self.assertIn("RequiredContractVersion = 1", source)
        self.assertNotIn("BepInDependency(BeltPluginGuid", plugin)
        self.assertIn("ExternalCompatibilityClaims.TryClaimClient", plugin)
        server = (MODULE / "server/ServerMod.cs").read_text(encoding="utf-8")
        self.assertIn("OnLoadOrder.Preload - 1", server)
        self.assertIn('"TryClaimTgc300"', server)
        self.assertIn('"TryClaimPackNStrap211"', server)
        self.assertIn("TgcBelts.All(templateTable.Items.ContainsKey)", server)

if __name__ == "__main__":
    unittest.main()
