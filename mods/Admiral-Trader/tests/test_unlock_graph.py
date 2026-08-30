import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
QUEST_DIR = ROOT / "db" / "quests"


def _quest_targets(condition):
    target = condition.get("target")
    if isinstance(target, str):
        return [target]
    if isinstance(target, list):
        return [value for value in target if isinstance(value, str)]
    return []


class UnlockGraphTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = {}
        for path in sorted(QUEST_DIR.glob("*.json")):
            quest = json.loads(path.read_text(encoding="utf-8"))
            quest_id = quest["_id"]
            if quest_id in cls.quests:
                raise AssertionError(f"duplicate quest id {quest_id}")
            cls.quests[quest_id] = quest

        cls.assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        cls.questassort = json.loads((ROOT / "db" / "questassort.json").read_text(encoding="utf-8"))

    def _dependencies(self):
        dependencies = {quest_id: set() for quest_id in self.quests}
        for quest_id, quest in self.quests.items():
            for condition in quest.get("conditions", {}).get("AvailableForStart", []):
                if condition.get("conditionType") != "Quest":
                    continue
                for target in _quest_targets(condition):
                    dependencies[quest_id].add(target)
        return dependencies

    def test_frozen_quest_graph_is_closed_acyclic_and_reachable(self):
        self.assertEqual(len(self.quests), 31)
        dependencies = self._dependencies()
        quest_ids = set(self.quests)

        external = {
            (quest_id, dependency)
            for quest_id, quest_dependencies in dependencies.items()
            for dependency in quest_dependencies
            if dependency not in quest_ids
        }
        self.assertEqual(external, set(), f"orphan/external quest prerequisites: {sorted(external)}")

        self_edges = {
            quest_id for quest_id, quest_dependencies in dependencies.items() if quest_id in quest_dependencies
        }
        self.assertEqual(self_edges, set(), f"self-dependent quests: {sorted(self_edges)}")

        state = {}

        def visit(quest_id, stack):
            marker = state.get(quest_id, 0)
            if marker == 1:
                cycle = stack[stack.index(quest_id):] + [quest_id]
                self.fail(f"quest dependency cycle: {' -> '.join(cycle)}")
            if marker == 2:
                return
            state[quest_id] = 1
            for dependency in dependencies[quest_id]:
                visit(dependency, stack + [quest_id])
            state[quest_id] = 2

        for quest_id in sorted(quest_ids):
            visit(quest_id, [])

        roots = {quest_id for quest_id, quest_dependencies in dependencies.items() if not quest_dependencies}
        self.assertTrue(roots, "quest graph has no root quests")

        reachable = set(roots)
        while True:
            newly_reachable = {
                quest_id
                for quest_id, quest_dependencies in dependencies.items()
                if quest_id not in reachable and quest_dependencies.issubset(reachable)
            }
            if not newly_reachable:
                break
            reachable.update(newly_reachable)

        self.assertEqual(reachable, quest_ids, f"unreachable quests: {sorted(quest_ids - reachable)}")

    def test_success_capability_unlocks_point_to_reachable_quests_and_existing_offers(self):
        dependencies = self._dependencies()
        quest_ids = set(self.quests)
        offer_ids = {item["_id"] for item in self.assort["items"]}
        success = self.questassort["success"]

        self.assertEqual(len(success), 7)
        self.assertEqual(set(success) - offer_ids, set(), "questassort.success references missing assort offers")
        self.assertEqual(set(success.values()) - quest_ids, set(), "questassort.success references missing quests")
        self.assertEqual(len(set(success.values())), 7, "multiple success offers share one capability quest")

        roots = {quest_id for quest_id, quest_dependencies in dependencies.items() if not quest_dependencies}
        reachable = set(roots)
        while True:
            newly_reachable = {
                quest_id
                for quest_id, quest_dependencies in dependencies.items()
                if quest_id not in reachable and quest_dependencies.issubset(reachable)
            }
            if not newly_reachable:
                break
            reachable.update(newly_reachable)

        locked_by_unreachable = {
            offer_id: quest_id for offer_id, quest_id in success.items() if quest_id not in reachable
        }
        self.assertEqual(locked_by_unreachable, {}, f"capability offers locked by unreachable quests: {locked_by_unreachable}")

    def test_no_started_or_fail_unlocks_exist_in_frozen_candidate(self):
        self.assertEqual(self.questassort["started"], {})
        self.assertEqual(self.questassort["fail"], {})


if __name__ == "__main__":
    unittest.main()
