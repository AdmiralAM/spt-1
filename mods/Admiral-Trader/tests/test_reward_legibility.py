import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUEST_DIR = ROOT / "db" / "quests"
SERVER_DIR = ROOT / "server"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class RewardLegibilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [load(path) for path in sorted(QUEST_DIR.glob("*.json"))]
        cls.ux_source = (SERVER_DIR / "QuestUxRegistration.cs").read_text(encoding="utf-8")

    def test_runtime_ux_loader_consumes_all_gameplay_locale_layers(self):
        for name in (
            "gameplay-alpha-en.json",
            "gameplay-alpha-ru.json",
            "objectives-en.json",
            "objectives-ru.json",
        ):
            self.assertIn(name, self.ux_source)
        self.assertIn("AppendStandingContext", self.ux_source)
        self.assertIn("Admiral reputation", self.ux_source)
        self.assertIn("Репутация у Адмирала", self.ux_source)

    def test_standing_target_and_amounts_are_valid(self):
        trader = "d5c27bb3169f8dfbc13f6b69"
        count = 0
        for quest in self.quests:
            for reward in (quest.get("rewards") or {}).get("Success") or []:
                if reward.get("type") != "TraderStanding":
                    continue
                count += 1
                self.assertEqual(reward.get("target"), trader, quest["_id"])
                self.assertGreater(float(reward.get("value", 0)), 0, quest["_id"])
                self.assertLessEqual(float(reward.get("value", 0)), 0.05, quest["_id"])
        self.assertEqual(count, 31)

    def test_runtime_standing_context_is_derived_from_backend_rewards(self):
        self.assertIn('GetProperty("rewards").GetProperty("Success")', self.ux_source)
        self.assertIn('"TraderStanding"', self.ux_source)
        self.assertIn('standing > 0.05m', self.ux_source)
        self.assertIn('successMessageText', self.ux_source)
        self.assertIn('completePlayerMessage', self.ux_source)


if __name__ == "__main__":
    unittest.main()
