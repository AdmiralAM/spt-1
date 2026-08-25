import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "build_weapon_ammo_audit.py"
spec = importlib.util.spec_from_file_location("weapon_ammo_audit", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


class WeaponAmmoAuditTests(unittest.TestCase):
    def test_combines_weapon_and_ammo_and_reports_cross_edge(self):
        inventory = {
            "quests": [
                {
                    "questId": "weapon-1",
                    "questName": "Weapon",
                    "bundle": "Weapon Proficiency",
                    "legacyTraderId": "trader-a",
                    "questType": "PickUp",
                    "prerequisites": [],
                    "objectives": {
                        "conditionTypes": {"HandoverItem": 1},
                        "targets": ["weapon-tpl"],
                        "firRequired": True,
                        "maxObjectiveValue": 20,
                    },
                    "rewards": {
                        "unlockCount": 4,
                        "types": {"AssortmentUnlock": 4},
                        "items": [],
                    },
                },
                {
                    "questId": "ammo-1",
                    "questName": "Ammo",
                    "bundle": "Ammo Proficiency",
                    "legacyTraderId": "trader-b",
                    "questType": "Elimination",
                    "prerequisites": ["weapon-1"],
                    "objectives": {
                        "conditionTypes": {"CounterCreator": 1},
                        "targets": ["ammo-tpl"],
                        "firRequired": False,
                        "maxObjectiveValue": 5,
                    },
                    "rewards": {
                        "unlockCount": 1,
                        "types": {"AssortmentUnlock": 1},
                        "items": [{"tpl": "ammo-sample", "count": 30}],
                    },
                },
                {"questId": "other", "bundle": "Keys Proficiency", "prerequisites": []},
            ]
        }
        audit = module.build_audit(inventory)
        summary = audit["summary"]
        self.assertEqual(summary["questCount"], 2)
        self.assertEqual(summary["crossBundleEdgeCount"], 1)
        self.assertEqual(summary["firQuestCount"], 1)
        self.assertEqual(summary["totalAssortmentUnlocks"], 5)
        self.assertEqual(summary["highUnlockQuestCount"], 1)
        self.assertEqual(summary["largeObjectiveQuestCount"], 1)


if __name__ == "__main__":
    unittest.main()
