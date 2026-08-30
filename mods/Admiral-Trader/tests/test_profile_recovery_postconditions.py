import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "tools" / "Reset-AdmiralTraderProfile.ps1"


class ProfileRecoveryPostconditionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.text = SCRIPT.read_text(encoding="utf-8")

    def test_recovery_verifies_all_owned_state_before_and_after_commit(self):
        for key in ("traderInfo", "questStatuses", "taskCounters", "dialogue", "traderPurchases"):
            self.assertIn(key, self.text)
        self.assertIn("Get-OwnedStateSummary", self.text)
        self.assertIn("Recovery postcondition failed before commit", self.text)
        self.assertIn("Recovery postcondition failed after commit", self.text)
        self.assertIn("$committed = Get-Content $resolvedProfile", self.text)

    def test_failure_path_restores_and_hash_verifies_backup(self):
        self.assertIn("Copy-Item $backupPath $resolvedProfile -Force", self.text)
        self.assertIn("$restoredHash = (Get-FileHash $resolvedProfile -Algorithm SHA256).Hash", self.text)
        self.assertIn("$restoredHash -ne $backupHash", self.text)

    def test_retired_offer_ids_are_covered_by_identity_validation(self):
        self.assertIn("$retiredOfferIds", self.text)
        self.assertIn("$offerIds = @($currentOfferIds + $retiredOfferIds", self.text)
        self.assertIn("$traderIds + $questIds + $offerIds", self.text)

    def test_dry_run_remains_default(self):
        self.assertIn("if (-not $Apply)", self.text)
        self.assertIn("Dry run only", self.text)


if __name__ == "__main__":
    unittest.main()
