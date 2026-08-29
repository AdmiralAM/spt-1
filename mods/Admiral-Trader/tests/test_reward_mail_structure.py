import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUEST_DIR = ROOT / "db" / "quests"
MONGO_ID = re.compile(r"^[0-9a-f]{24}$")


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class RewardMailStructureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [load(path) for path in sorted(QUEST_DIR.glob("*.json"))]

    def test_started_and_fail_rewards_remain_empty(self):
        self.assertEqual(len(self.quests), 31)
        for quest in self.quests:
            rewards = quest.get("rewards") or {}
            self.assertEqual(rewards.get("Started") or [], [], quest["_id"])
            self.assertEqual(rewards.get("Fail") or [], [], quest["_id"])

    def test_success_reward_ids_are_valid_and_globally_unique(self):
        seen = {}
        for quest in self.quests:
            for reward in (quest.get("rewards") or {}).get("Success") or []:
                reward_id = str(reward.get("id") or "")
                self.assertRegex(reward_id, MONGO_ID, quest["_id"])
                self.assertNotIn(
                    reward_id,
                    seen,
                    f"reward id {reward_id} reused by {seen.get(reward_id)} and {quest['_id']}",
                )
                seen[reward_id] = quest["_id"]
        self.assertGreater(len(seen), 0)

    def test_item_reward_graph_has_one_root_and_no_orphans(self):
        for quest in self.quests:
            for reward in (quest.get("rewards") or {}).get("Success") or []:
                if reward.get("type") != "Item":
                    continue
                items = reward.get("items") or []
                target = str(reward.get("target") or "")
                by_id = {str(item.get("_id") or ""): item for item in items}
                self.assertEqual(len(by_id), len(items), quest["_id"])
                self.assertIn(target, by_id, quest["_id"])

                roots = []
                for item_id, item in by_id.items():
                    self.assertRegex(item_id, MONGO_ID, quest["_id"])
                    self.assertRegex(str(item.get("_tpl") or ""), MONGO_ID, quest["_id"])
                    parent_id = item.get("parentId")
                    if not parent_id:
                        roots.append(item_id)
                        continue
                    self.assertIn(str(parent_id), by_id, f"{quest['_id']}: orphan reward item {item_id}")
                    self.assertTrue(str(item.get("slotId") or "").strip(), f"{quest['_id']}: child reward item {item_id} has no slotId")

                # Single-stack rewards normally omit parentId on the target; presets may
                # contain children, but every graph must still have exactly one root and
                # that root must be the reward target delivered through SPT mail.
                self.assertEqual(roots, [target], quest["_id"])


if __name__ == "__main__":
    unittest.main()
