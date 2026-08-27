import importlib.util
import unittest
from pathlib import Path


TOOL_PATH = Path(__file__).parents[1] / "tools" / "build_migration_plan.py"
SPEC = importlib.util.spec_from_file_location("admiral_migration_plan", TOOL_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class MigrationPlanTests(unittest.TestCase):
    def test_active_nonrestartable_is_retained_and_successor_blocked(self):
        inventory = {
            "quests": [
                {"questId": "legacy-a", "restartable": False, "successors": ["legacy-b"]},
                {"questId": "legacy-b", "restartable": False, "successors": []},
            ]
        }
        profile = {"Quests": [{"qid": "legacy-a", "status": 2}]}

        plan = MODULE.build_plan(inventory, profile)

        self.assertEqual(plan["retainedCompletionQuestIds"], ["legacy-a"])
        self.assertEqual(plan["blockedLegacySuccessorIds"], ["legacy-b"])
        self.assertFalse(plan["directProfileWrites"])

    def test_no_profile_record_means_no_completion_template(self):
        inventory = {"quests": [{"questId": "legacy-a", "restartable": False, "successors": []}]}
        plan = MODULE.build_plan(inventory, {"Quests": []})
        self.assertEqual(plan["retainedCompletionQuestIds"], [])

    def test_restartable_active_is_excluded_by_default(self):
        inventory = {"quests": [{"questId": "legacy-a", "restartable": True, "successors": []}]}
        profile = {"Quests": [{"qid": "legacy-a", "status": 3}]}
        plan = MODULE.build_plan(inventory, profile)
        self.assertEqual(plan["retainedCompletionQuestIds"], [])
        self.assertEqual(plan["excludedRestartableQuestIds"], ["legacy-a"])

    def test_history_delayed_and_stale_states_are_not_mutated(self):
        inventory = {
            "quests": [
                {"questId": "success", "restartable": False, "successors": []},
                {"questId": "failed", "restartable": False, "successors": []},
                {"questId": "delayed", "restartable": False, "successors": []},
                {"questId": "stale", "restartable": False, "successors": []},
            ]
        }
        profile = {
            "Quests": [
                {"qid": "success", "status": 4},
                {"qid": "failed", "status": 5},
                {"qid": "delayed", "status": 9},
                {"qid": "stale", "status": 1},
            ]
        }
        plan = MODULE.build_plan(inventory, profile)
        self.assertEqual(plan["completedHistoryQuestIds"], ["success"])
        self.assertEqual(plan["failedHistoryQuestIds"], ["failed"])
        self.assertEqual(plan["delayedLegacyRecordQuestIds"], ["delayed"])
        self.assertEqual(plan["staleLegacyRecordQuestIds"], ["stale"])
        self.assertEqual(plan["retainedCompletionQuestIds"], [])

    def test_unknown_status_is_fail_closed_signal(self):
        inventory = {"quests": [{"questId": "legacy-a", "restartable": False, "successors": []}]}
        profile = {"Quests": [{"qid": "legacy-a", "status": 99}]}
        plan = MODULE.build_plan(inventory, profile)
        self.assertEqual(plan["summary"]["unknownStatusCount"], 1)
        self.assertEqual(plan["retainedCompletionQuestIds"], [])


if __name__ == "__main__":
    unittest.main()
