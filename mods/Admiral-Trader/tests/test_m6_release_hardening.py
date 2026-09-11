import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
REPO = ROOT.parents[1]


class M6ReleaseHardeningTests(unittest.TestCase):
    def test_release_metadata_and_scope_are_aligned(self):
        runtime = json.loads((ROOT / "manifests/runtime-manifest.json").read_text())
        m6 = json.loads((ROOT / "manifests/m6-stable-release.json").read_text())
        self.assertEqual((runtime["version"], runtime["sptCompatibility"]), ("0.1.0+milestones", "~4.1.0"))
        self.assertEqual(runtime["schemaVersion"], 2)
        self.assertFalse(runtime["registrationEnabled"])
        scope = m6["scopeFreeze"]
        self.assertEqual((scope["quests"], scope["offers"]), (43, 15))
        self.assertEqual(
            (scope["newQuests"], scope["newOffers"], scope["newMechanics"], scope["newDependencies"]),
            (0, 0, 0, 0),
        )
        self.assertFalse(m6["stableClaimAllowed"])

    def test_runtime_sources_do_not_write_profiles_and_have_rollback(self):
        sources = "\n".join(path.read_text(encoding="utf-8") for path in (ROOT / "server").glob("*.cs"))
        for forbidden in ("user/profiles", "SetPmcProfile", "SaveProfile", "ProfileStore"):
            self.assertNotIn(forbidden, sources)
        self.assertIn("tradersTable.Remove(traderBase.Id)", sources)
        self.assertIn("templateTable.Quests.Remove(questId)", sources)

    def test_install_and_build_contract_cover_aliases_and_inventory(self):
        install = (ROOT / "docs/INSTALL.md").read_text(encoding="utf-8")
        lifecycle = (ROOT / "tools/Test-StablePackageLifecycle.ps1").read_text(encoding="utf-8")
        builder = (ROOT / "tools/Build-Spt415Rc.ps1").read_text(encoding="utf-8")
        for alias in ("Admiral Trader", "Admiral-Trader"):
            self.assertIn(alias, install)
            self.assertIn(alias, lifecycle)
        self.assertIn("admiral-trader-package-files.json", builder)
        self.assertIn("stable-release-candidate", builder)
        self.assertIn("removeInvalidTradersFromProfile", install)
        self.assertIn("Leave `removeModItemsFromProfile` unchanged", install)
        self.assertIn("d5c27bb3169f8dfbc13f6b69", install)

    def test_combined_candidate_uses_active_campaign(self):
        builder = (REPO / "mods/Economy-Admiral/tools/Build-CombinedSpt415Rc.ps1").read_text(encoding="utf-8")
        workflow = (REPO / ".github/workflows/admiral-economy-combined-spt415-rc.yml").read_text(encoding="utf-8")
        self.assertNotIn("TraderWorktree", builder + workflow)
        self.assertNotIn("frozen-trader", builder + workflow)
        self.assertIn("$quests.Count -ne 43", builder)
        self.assertIn("Count -ne 15", builder)
        self.assertIn("'mods/Admiral-Trader/**'", workflow)


if __name__ == "__main__":
    unittest.main()
