import json
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
REPO_ROOT = ROOT.parents[1]
TARGET = "4.1.5"
STALE_TARGET = "4.1.4"


class RuntimeTargetAuthorityTests(unittest.TestCase):
    def load_json(self, relative: str):
        return json.loads((ROOT / relative).read_text(encoding="utf-8"))

    def test_runtime_manifest_and_csproj_share_canonical_target(self):
        runtime = self.load_json("manifests/runtime-manifest.json")
        self.assertEqual(runtime["targetSptVersion"], TARGET)
        self.assertEqual(runtime["publishedApiCompileBaseline"], TARGET)

        root = ET.parse(ROOT / "server/AdmiralTrader.Server.csproj").getroot()
        props = {
            child.tag: (child.text or "").strip()
            for group in root.findall("PropertyGroup")
            for child in group
        }
        self.assertEqual(props["SptRuntimeTarget"], TARGET)
        self.assertEqual(props["SptPublishedApiBaseline"], TARGET)

    def test_active_runtime_manifests_share_canonical_target(self):
        paths = (
            "manifests/ammo-offer-policy.json",
            "manifests/weapon-ammo-authored-spec.json",
            "manifests/weapon-ammo-runtime-plan.json",
            "manifests/weapon-ammo-capabilities.json",
            "manifests/weapon-ammo-selection-policy.json",
            "manifests/weapon-family-runtime-pools.json",
        )
        for path in paths:
            with self.subTest(path=path):
                self.assertEqual(self.load_json(path)["targetSptVersion"], TARGET)

    def test_migration_policy_requires_exact_415_boundary(self):
        campaign = self.load_json("manifests/campaign-manifest.json")
        migration = json.dumps(campaign["migrationPolicy"], sort_keys=True)
        self.assertIn("4.1.5", migration)
        self.assertNotIn(STALE_TARGET, migration)

    def test_active_runtime_tools_do_not_hardcode_stale_target(self):
        paths = (
            "tools/validate_runtime_assort.py",
            "tools/validate_weapon_ammo_authored_spec.py",
            "tools/build_weapon_ammo_pools.py",
            "tools/select_weapon_ammo_rewards.py",
            "tools/build_weapon_ammo_runtime_templates.py",
        )
        for path in paths:
            text = (ROOT / path).read_text(encoding="utf-8")
            with self.subTest(path=path):
                self.assertIn(TARGET, text)
                self.assertNotIn(STALE_TARGET, text)

    def test_active_trader_workflows_do_not_validate_stale_target(self):
        paths = (
            REPO_ROOT / ".github/workflows/admiral-trader-validate.yml",
            REPO_ROOT / ".github/workflows/admiral-trader-weapon-ammo-pools.yml",
        )
        for path in paths:
            text = path.read_text(encoding="utf-8")
            with self.subTest(path=str(path)):
                self.assertIn(TARGET, text)
                self.assertNotIn(STALE_TARGET, text)


if __name__ == "__main__":
    unittest.main()
