import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


TOOL_PATH = Path(__file__).parents[1] / "tools" / "materialize_quest_templates.py"
SPEC = importlib.util.spec_from_file_location("admiral_quest_materializer", TOOL_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class QuestMaterializerTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        bundle = self.root / "Keys Proficiency"
        bundle.mkdir(parents=True)
        quests = {
            "keep": {
                "_id": "keep",
                "traderId": "legacy-trader",
                "conditions": {"AvailableForStart": [], "AvailableForFinish": []},
                "rewards": {
                    "Success": [
                        {"id": "stand", "type": "TraderStanding", "target": "legacy-trader", "value": 0.1},
                        {"id": "unlock", "type": "AssortmentUnlock", "target": "legacy-assort"},
                        {"id": "xp", "type": "Experience", "value": 1000},
                    ]
                },
            },
            "active": {
                "_id": "active",
                "traderId": "legacy-trader",
                "restartable": False,
                "conditions": {
                    "AvailableForStart": [{"id": "old", "conditionType": "Level", "compareMethod": ">=", "value": 1}],
                    "AvailableForFinish": [{"id": "finish", "conditionType": "HandoverItem", "target": ["item"], "value": 1}],
                },
                "rewards": {"Success": [{"id": "xp2", "type": "Experience", "value": 2500}]},
            },
        }
        (bundle / "quests.json").write_text(json.dumps(quests), encoding="utf-8")

        self.inventory = {
            "quests": [
                {
                    "questId": "keep",
                    "sourcePath": "Keys Proficiency/quests.json",
                    "curationDecision": "KEEP",
                    "prerequisites": [],
                    "restartable": False,
                },
                {
                    "questId": "active",
                    "sourcePath": "Keys Proficiency/quests.json",
                    "curationDecision": "MIGRATION_ONLY",
                    "prerequisites": [],
                    "restartable": False,
                },
            ]
        }

    def tearDown(self):
        self.tmp.cleanup()

    def test_keep_template_is_retargeted_and_legacy_unlock_is_blocked(self):
        result = MODULE.build_materialization(self.inventory, self.root)
        quest = result["curatedTemplates"]["keep"]
        self.assertEqual(quest["traderId"], MODULE.ADMIRAL_TRADER_ID)
        success = quest["rewards"]["Success"]
        self.assertEqual([reward["type"] for reward in success], ["TraderStanding", "Experience"])
        self.assertEqual(success[0]["target"], MODULE.ADMIRAL_TRADER_ID)
        self.assertEqual(result["summary"]["blockedLegacyUnlockRewardCount"], 1)
        self.assertEqual(result["summary"]["accidentalDeprecatedTemplateCount"], 0)

    def test_completion_bridge_is_profile_scoped_and_start_closed(self):
        migration_plan = {
            "retainedCompletionQuestIds": ["active"],
            "blockedLegacySuccessorIds": ["deprecated-next"],
        }
        result = MODULE.build_materialization(self.inventory, self.root, migration_plan)
        self.assertEqual(set(result["completionBridgeTemplates"]), {"active"})
        quest = result["completionBridgeTemplates"]["active"]
        self.assertEqual(quest["traderId"], MODULE.ADMIRAL_TRADER_ID)
        self.assertFalse(quest["restartable"])
        start = quest["conditions"]["AvailableForStart"]
        self.assertEqual(len(start), 1)
        self.assertEqual(start[0]["conditionType"], "Level")
        self.assertEqual(start[0]["value"], 999)
        self.assertEqual(quest["conditions"]["AvailableForFinish"][0]["id"], "finish")
        self.assertEqual(result["blockedLegacySuccessorIds"], ["deprecated-next"])

    def test_restartable_completion_bridge_is_rejected(self):
        inventory = json.loads(json.dumps(self.inventory))
        inventory["quests"][1]["restartable"] = True
        with self.assertRaises(ValueError):
            MODULE.build_materialization(
                inventory,
                self.root,
                {"retainedCompletionQuestIds": ["active"]},
            )

    def test_external_keep_prerequisite_is_reported(self):
        inventory = json.loads(json.dumps(self.inventory))
        inventory["quests"][0]["prerequisites"] = ["active"]
        result = MODULE.build_materialization(inventory, self.root)
        self.assertEqual(result["summary"]["externalCuratedPrerequisiteCount"], 1)
        self.assertEqual(
            result["diagnostics"]["externalCuratedPrerequisites"],
            [{"questId": "keep", "prerequisiteQuestId": "active"}],
        )


if __name__ == "__main__":
    unittest.main()
