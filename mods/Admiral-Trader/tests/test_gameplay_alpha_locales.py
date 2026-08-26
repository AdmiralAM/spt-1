import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOCALE = ROOT / "db" / "locales"
MANIFESTS = ROOT / "manifests"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class GameplayAlphaLocaleTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.en = load(LOCALE / "gameplay-alpha-en.json")
        cls.ru = load(LOCALE / "gameplay-alpha-ru.json")
        cls.capabilities = load(MANIFESTS / "weapon-ammo-capabilities.json")["families"]
        cls.ammo = load(MANIFESTS / "ammo-offer-policy.json")["offers"]
        cls.questassort = load(ROOT / "db" / "questassort.json")["success"]

    def test_en_ru_override_sets_are_exact_and_nonempty(self):
        self.assertEqual(len(self.en), 59)
        self.assertEqual(set(self.en), set(self.ru))
        self.assertTrue(all(value.strip() for value in self.en.values()))
        self.assertTrue(all(value.strip() for value in self.ru.values()))

    def test_qualification_prose_no_longer_claims_combat_objective(self):
        qualification_ids = [
            "59ca4829e098dfafa03888d2",
            "ad9233f54a7132d905d6f29d",
            "5f62a924076e4b7c2320f2e8",
            "4ada822d634041a721b346d5",
            "2568ee0bfe2ee12f24d78f45",
            "a0d05e28971f1ba57639b97d",
            "cb8a202d7107f39d860ccb38",
        ]
        for quest_id in qualification_ids:
            description = self.en[f"{quest_id} description"].lower()
            self.assertIn("possess", description)
            self.assertNotIn("combat competence", description)
            self.assertIn("не изымается", self.ru[f"{quest_id} description"].lower())

    def test_permanent_munitions_payoff_names_actual_ammo_and_unlock(self):
        quest_to_family = {str(offer["questId"]): family for family, offer in self.ammo.items()}
        self.assertEqual(len(quest_to_family), 6)
        for quest_id, family in quest_to_family.items():
            capability = self.capabilities[family]
            text = self.en[f"{quest_id} description"] + " " + self.en[f"{quest_id} successMessageText"]
            short_name = capability["name"].replace("5.7x28mm ", "").replace("5.56x45mm ", "").replace("7.62x51mm ", "")
            self.assertTrue(any(token in text for token in (capability["name"], short_name)), family)
            self.assertIn("finite", text.lower())
            offer_id = next(offer_id for offer_id, gate in self.questassort.items() if gate == quest_id)
            self.assertTrue(offer_id)

    def test_special_weapons_explicitly_remains_sample_only(self):
        qid = "f1368cb3b69c3a4917c4f206"
        text = self.en[f"{qid} description"] + " " + self.en[f"{qid} successMessageText"]
        self.assertIn("RSP-30", text)
        self.assertIn("does not unlock", text)
        self.assertNotIn(qid, self.questassort.values())

    def test_labs_clearance_explicitly_describes_finite_unlock(self):
        qid = "68a6527a3c73b2e85977d7a1"
        text = self.en[f"{qid} description"] + " " + self.en[f"{qid} successMessageText"]
        self.assertIn("Laboratory access-card", text)
        self.assertIn("one card per reset", text)
        self.assertIn(qid, self.questassort.values())


if __name__ == "__main__":
    unittest.main()
