import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


TOOL_PATH = Path(__file__).parents[1] / "tools" / "build_keys_runtime_templates.py"
SPEC = importlib.util.spec_from_file_location("admiral_keys_runtime", TOOL_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
SPEC.loader.exec_module(MODULE)


class KeysRuntimeTemplateTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        bundle = self.root / "Keys Proficiency" / "legacy"
        bundle.mkdir(parents=True)
        source = {
            "legacy-a": {
                "_id": "legacy-a",
                "conditions": {
                    "AvailableForFinish": [
                        {"conditionType": "FindItem", "target": ["key-a", "key-b"], "value": 2},
                        {"conditionType": "HandoverItem", "target": [MODULE.RUB_TPL], "value": 500000},
                    ]
                },
            },
            "legacy-b": {
                "_id": "legacy-b",
                "conditions": {
                    "AvailableForFinish": [
                        {"conditionType": "FindItem", "target": ["key-b", "key-c"], "value": 1}
                    ]
                },
            },
        }
        (bundle / "quests.json").write_text(json.dumps(source), encoding="utf-8")
        self.inventory = {
            "quests": [
                {"questId": "legacy-a", "sourcePath": "Keys Proficiency/legacy/quests.json"},
                {"questId": "legacy-b", "sourcePath": "Keys Proficiency/legacy/quests.json"},
            ]
        }
        self.plan = {
            "groups": [
                {"id": "keys-test", "sourceQuestIds": ["legacy-a", "legacy-b"]}
            ]
        }
        self.spec = {
            "quests": [
                {
                    "id": "0123456789abcdef01234567",
                    "slug": "keys-test",
                    "name": "Test Keys",
                    "minimumLevel": 10,
                    "map": "bigmap",
                    "prerequisites": ["aaaaaaaaaaaaaaaaaaaaaaaa"],
                    "objective": {"representativeCount": 2, "sourceGroup": "keys-test"},
                    "rewardBudget": {"xp": 8000, "rub": 25000, "standing": 0.02, "unlockSlots": 1},
                }
            ]
        }

    def tearDown(self):
        self.tmp.cleanup()

    def test_source_pool_uses_find_item_only(self):
        payload = MODULE.build_payload(self.spec, self.plan, self.inventory, self.root)
        self.assertEqual(payload["sourceKeyPools"]["keys-test"], ["key-a", "key-b", "key-c"])
        self.assertNotIn(MODULE.RUB_TPL, payload["sourceKeyPools"]["keys-test"])

    def test_native_template_keeps_keys_and_removes_money_handover(self):
        payload = MODULE.build_payload(self.spec, self.plan, self.inventory, self.root)
        template = payload["templates"]["0123456789abcdef01234567"]
        finish = template["conditions"]["AvailableForFinish"]
        self.assertEqual(len(finish), 1)
        self.assertEqual(finish[0]["conditionType"], "FindItem")
        self.assertEqual(finish[0]["value"], 2)
        self.assertFalse(finish[0]["onlyFoundInRaid"])
        self.assertEqual(template["traderId"], MODULE.TRADER_ID)
        self.assertEqual(template["type"], "PickUp")

    def test_reward_budget_materializes_without_legacy_unlock(self):
        payload = MODULE.build_payload(self.spec, self.plan, self.inventory, self.root)
        template = payload["templates"]["0123456789abcdef01234567"]
        types = [reward["type"] for reward in template["rewards"]["Success"]]
        self.assertEqual(types, ["Experience", "TraderStanding", "Item"])
        rub_reward = template["rewards"]["Success"][2]
        self.assertEqual(rub_reward["items"][0]["_tpl"], MODULE.RUB_TPL)
        self.assertEqual(rub_reward["items"][0]["upd"]["StackObjectsCount"], 25000)
        self.assertEqual(payload["summary"]["deferredUnlockSlotCount"], 1)


if __name__ == "__main__":
    unittest.main()
