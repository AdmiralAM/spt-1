import json
from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"
TOOLS = ROOT / "tools"
MANIFEST = ROOT / "manifests" / "runtime-manifest.json"


class RuntimeReleaseContractTests(unittest.TestCase):
    def test_source_manifest_is_exact_413_and_fail_closed(self):
        manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
        self.assertEqual(manifest["schemaVersion"], 3)
        self.assertEqual(manifest["product"], "Admiral Trader")
        self.assertEqual(manifest["targetSptVersion"], "4.1.3")
        self.assertEqual(manifest["publicationMode"], "test-candidate-source")
        self.assertFalse(manifest["registrationEnabled"])
        self.assertTrue(manifest["mergeRequiresRuntimeEvidence"])

    def test_mod_metadata_pins_exact_413(self):
        text = (SERVER / "ModMetadata.cs").read_text(encoding="utf-8")
        self.assertIn('SemanticVersioning.Range SptVersion { get; init; } = new("4.1.3");', text)
        self.assertNotIn('new("~4.1.0")', text)

    def test_exact_project_requires_all_runtime_references(self):
        text = (SERVER / "AdmiralTrader.Server.csproj").read_text(encoding="utf-8")
        required = (
            "SPTarkov.Server.Core.dll",
            "SPTarkov.Common.dll",
            "SPTarkov.DI.dll",
            "SemanticVersioning.dll",
            "JetBrains.Annotations.dll",
        )
        for assembly in required:
            self.assertIn(assembly, text)
            self.assertIn(f"!Exists('$(SptRuntimeLibDir)\\{assembly}')", text)

    def test_builder_records_all_five_runtime_assemblies(self):
        text = (TOOLS / "build_spt413_test_candidate.ps1").read_text(encoding="utf-8")
        required = (
            "SPTarkov.Server.Core.dll",
            "SPTarkov.Common.dll",
            "SPTarkov.DI.dll",
            "SemanticVersioning.dll",
            "JetBrains.Annotations.dll",
        )
        for assembly in required:
            self.assertIn(f"'{assembly}'", text)
        self.assertIn("runtimeAssemblies = $runtimeAssemblies", text)
        self.assertIn("assemblyVersion = $assemblyName.Version.ToString()", text)
        self.assertIn("sha256 = (Get-FileHash $path -Algorithm SHA256)", text)
        self.assertIn("schemaVersion = 5", text)

    def test_runtime_manifest_loader_enforces_publication_contract(self):
        text = (SERVER / "TraderRegistration.cs").read_text(encoding="utf-8")
        self.assertIn("manifest.SchemaVersion != 3", text)
        self.assertIn('manifest.Product, "Admiral Trader"', text)
        self.assertIn('manifest.TargetSptVersion, "4.1.3"', text)
        self.assertIn('manifest.RegistrationEnabled ? "test-candidate" : "test-candidate-source"', text)

    def test_runtime_tpl_gate_covers_handover_and_find_item(self):
        text = (SERVER / "RuntimeDataValidation.cs").read_text(encoding="utf-8")
        self.assertIn('string.Equals(type, "FindItem"', text)
        self.assertIn('string.Equals(type, "HandoverItem"', text)

    def test_trader_registration_preflights_before_mutation(self):
        text = (SERVER / "TraderRegistration.cs").read_text(encoding="utf-8")
        preflight = text.index("PreflightRegistrationSurfaces(traderBase.Id)")
        avatar = text.index("RegisterAvatarRoute(modPath, traderBase)")
        update_time = text.index("traderConfig.UpdateTime.Add")
        ragfair = text.index("ragfairConfig.Traders.TryAdd")
        trader = text.index("tradersTable.TryAdd")
        self.assertLess(preflight, avatar)
        self.assertLess(preflight, update_time)
        self.assertLess(preflight, ragfair)
        self.assertLess(preflight, trader)
        self.assertIn("tradersTable.ContainsKey(traderId)", text)
        self.assertIn("traderConfig.UpdateTime.Any", text)
        self.assertIn("ragfairConfig.Traders.ContainsKey(traderId)", text)

    def test_admiral_insurance_is_fail_closed_after_mod_loading(self):
        text = (SERVER / "TraderRegistration.cs").read_text(encoding="utf-8")
        self.assertIn("class AdmiralInsurancePublicationGuard", text)
        self.assertIn("OnLoadOrder.PostLoad + 100_000", text)
        self.assertIn("insurance.Availability = false", text)
        self.assertIn("level.InsurancePriceCoefficient = 0", text)
        self.assertIn("must not publish an insurance service", text)


if __name__ == "__main__":
    unittest.main()
