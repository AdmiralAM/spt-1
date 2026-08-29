from pathlib import Path
import json
import unittest


ROOT = Path(__file__).resolve().parents[1]
REGISTRATION = ROOT / "server" / "ProfileTemplateRegistration.cs"
QUEST_REGISTRATION = ROOT / "server" / "QuestRegistration.cs"
SERVER = ROOT / "server"
QUESTS = ROOT / "db" / "quests"
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

    def test_profile_template_hook_never_auto_accepts_or_auto_finishes_quests(self):
        text = REGISTRATION.read_text(encoding="utf-8")
        self.assertNotIn("SetQuestsAvailableForStart", text)
        self.assertNotIn("SetQuestsAvailableForFinish", text)
        self.assertNotIn("AddAllQuestsToProfile", text)
        self.assertNotIn("QuestStatus", text)

    def test_authored_quest_templates_never_preseed_player_lifecycle(self):
        text = QUEST_REGISTRATION.read_text(encoding="utf-8")
        self.assertIn("quest.Status = 0", text)
        self.assertIn("quest.SptStatus = null", text)
        self.assertNotIn("quest.SptStatus = QuestStatusEnum.Started", text)
        self.assertIn('quest.ProgressSource ??= "eft"', text)
        self.assertIn("quest.GameModes ??= []", text)
        self.assertIn("quest.RankingModes ??= []", text)
        self.assertIn("quest.ArenaLocations ??= []", text)

    def test_all_authored_quests_require_explicit_manual_completion(self):
        quest_files = sorted(QUESTS.glob("*.json"))
        self.assertEqual(len(quest_files), 31)
        for path in quest_files:
            quest = json.loads(path.read_text(encoding="utf-8"))
            self.assertIs(quest.get("instantComplete"), False, quest.get("_id"))
            self.assertEqual(quest.get("acceptanceAndFinishingSource"), "eft", quest.get("_id"))
            finish = ((quest.get("conditions") or {}).get("AvailableForFinish") or [])
            self.assertGreater(len(finish), 0, quest.get("_id"))
            success = ((quest.get("rewards") or {}).get("Success") or [])
            self.assertGreater(len(success), 0, quest.get("_id"))

    def test_server_mod_never_forces_quest_completion_or_success(self):
        source = "\n".join(
            path.read_text(encoding="utf-8")
            for path in sorted(SERVER.glob("*.cs"))
        )
        self.assertNotIn("CompleteQuest(", source)
        self.assertNotIn("QuestStatusEnum.Success", source)
        self.assertNotIn("SetQuestsAvailableForFinish", source)

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
