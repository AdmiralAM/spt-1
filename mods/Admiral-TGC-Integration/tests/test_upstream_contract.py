import importlib.util
import json
import tempfile
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SPEC_LOADER = importlib.util.spec_from_file_location("audit_upstream", ROOT / "tools/audit_upstream.py")
MODULE = importlib.util.module_from_spec(SPEC_LOADER)
assert SPEC_LOADER.loader
SPEC_LOADER.loader.exec_module(MODULE)


class UpstreamContractTests(unittest.TestCase):
    def test_contract_assigns_each_mutation_domain_once(self):
        spec = json.loads((ROOT / "manifests/upstream-tgc-3.0.0.json").read_text(encoding="utf-8"))
        self.assertEqual(spec["targetTraderId"], "d5c27bb3169f8dfbc13f6b69")
        self.assertEqual(
            set(spec["ownership"].values()),
            {"Admiral Trader", "B&A&HB", "Admiral TGC Integration"},
        )
        self.assertIn("TGC-NG.dll", spec["forbiddenFinalRuntime"])
        self.assertIn("Painter-4.0.dll", spec["forbiddenFinalRuntime"])

    def test_audit_fails_closed_when_source_set_is_incomplete(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaisesRegex(ValueError, "missing required upstream files"):
                MODULE.audit(Path(directory))

    def test_server_source_has_no_painter_or_belt_mutation(self):
        source = (ROOT / "server/TgcContentIntegration.cs").read_text(encoding="utf-8")
        self.assertNotIn("668aaff35fd574b6dcc4a686", source)
        self.assertNotIn("AddTraderAssort", source)
        self.assertNotIn("AddTraderSuits", source)
        self.assertNotIn("AddToArmband", source)
        self.assertNotIn("AddToSecureContainer", source)
        self.assertNotIn("< item.Capacity", source)
        self.assertIn("Painter and Belt/secure-container mutation disabled", source)


if __name__ == "__main__":
    unittest.main()
