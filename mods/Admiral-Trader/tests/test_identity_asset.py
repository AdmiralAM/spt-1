import hashlib
import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRADER_ID = "d5c27bb3169f8dfbc13f6b69"
EXPECTED_ROUTE = f"/files/trader/avatar/{TRADER_ID}.jpg"
EXPECTED_RUNTIME_GIT_BLOB_SHA1 = "0cd9db6776b246c08eb9ae0f1ac3e79c2a486966"
EXPECTED_SOURCE_SHA256 = "48508c7370bd0c98ed368049ff89a161282279a0ffa40a705e73f23d83a28aff"
EXPECTED_DIMENSIONS = (512, 576)


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def git_blob_sha1(data: bytes):
    header = f"blob {len(data)}\0".encode("ascii")
    return hashlib.sha1(header + data).hexdigest()


def jpeg_dimensions(data: bytes):
    if len(data) < 4 or data[:2] != b"\xff\xd8" or data[-2:] != b"\xff\xd9":
        raise AssertionError("portrait is not a complete JPEG stream")
    offset = 2
    sof_markers = {0xC0, 0xC1, 0xC2, 0xC3, 0xC5, 0xC6, 0xC7, 0xC9, 0xCA, 0xCB, 0xCD, 0xCE, 0xCF}
    while offset + 4 <= len(data):
        if data[offset] != 0xFF:
            offset += 1
            continue
        while offset < len(data) and data[offset] == 0xFF:
            offset += 1
        if offset >= len(data):
            break
        marker = data[offset]
        offset += 1
        if marker in (0xD8, 0xD9) or 0xD0 <= marker <= 0xD7:
            continue
        if offset + 2 > len(data):
            break
        length = int.from_bytes(data[offset:offset + 2], "big")
        if length < 2 or offset + length > len(data):
            raise AssertionError("portrait JPEG contains an invalid marker length")
        if marker in sof_markers:
            if length < 7:
                raise AssertionError("portrait JPEG SOF marker is truncated")
            height = int.from_bytes(data[offset + 3:offset + 5], "big")
            width = int.from_bytes(data[offset + 5:offset + 7], "big")
            return width, height
        offset += length
    raise AssertionError("portrait JPEG has no SOF dimensions")


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
        self.assertEqual(portrait["sourceDimensions"], "1182x1330")

    def test_runtime_asset_exists_and_matches_locked_blob_and_geometry(self):
        self.assertTrue(self.asset.is_file())
        data = self.asset.read_bytes()
        blob_oid = git_blob_sha1(data)
        self.assertEqual(blob_oid, EXPECTED_RUNTIME_GIT_BLOB_SHA1)
        self.assertEqual(self.identity["portrait"]["runtimeGitBlobSha1"], blob_oid)
        self.assertGreater(len(data), 10_000)
        self.assertEqual(jpeg_dimensions(data), EXPECTED_DIMENSIONS)
        self.assertEqual(self.identity["portrait"]["runtimeDimensions"], "512x576")

    def test_base_uses_only_official_portrait_route(self):
        self.assertEqual(self.base["avatar"], EXPECTED_ROUTE)
        self.assertEqual(self.identity["portrait"]["runtimeRoute"], EXPECTED_ROUTE)
        self.assertEqual(self.identity["portrait"]["runtimeAsset"], f"assets/{TRADER_ID}.jpg")


if __name__ == "__main__":
    unittest.main()
