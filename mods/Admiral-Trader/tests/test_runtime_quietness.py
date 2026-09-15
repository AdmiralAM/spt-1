import re
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SERVER = ROOT / "server"


class RuntimeQuietnessTests(unittest.TestCase):
    def test_server_has_no_background_or_per_frame_execution(self):
        source = "\n".join(path.read_text(encoding="utf-8") for path in SERVER.glob("*.cs"))
        forbidden = (
            r"\bTimer\b",
            r"\bTask\.Run\s*\(",
            r"\bThread\b",
            r"\bwhile\s*\(",
            r"\bfor\s*\(\s*;\s*;",
            r"\b(Update|FixedUpdate|LateUpdate)\s*\(",
        )
        for pattern in forbidden:
            self.assertIsNone(re.search(pattern, source), pattern)

    def test_profile_scoped_shop_route_is_quiet(self):
        source = (SERVER / "RelationshipStandingAssortDynamicRouter.cs").read_text(encoding="utf-8")
        self.assertNotRegex(source, r"logger\.|Console\.")
        self.assertNotRegex(source, r"File\.|Directory\.")

    def test_release_package_carries_only_runtime_manifests(self):
        builder = (ROOT / "tools" / "Build-Spt415Rc.ps1").read_text(encoding="utf-8")
        self.assertIn("'campaign-manifest.json','relationship-stock.json','runtime-manifest.json','story-campaign-runtime.json'", builder)
        self.assertNotIn("'db','manifests','assets'", builder)


if __name__ == "__main__":
    unittest.main()
