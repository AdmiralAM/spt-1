import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def load_quest(quest_id):
    matches = list((ROOT / "db/quests").glob(f"*-{quest_id}.json"))
    assert len(matches) == 1
    return json.loads(matches[0].read_text(encoding="utf-8-sig"))


def counter(quest):
    rows = [row for row in quest["conditions"]["AvailableForFinish"] if row.get("conditionType") == "CounterCreator"]
    assert len(rows) == 1
    return rows[0]


def counters(quest):
    return [row for row in quest["conditions"]["AvailableForFinish"] if row.get("conditionType") == "CounterCreator"]


class AuditCandidatePolishTests(unittest.TestCase):
    def test_low_profile_is_a_real_interchange_route_in_the_same_equipment(self):
        quest = load_quest("208db81b5ce195bf0c176852")
        objective = counter(quest)
        nested = objective["counter"]["conditions"]
        self.assertTrue(objective["oneSessionOnly"])
        self.assertEqual(
            {"Equipment", "Location", "VisitPlace", "ExitStatus"},
            {row["conditionType"] for row in nested},
        )
        self.assertEqual(
            {"place_SALE_03_KOSTIN", "place_WARBLOOD_04_1"},
            {row["target"] for row in nested if row["conditionType"] == "VisitPlace"},
        )
        self.assertEqual(
            [["572b7adb24597762ae139821"], ["56e33680d2720be2748b4576"]],
            next(row["equipmentInclusive"] for row in nested if row["conditionType"] == "Equipment"),
        )

    def test_open_corridor_is_a_newcomer_viable_clearance_with_separate_extraction(self):
        objectives = counters(load_quest("3c6e085fc02f0597efdb5d5a"))
        objective = next(row for row in objectives if any(x["conditionType"] == "Kills" for x in row["counter"]["conditions"]))
        nested = objective["counter"]["conditions"]
        kills = next(row for row in nested if row["conditionType"] == "Kills")
        self.assertTrue(objective["oneSessionOnly"])
        self.assertEqual(2, objective["value"])
        self.assertEqual("Savage", kills["target"])
        self.assertEqual([], kills["weapon"])
        self.assertEqual({"value": 0, "compareMethod": ">="}, kills["distance"])
        extraction = next(row for row in objectives if any(x["conditionType"] == "ExitStatus" for x in row["counter"]["conditions"]))
        self.assertTrue(any(row.get("status") == ["Survived"] for row in extraction["counter"]["conditions"]))

    def test_exit_discipline_requires_combat_and_survived_extraction_in_one_raid(self):
        objectives = counters(load_quest("e520cec55b83621928e9e4ec"))
        objective = next(row for row in objectives if any(x["conditionType"] == "Kills" for x in row["counter"]["conditions"]))
        nested = objective["counter"]["conditions"]
        self.assertTrue(objective["oneSessionOnly"])
        self.assertEqual(5, objective["value"])
        self.assertEqual("Any", next(row for row in nested if row["conditionType"] == "Kills")["target"])
        extraction = next(row for row in objectives if any(x["conditionType"] == "ExitStatus" for x in row["counter"]["conditions"]))
        self.assertTrue(any(row.get("status") == ["Survived"] for row in extraction["counter"]["conditions"]))

    def test_retained_candidates_have_distinct_runtime_roles(self):
        acoustic = counter(load_quest("8dad0d354ac000b7bbf05b9a"))["counter"]["conditions"]
        contested = counter(load_quest("31ab6a69a8436df6b3834b0a"))["counter"]["conditions"]
        self.assertEqual(2, sum(row["conditionType"] == "VisitPlace" for row in acoustic))
        self.assertTrue(any(row["conditionType"] == "Equipment" for row in acoustic))
        self.assertEqual("AnyPmc", next(row for row in contested if row["conditionType"] == "Kills")["target"])

    def test_player_facing_objectives_state_every_new_constraint(self):
        m3 = json.loads((ROOT / "db/locales/m3-ru.json").read_text(encoding="utf-8-sig"))
        m8 = json.loads((ROOT / "db/locales/m8-ru.json").read_text(encoding="utf-8-sig"))
        self.assertIn("KOSTIN", m3["96d629538203984d6a1ee835"])
        self.assertIn("первый складской сектор", m3["96d629538203984d6a1ee835"])
        self.assertIn("без требований к дистанции", m8["0655c05e2745efd12e740c0d"])
        self.assertIn("эвакуироваться", m8["ceaae27bfcc1584b32c83936"])
        self.assertIn("5 целей", m8["a0d2f4ce6b38983415aff9be"])
        self.assertIn("эвакуироваться", m8["7710e9019fa024ee58d4adb7"])


if __name__ == "__main__":
    unittest.main()
