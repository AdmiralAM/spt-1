import copy
import importlib.util
import json
from pathlib import Path
import shutil
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[3]
spec = importlib.util.spec_from_file_location("onboarding", ROOT / "mods/Economy-Admiral/tools/prepare_fresh_profile_quest.py")
onboarding = importlib.util.module_from_spec(spec)
spec.loader.exec_module(onboarding)


class FreshProfileQuestTests(unittest.TestCase):
    def test_staged_onboarding_preserves_campaign_and_unlocks_level_one(self):
        with tempfile.TemporaryDirectory() as temp:
            stage = Path(temp)
            shutil.copytree(ROOT / "mods/Admiral-Trader/db/quests", stage / "db/quests")
            before = {p.name: json.loads(p.read_text()) for p in (stage / "db/quests").glob("*.json")}
            self.assertEqual(len(before), 31)
            onboarding.prepare(stage)
            for path in (stage / "db/quests").glob("*.json"):
                actual = json.loads(path.read_text())
                expected = copy.deepcopy(before[path.name])
                if actual["_id"] == onboarding.QUEST_ID:
                    expected["conditions"]["AvailableForStart"][0]["value"] = 1
                    expected["conditions"]["AvailableForFinish"][0]["target"] = ["5449016a4bdc2d6f028b456f"]
                    expected["conditions"]["AvailableForFinish"][0]["value"] = onboarding.TEST_HANDOVER_ROUBLES
                    expected["conditions"]["AvailableForFinish"][0]["onlyFoundInRaid"] = False
                    self.assertFalse(actual["secretQuest"])
                    self.assertEqual(actual["side"], "Pmc")
                    self.assertEqual(actual["traderId"], onboarding.TRADER_ID)
                    self.assertEqual(len(actual["conditions"]["AvailableForStart"]), 1)
                    self.assertGreaterEqual(1, actual["conditions"]["AvailableForStart"][0]["value"])
                self.assertEqual(actual, expected)
            onboarding.prepare(stage)  # Safe to stage the correction twice.
            quest = json.loads(next((stage / "db/quests").glob(f"*-{onboarding.QUEST_ID}.json")).read_text())
            self.assertEqual(quest["conditions"]["AvailableForFinish"][0]["target"], ["5449016a4bdc2d6f028b456f"])
            self.assertEqual(quest["conditions"]["AvailableForFinish"][0]["value"], 1000)


if __name__ == "__main__":
    unittest.main()
