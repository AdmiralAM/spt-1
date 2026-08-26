import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUEST_DIR = ROOT / "db" / "quests"
LOCALE_DIR = ROOT / "db" / "locales"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class RewardLegibilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [load(path) for path in sorted(QUEST_DIR.glob("*.json"))]
        cls.en = {}
        cls.ru = {}
        for name in ("en.json", "arsenal-en.json", "gameplay-alpha-en.json"):
            cls.en.update(load(LOCALE_DIR / name))
        for name in ("ru.json", "arsenal-ru.json", "gameplay-alpha-ru.json"):
            cls.ru.update(load(LOCALE_DIR / name))

    def test_every_rewarding_quest_has_visible_payoff_context(self):
        for quest in self.quests:
            qid = quest["_id"]
            rewards = (quest.get("rewards") or {}).get("Success") or []
            if not rewards:
                continue
            en = " ".join(self.en.get(f"{qid} {field}", "") for field in ("description", "successMessageText", "completePlayerMessage")).lower()
            ru = " ".join(self.ru.get(f"{qid} {field}", "") for field in ("description", "successMessageText", "completePlayerMessage")).lower()
            standing = [r for r in rewards if r.get("type") == "TraderStanding"]
            if standing:
                self.assertTrue(any(token in en for token in ("standing", "trust", "reputation", "relationship")), qid)
                self.assertTrue(any(token in ru for token in ("репутац", "довер", "отношен")), qid)

    def test_standing_target_and_amounts_are_valid(self):
        trader = "d5c27bb3169f8dfbc13f6b69"
        for quest in self.quests:
            for reward in (quest.get("rewards") or {}).get("Success") or []:
                if reward.get("type") != "TraderStanding":
                    continue
                self.assertEqual(reward.get("target"), trader, quest["_id"])
                self.assertGreater(float(reward.get("value", 0)), 0, quest["_id"])
                self.assertLessEqual(float(reward.get("value", 0)), 0.05, quest["_id"])


if __name__ == "__main__":
    unittest.main()
