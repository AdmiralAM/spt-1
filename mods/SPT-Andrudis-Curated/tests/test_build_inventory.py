import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


TOOL_PATH = Path(__file__).parents[1] / "tools" / "build_inventory.py"
SPEC = importlib.util.spec_from_file_location("andrudis_inventory", TOOL_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class InventoryTests(unittest.TestCase):
    def test_builds_graph_and_applies_bundle_rule(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp) / "QuestBundles"
            trader = root / "Boss Hunt" / "legacy-trader"
            trader.mkdir(parents=True)
            quests = {
                "q1": {
                    "_id": "q1",
                    "QuestName": "Boss Hunt x1",
                    "traderId": "legacy-trader",
                    "conditions": {
                        "AvailableForStart": [],
                        "AvailableForFinish": [{
                            "conditionType": "CounterCreator",
                            "type": "Elimination",
                            "value": "1",
                            "counter": {"conditions": [{"conditionType": "Kills", "savageRole": ["bossKilla"]}]},
                        }],
                    },
                    "rewards": {"Success": [{"type": "Experience", "value": "250"}]},
                },
                "q2": {
                    "_id": "q2",
                    "QuestName": "Boss Hunt x2",
                    "traderId": "legacy-trader",
                    "conditions": {
                        "AvailableForStart": [{"conditionType": "Quest", "target": "q1"}],
                        "AvailableForFinish": [{"conditionType": "CounterCreator", "type": "Elimination", "value": "2"}],
                    },
                    "rewards": {"Success": []},
                },
            }
            (trader / "quests.json").write_text(json.dumps(quests), encoding="utf-8")
            rules = {
                "rules": [{
                    "match": {"bundleEquals": "Boss Hunt"},
                    "decision": "MERGE",
                    "reason": "collapse ladder",
                }]
            }

            payload = MODULE.build_payload(root, rules)
            by_id = {row["questId"]: row for row in payload["quests"]}

            self.assertEqual(payload["schemaVersion"], 2)
            self.assertEqual(payload["summary"]["questCount"], 2)
            self.assertEqual(payload["summary"]["decisionCounts"], {"MERGE": 2})
            self.assertEqual(payload["summary"]["missingPrerequisiteCount"], 0)
            self.assertEqual(payload["summary"]["cycleCount"], 0)
            self.assertEqual(by_id["q1"]["successors"], ["q2"])
            self.assertEqual(by_id["q2"]["prerequisites"], ["q1"])
            self.assertEqual(by_id["q1"]["objectives"]["killRoles"], ["bossKilla"])
            self.assertEqual(by_id["q1"]["rewards"]["experience"], 250.0)

    def test_fir_and_item_rewards_are_summarized(self):
        quest = {
            "conditions": {
                "AvailableForFinish": [{
                    "conditionType": "HandoverItem",
                    "onlyFoundInRaid": True,
                    "target": ["tpl-a"],
                    "value": "3",
                }]
            },
            "rewards": {
                "Success": [{
                    "type": "Item",
                    "items": [{"_tpl": "reward-a", "upd": {"StackObjectsCount": 5}}],
                }]
            },
        }

        objectives = MODULE.extract_objective_summary(quest)
        rewards = MODULE.extract_rewards(quest)

        self.assertTrue(objectives["firRequired"])
        self.assertEqual(objectives["maxObjectiveValue"], 3.0)
        self.assertEqual(rewards["items"], [{"tpl": "reward-a", "count": 5}])

    def test_graph_diagnostics_find_missing_cross_bundle_and_cycle(self):
        rows = [
            {
                "questId": "a",
                "bundle": "One",
                "sourcePath": "One/t/quests.json",
                "prerequisites": ["b", "missing"],
                "successors": ["b"],
            },
            {
                "questId": "b",
                "bundle": "Two",
                "sourcePath": "Two/t/quests.json",
                "prerequisites": ["a"],
                "successors": ["a"],
            },
        ]

        diagnostics = MODULE.build_graph_diagnostics(rows)

        self.assertEqual(len(diagnostics["missingPrerequisites"]), 1)
        self.assertEqual(len(diagnostics["crossBundleEdges"]), 2)
        self.assertEqual(diagnostics["cycles"], [["a", "b"]])

    def test_duplicate_ids_are_reported(self):
        rows = [
            {"questId": "same", "bundle": "One", "sourcePath": "One/a/quests.json", "prerequisites": [], "successors": []},
            {"questId": "same", "bundle": "Two", "sourcePath": "Two/b/quests.json", "prerequisites": [], "successors": []},
        ]

        diagnostics = MODULE.build_graph_diagnostics(rows)

        self.assertEqual(diagnostics["duplicateQuestIds"], [{
            "questId": "same",
            "sources": ["One/a/quests.json", "Two/b/quests.json"],
        }])

    def test_invalid_rule_decision_fails_closed(self):
        with self.assertRaises(ValueError):
            MODULE.match_rule({}, "Any", {"rules": [{"match": {}, "decision": "MAYBE"}]})


if __name__ == "__main__":
    unittest.main()
