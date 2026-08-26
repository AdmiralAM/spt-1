import hashlib
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRADER_ID = "d5c27bb3169f8dfbc13f6b69"
EXPECTED_ROUTE = f"/files/trader/avatar/{TRADER_ID}.jpg"
EXPECTED_RUNTIME_SHA256 = "2c78721915489107142da0d0f434e450fdec564658af979933d1503a0a114061"
EXPECTED_SOURCE_SHA256 = "48508c7370bd0c98ed368049ff89a161282279a0ffa40a705e73f23d83a28aff"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class IdentityAssetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.identity = load(ROOT / "manifests" / "identity-assets.json")
        cls.base = load(ROOT / "db" / "base.json")
        cls.asset = ROOT / "assets" / f"{TRADER_ID}.jpg"

    def test_official_portrait_is_final_and_not_substitutable(self):
        portrait = self.identity["portrait"]
        self.assertEqual(portrait["selectionStatus"], "final")
        self.assertEqual(portrait["assetStatus"], "ingested")
        self.assertFalse(portrait["substitutionAllowed"])
        self.assertFalse(portrait["placeholderAllowed"])
        self.assertEqual(portrait["sourceFileSha256"], EXPECTED_SOURCE_SHA256)

    def test_runtime_asset_exists_and_matches_locked_hash(self):
        self.assertTrue(self.asset.is_file())
        digest = hashlib.sha256(self.asset.read_bytes()).hexdigest()
        self.assertEqual(digest, EXPECTED_RUNTIME_SHA256)
        self.assertEqual(self.identity["portrait"]["runtimeSha256"], digest)
        self.assertGreater(self.asset.stat().st_size, 50_000)

    def test_base_uses_only_official_portrait_route(self):
        self.assertEqual(self.base["avatar"], EXPECTED_ROUTE)
        self.assertEqual(self.identity["portrait"]["runtimeRoute"], EXPECTED_ROUTE)
        self.assertEqual(
            self.identity["portrait"]["runtimeAsset"],
            f"assets/{TRADER_ID}.jpg",
        )


if __name__ == "__main__":
    unittest.main()
