import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class IdentityAssetContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.identity = load(ROOT / "manifests" / "identity-assets.json")
        cls.runtime = load(ROOT / "manifests" / "runtime-manifest.json")
        cls.base = load(ROOT / "db" / "base.json")

    def test_official_identity_is_locked(self):
        self.assertEqual(self.identity["product"], "Admiral Trader")
        self.assertEqual(self.identity["traderId"], "d5c27bb3169f8dfbc13f6b69")
        self.assertEqual(self.identity["officialName"], {"en": "Admiral", "ru": "Адмирал"})

    def test_portrait_selection_is_final_but_binary_is_not_substitutable(self):
        portrait = self.identity["portrait"]
        self.assertEqual(portrait["selectionStatus"], "selected")
        self.assertIn("white naval tunic", portrait["description"].lower())
        self.assertEqual(portrait["sourceConversation"], "Генерация иконок адмирала")
        self.assertFalse(portrait["substitutionAllowed"])
        self.assertFalse(portrait["placeholderIsCreativeFallback"])

    def test_target_asset_and_route_are_bound_to_trader_id(self):
        portrait = self.identity["portrait"]
        trader_id = self.identity["traderId"]
        self.assertEqual(portrait["targetAsset"], f"assets/{trader_id}.jpg")
        self.assertEqual(portrait["targetRoute"], f"/files/trader/avatar/{trader_id}.jpg")

    def test_pending_binary_handoff_keeps_source_fail_closed_on_placeholder(self):
        portrait = self.identity["portrait"]
        data = self.runtime["data"]
        self.assertEqual(portrait["assetStatus"], "awaiting-exact-binary-handoff")
        self.assertEqual(data["avatarMode"], "built-in-test-placeholder")
        self.assertEqual(self.base["avatar"], data["avatar"])
        self.assertNotEqual(data["avatar"], portrait["targetRoute"])


if __name__ == "__main__":
    unittest.main()
