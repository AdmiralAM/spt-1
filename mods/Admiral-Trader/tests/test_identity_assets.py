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

    def test_portrait_selection_and_binary_are_final(self):
        portrait = self.identity["portrait"]
        self.assertEqual(portrait["selectionStatus"], "final")
        self.assertEqual(portrait["assetStatus"], "ingested")
        self.assertIn("white naval dress tunic", portrait["description"].lower())
        self.assertEqual(portrait["sourceConversation"], "current Admiral Trader chat")
        self.assertEqual(portrait["sourceFileSha256"], "2387fb3d6bc9b8a0ec677789959d7007f108744e72d7cf809ed945d459428cda")
        self.assertEqual(portrait["sourceDimensions"], "1365x1536")
        self.assertFalse(portrait["substitutionAllowed"])
        self.assertFalse(portrait["placeholderAllowed"])

    def test_runtime_asset_and_route_are_bound_to_trader_id(self):
        portrait = self.identity["portrait"]
        trader_id = self.identity["traderId"]
        self.assertEqual(portrait["runtimeAsset"], f"assets/{trader_id}.jpg")
        self.assertEqual(portrait["runtimeRoute"], f"/files/trader/avatar/{trader_id}.jpg")
        self.assertEqual(self.base["avatar"], portrait["runtimeRoute"])

    def test_runtime_manifest_has_no_placeholder_state(self):
        portrait = self.identity["portrait"]
        data = self.runtime["data"]
        self.assertEqual(data["avatarMode"], "official-custom-route")
        self.assertEqual(data["avatar"], portrait["runtimeRoute"])
        self.assertEqual(data["officialPortraitAssetStatus"], "ingested")
        self.assertEqual(data["officialPortraitAsset"], portrait["runtimeAsset"])
        self.assertFalse(data["finalPortraitDeferred"])


if __name__ == "__main__":
    unittest.main()
