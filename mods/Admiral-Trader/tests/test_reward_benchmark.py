import importlib.util
import unittest
from pathlib import Path


TOOL_PATH = Path(__file__).parents[1] / "tools" / "build_reward_benchmark.py"
SPEC = importlib.util.spec_from_file_location("admiral_reward_benchmark", TOOL_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class RewardBenchmarkTests(unittest.TestCase):
    def test_extracts_level_bucket_and_reward_distributions(self):
        raw = {
            "q1": {
                "_id": "q1",
                "type": "Elimination",
                "conditions": {
                    "AvailableForStart": [
                        {"conditionType": "Level", "compareMethod": ">=", "value": 5}
                    ]
                },
                "rewards": {
                    "Success": [
                        {"type": "Experience", "value": "1000"},
                        {"type": "TraderStanding", "value": "0.02"},
                        {
                            "type": "Item",
                            "items": [{"_tpl": "rub", "upd": {"StackObjectsCount": 15000}}],
                        },
                    ]
                },
            },
            "q2": {
                "_id": "q2",
                "type": "Completion",
                "conditions": {
                    "AvailableForStart": [
                        {"conditionType": "Level", "compareMethod": ">", "value": 20}
                    ]
                },
                "rewards": {
                    "Success": [
                        {"type": "Experience", "value": 3000},
                        {"type": "AssortmentUnlock", "target": "x"},
                    ]
                },
            },
        }

        result = MODULE.build_benchmark(raw)

        self.assertEqual(result["summary"]["questCount"], 2)
        self.assertEqual(result["summary"]["questTypes"], {"Completion": 1, "Elimination": 1})
        self.assertEqual(result["summary"]["xp"]["median"], 2000.0)
        self.assertEqual(result["levelBuckets"]["01-10"]["questCount"], 1)
        self.assertEqual(result["levelBuckets"]["21-30"]["questCount"], 1)
        self.assertEqual(result["summary"]["topRewardItemTemplates"], [{"tpl": "rub", "units": 15000.0}])
        self.assertEqual(result["summary"]["unlocks"]["max"], 1.0)

    def test_nested_quest_container_is_supported(self):
        raw = {"templates": {"quests": [{"_id": "q1", "conditions": {}, "rewards": {"Success": []}}]}}
        result = MODULE.build_benchmark(raw)
        self.assertEqual(result["summary"]["questCount"], 1)

    def test_empty_distribution_is_stable(self):
        self.assertEqual(
            MODULE.distribution([]),
            {"count": 0, "min": 0.0, "p25": 0.0, "median": 0.0, "p75": 0.0, "p90": 0.0, "max": 0.0},
        )


if __name__ == "__main__":
    unittest.main()
