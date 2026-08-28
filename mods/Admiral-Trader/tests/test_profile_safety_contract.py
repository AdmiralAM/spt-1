from pathlib import Path
import unittest


ROOT = Path(__file__).resolve().parents[1]
REGISTRATION = ROOT / "server" / "ProfileTemplateRegistration.cs"
RECOVERY = ROOT / "tools" / "Reset-AdmiralTraderProfile.ps1"
TRADER_ID = "d5c27bb3169f8dfbc13f6b69"


class ProfileSafetyContractTests(unittest.TestCase):
    def test_profile_template_registration_is_fail_closed(self):
        text = REGISTRATION.read_text(encoding="utf-8")
        self.assertIn("AdmiralTraderRegistration.LoadRuntimeManifest", text)
        self.assertIn("if (!runtimeManifest.RegistrationEnabled)", text)
        self.assertIn("profile-template publication gate is disabled", text)
        gate = text.index("if (!runtimeManifest.RegistrationEnabled)")
        mutation = text.index("side.Trader.InitialStanding[RuntimeIdentity.TraderId] = 0d")
        self.assertLess(gate, mutation)

    def test_new_profile_starting_standing_is_pinned_to_zero_for_both_sides(self):
        text = REGISTRATION.read_text(encoding="utf-8")
        self.assertIn("PinStartingStanding(profileTemplate.Usec)", text)
        self.assertIn("PinStartingStanding(profileTemplate.Bear)", text)
        self.assertIn("InitialStanding ??= new Dictionary<string, double?>()", text)
        self.assertIn("InitialStanding[RuntimeIdentity.TraderId] = 0d", text)

    def test_recovery_is_dry_run_by_default_and_backup_first(self):
        text = RECOVERY.read_text(encoding="utf-8")
        self.assertIn("[switch]$Apply", text)
        self.assertIn("if (-not $Apply)", text)
        self.assertIn("Dry run only", text)
        self.assertIn("Copy-Item $resolvedProfile $backupPath -Force", text)
        self.assertIn("Get-FileHash $resolvedProfile -Algorithm SHA256", text)
        self.assertIn("Get-FileHash $backupPath -Algorithm SHA256", text)
        self.assertIn("Backup verification failed; profile was not modified.", text)
        backup = text.index("Copy-Item $resolvedProfile $backupPath -Force")
        mutation = text.index("foreach ($traderId in $ownedTraderInfo)")
        self.assertLess(backup, mutation)

    def test_recovery_scope_comes_from_immutable_identity_ledger(self):
        text = RECOVERY.read_text(encoding="utf-8")
        self.assertIn(f"$frozenTraderId = '{TRADER_ID}'", text)
        self.assertIn("manifests\\persistent-identities.json", text)
        self.assertIn("Expected exactly 31 current Admiral quest IDs", text)
        self.assertIn("Expected exactly 11 current Admiral offer IDs", text)
        self.assertIn("$ledger.retired.traderIds", text)
        self.assertIn("$ledger.retired.questIds", text)
        self.assertIn("$currentTraderIds + $retiredTraderIds", text)
        self.assertIn("$currentQuestIds + $retiredQuestIds", text)
        self.assertIn("$questSet.Contains([string]$_.qid)", text)
        self.assertIn("$questSet.Contains([string]$property.Value.sourceId)", text)
        self.assertNotIn("$pmc.Quests = @()", text)
        self.assertNotIn("$pmc.TradersInfo =", text)
        self.assertNotIn("$profile.dialogues =", text)


if __name__ == "__main__":
    unittest.main()
