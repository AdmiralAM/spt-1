import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "audit_spt415_quest_conditions.py"
spec = importlib.util.spec_from_file_location("spt415_condition_audit", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


class Spt415QuestConditionAuditTests(unittest.TestCase):
    def test_collects_nested_native_condition_shapes(self):
        quests = {
            "q1": {
                "location": "woods",
                "conditions": {
                    "AvailableForFinish": [
                        {
                            "conditionType": "CounterCreator",
                            "counter": {
                                "conditions": [
                                    {
                                        "conditionType": "Kills",
                                        "target": "AnyPmc",
                                        "savageRole": ["pmcBot"],
                                        "daytime": {"from": 6, "to": 18},
                                        "distance": {"compareMethod": ">=", "value": 80},
                                    },
                                    {"conditionType": "Location", "target": ["woods"]},
                                    {"conditionType": "ExitStatus", "status": ["Survived"]},
                                ]
                            },
                        },
                        {"conditionType": "VisitPlace", "target": "test_zone"},
                    ]
                },
            }
        }
        result = module.audit(quests)
        self.assertEqual(result["targetSptVersion"], "4.1.5")
        self.assertEqual(result["questCount"], 1)
        self.assertEqual(result["conditionTypeCounts"]["Kills"], 1)
        self.assertEqual(result["killTargets"][0]["value"], "AnyPmc")
        self.assertEqual(result["savageRoles"][0]["value"], "pmcBot")
        self.assertEqual(result["exitStatuses"][0]["value"], "Survived")
        self.assertEqual(result["visitPlaceTargets"][0]["value"], "test_zone")
        self.assertEqual(result["locationConditionTargets"][0]["value"], "woods")
        self.assertEqual(result["daytimeWindows"][0]["value"], "6->18")
        self.assertEqual(result["distanceShapes"][0]["value"], ">=:80")

    def test_rejects_non_object_database(self):
        with self.assertRaises(ValueError):
            module.audit([])


if __name__ == "__main__":
    unittest.main()
