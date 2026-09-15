import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class CampaignConcurrencyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = {}
        for path in (ROOT / "db/quests").glob("*.json"):
            quest = json.loads(path.read_text(encoding="utf-8"))
            cls.quests[quest["_id"]] = quest

    def prerequisites(self, quest_id):
        return {
            condition["target"]
            for condition in self.quests[quest_id]["conditions"]["AvailableForStart"]
            if condition["conditionType"] == "Quest"
        }

    def test_all_forty_weapon_quests_form_exactly_two_linear_lanes(self):
        weapon_ids = {
            json.loads(path.read_text(encoding="utf-8"))["_id"]
            for pattern in ("20-*.json", "40-*.json")
            for path in (ROOT / "db/quests").glob(pattern)
        }
        self.assertEqual(len(weapon_ids), 40)
        roots = [quest_id for quest_id in weapon_ids if not (self.prerequisites(quest_id) & weapon_ids)]
        self.assertEqual(set(roots), {"738588764e9531bdb8ccfc5f", "eb93814dd020bdc131d526aa"})

        children = {quest_id: [] for quest_id in weapon_ids}
        for quest_id in weapon_ids:
            for parent in self.prerequisites(quest_id) & weapon_ids:
                children[parent].append(quest_id)
        self.assertTrue(all(len(rows) <= 1 for rows in children.values()))

        visited = set()
        for root in roots:
            current = root
            while current:
                self.assertNotIn(current, visited)
                visited.add(current)
                current = children[current][0] if children[current] else None
        self.assertEqual(visited, weapon_ids)

    def test_fresh_high_level_profile_is_not_flooded_with_admiral_roots(self):
        roots = []
        for quest_id, quest in self.quests.items():
            authored_parents = self.prerequisites(quest_id) & self.quests.keys()
            if not authored_parents:
                level = next(
                    condition["value"]
                    for condition in quest["conditions"]["AvailableForStart"]
                    if condition["conditionType"] == "Level"
                )
                if level <= 35:
                    roots.append(quest_id)
        self.assertEqual(len(roots), 8)
        weapon_roots = {
            quest_id for quest_id in roots
            if next(path for path in (ROOT / "db/quests").glob(f"*-{quest_id}.json")).name.startswith(("20-", "40-"))
        }
        self.assertEqual(len(weapon_roots), 2)


if __name__ == "__main__":
    unittest.main()
