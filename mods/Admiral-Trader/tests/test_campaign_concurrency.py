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

    def test_weapon_campaign_has_two_entry_points_and_progressive_family_rotation(self):
        tracks = (
            (
                "59ca4829e098dfafa03888d2",
                "5f62a924076e4b7c2320f2e8",
                "2568ee0bfe2ee12f24d78f45",
                "cb8a202d7107f39d860ccb38",
            ),
            (
                "ad9233f54a7132d905d6f29d",
                "4ada822d634041a721b346d5",
                "a0d05e28971f1ba57639b97d",
            ),
        )
        previous_finals = (
            (None, "8cba3e2ec639a4aa2c26c4da", "8d8d81032315f4fdc5a06798", "7564e60e4c1c2f1b67a594a4"),
            (None, "43d9544a09d068476a1a18df", "f6e51dc4e50e47ee9af50a4d"),
        )
        for track, gates in zip(tracks, previous_finals):
            for quest_id, gate in zip(track, gates):
                expected = set() if gate is None else {gate}
                self.assertEqual(self.prerequisites(quest_id), expected, quest_id)


if __name__ == "__main__":
    unittest.main()
