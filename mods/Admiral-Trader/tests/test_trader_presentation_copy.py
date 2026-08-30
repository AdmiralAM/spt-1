from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[1]
PRESENTATION = ROOT / "server" / "TraderPresentationLocalization.cs"
REGISTRATION = ROOT / "server" / "TraderRegistration.cs"


class TraderPresentationCopyTests(unittest.TestCase):
    def test_presentation_layer_replaces_identity_only_description(self):
        text = PRESENTATION.read_text(encoding="utf-8")
        self.assertIn("TraderPresentationCopy.DescriptionRu", text)
        self.assertIn("TraderPresentationCopy.DescriptionEn", text)
        self.assertIn('$"{RuntimeIdentity.TraderId} Description"', text)
        self.assertIn("tradersTable.ContainsKey(traderId)", text)

        registration = REGISTRATION.read_text(encoding="utf-8")
        self.assertIn('lazyLoadedLocaleData[$"{traderBase.Id} Description"] = localizedName;', registration)
        self.assertGreater(text.index("OnLoadOrder.Preload + 3"), -1)

    def test_english_description_is_role_copy_not_implementation_copy(self):
        text = PRESENTATION.read_text(encoding="utf-8")
        match = re.search(r'DescriptionEn = "([^"]+)";', text)
        self.assertIsNotNone(match)
        value = match.group(1)
        self.assertGreaterEqual(len(value), 90)
        for forbidden in ("SPT", "TPL", "questassort", "runtime", "finite offer", "LL"):
            self.assertNotIn(forbidden.lower(), value.lower())
        for concept in ("logistics", "procurement", "contracts"):
            self.assertIn(concept, value.lower())

    def test_russian_description_is_real_cyrillic_role_copy(self):
        text = PRESENTATION.read_text(encoding="utf-8")
        match = re.search(r'DescriptionRu = "([^"]+)";', text)
        self.assertIsNotNone(match)
        value = match.group(1)
        self.assertGreaterEqual(len(value), 90)
        self.assertRegex(value, r"[А-Яа-яЁё]")
        for forbidden in ("SPT", "TPL", "questassort", "runtime"):
            self.assertNotIn(forbidden.lower(), value.lower())
        self.assertIn("логист", value.lower())
        self.assertIn("контракт", value.lower())

    def test_presentation_does_not_touch_quest_or_assort_data(self):
        text = PRESENTATION.read_text(encoding="utf-8")
        for forbidden in ("TraderAssort", "QuestAssort", "QuestStatus", "CompleteQuest", "TraderStanding"):
            self.assertNotIn(forbidden, text)


if __name__ == "__main__":
    unittest.main()
