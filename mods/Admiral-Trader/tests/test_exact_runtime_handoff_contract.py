from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "tools" / "package_spt413_exact_candidate.ps1"
BUILDER = ROOT / "tools" / "build_spt413_test_candidate.ps1"
RECOVERY = ROOT / "tools" / "Reset-AdmiralTraderProfile.ps1"
PORTRAIT = "assets/d5c27bb3169f8dfbc13f6b69.jpg"


class ExactRuntimeHandoffContractTests(unittest.TestCase):
    def test_wrapper_exists_and_calls_exact_runtime_builder(self):
        text = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("build_spt413_test_candidate.ps1", text)
        self.assertIn("-ExpectedHeadSha", text)
        self.assertIn("exact-installed-runtime", text)

    def test_wrapper_rejects_published_api_provenance(self):
        text = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("publishedApiCoreSha256", text)
        self.assertIn("Refusing to package non-exact candidate", text)
        self.assertIn("physicalRuntimeEvidenceEligible", text)
        self.assertIn("runtimeCoreSha256", text)

    def test_wrapper_revalidates_runtime_fatal_questassort_contract(self):
        text = SCRIPT.read_text(encoding="utf-8")
        for key in ("started", "success", "fail"):
            self.assertIn(key, text)
        self.assertIn("exactly seven success unlock mappings", text)

    def test_exact_runtime_handoff_requires_full_40_character_head_sha(self):
        for path in (SCRIPT, BUILDER):
            text = path.read_text(encoding="utf-8")
            self.assertIn("^[0-9a-f]{40}$", text, path.name)
            self.assertNotIn("^[0-9a-f]{7,40}$", text, path.name)
            self.assertNotIn("StartsWith($expected", text, path.name)
            self.assertIn("$sourceHead -ne $expected", text, path.name)

    def test_builder_is_staging_only_and_copies_official_assets(self):
        text = BUILDER.read_text(encoding="utf-8")
        self.assertNotIn("[switch]$Install", text)
        self.assertNotIn("if ($Install) {", text)
        self.assertNotIn("Remove-Item $destination -Recurse -Force", text)
        self.assertIn("Copy-Item (Join-Path $moduleRoot 'assets')", text)
        self.assertIn("runtimeGitBlobSha1", text)
        self.assertIn("Official portrait hash drift", text)
        self.assertIn("Official portrait runtime asset is not a complete JPEG stream", text)
        self.assertIn("Staging-only builder completed", text)

    def test_wrapper_requires_official_portrait_in_stage_archive_and_install(self):
        text = SCRIPT.read_text(encoding="utf-8")
        self.assertIn("identity-assets.json", text)
        self.assertIn("officialPortraitSha256", text)
        self.assertIn("officialPortraitGitBlobSha1", text)
        self.assertIn("officialPortraitRoute", text)
        self.assertIn("Staged official portrait provenance drift", text)
        self.assertIn("Staged official portrait is not a complete JPEG stream", text)
        self.assertIn("Prepared install portrait hash drift", text)
        self.assertIn('"SPT_Runtime/user/mods/Admiral-Trader/$portraitRelative"', text)

    def test_wrapper_packages_backup_first_profile_recovery(self):
        self.assertTrue(RECOVERY.is_file())
        text = SCRIPT.read_text(encoding="utf-8")
        recovery = RECOVERY.read_text(encoding="utf-8")
        self.assertIn("Reset-AdmiralTraderProfile.ps1", text)
        self.assertIn("tools\\Reset-AdmiralTraderProfile.ps1", text)
        self.assertIn("SPT_Runtime/user/mods/Admiral-Trader/tools/Reset-AdmiralTraderProfile.ps1", text)
        self.assertIn("Copy-Item $recoverySource $recoveryPath -Force", text)
        self.assertIn("manifests\\persistent-identities.json", recovery)
        self.assertIn("$ledger.retired.traderIds", recovery)
        self.assertIn("$ledger.retired.questIds", recovery)
        self.assertIn("Copy-Item $resolvedProfile $backupPath -Force", recovery)
        self.assertIn("Backup verification failed; profile was not modified.", recovery)
        backup = recovery.index("Copy-Item $resolvedProfile $backupPath -Force")
        mutation = recovery.index("foreach ($traderId in $ownedTraderInfo)")
        self.assertLess(backup, mutation)

    def test_wrapper_emits_single_obvious_exact_head_zip_and_checksum(self):
        text = SCRIPT.read_text(encoding="utf-8")
        self.assertIn('Admiral-Trader-SPT413-$sourceHead.zip', text)
        self.assertIn("Compress-Archive", text)
        self.assertIn("SPT_Runtime/user/mods/Admiral-Trader", text)
        self.assertIn("SHA-256", text)

    def test_wrapper_installs_only_after_final_archive_validation(self):
        text = SCRIPT.read_text(encoding="utf-8")
        builder_call = "& $builder -SptRoot $SptRoot -ExpectedHeadSha $expected"
        self.assertIn(builder_call, text)
        self.assertNotIn("& $builder -SptRoot $SptRoot -ExpectedHeadSha $expected -Install", text)
        checksum = text.index("$artifactSha256 = (Get-FileHash $artifactPath -Algorithm SHA256)")
        install = text.index("if ($Install) {", checksum)
        archive_validation = text.index("Exact-runtime archive contains forbidden build/debug junk.")
        self.assertGreater(install, archive_validation)
        self.assertGreater(install, checksum)
        self.assertIn("Installed fully validated exact-runtime test candidate", text[install:])

    def test_final_install_is_prepared_then_rollback_safe_swapped(self):
        text = SCRIPT.read_text(encoding="utf-8")
        install = text.index("if ($Install) {")
        block = text[install:]
        self.assertIn(".Admiral-Trader.incoming", block)
        self.assertIn(".Admiral-Trader.rollback", block)
        self.assertIn("Copy-Item $stageMod $incoming -Recurse", block)
        self.assertIn("Prepared install tree is incomplete", block)
        self.assertIn("Move-Item $destination $backup", block)
        self.assertIn("Move-Item $incoming $destination", block)
        self.assertIn("Move-Item $backup $destination -ErrorAction SilentlyContinue", block)
        copy_incoming = block.index("Copy-Item $stageMod $incoming -Recurse")
        backup_existing = block.index("Move-Item $destination $backup")
        activate_incoming = block.index("Move-Item $incoming $destination")
        rollback = block.index("Move-Item $backup $destination -ErrorAction SilentlyContinue")
        self.assertLess(copy_incoming, backup_existing)
        self.assertLess(backup_existing, activate_incoming)
        self.assertGreater(rollback, activate_incoming)


if __name__ == "__main__":
    unittest.main()
