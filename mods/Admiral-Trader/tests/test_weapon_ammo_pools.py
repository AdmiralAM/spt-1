import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "build_weapon_ammo_pools.py"
spec = importlib.util.spec_from_file_location("weapon_ammo_pools", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


class WeaponAmmoPoolTests(unittest.TestCase):
    def test_resolves_weapon_class_and_ammo_tiers(self):
        items = {
            "w1": {"_name": "Test rifle", "_props": {"weapClass": "assaultRifle", "ammoCaliber": "Caliber545x39"}},
            "a1": {"_name": "Standard", "_props": {"Caliber": "Caliber545x39", "PenetrationPower": 25, "Damage": 55}},
            "a2": {"_name": "Controlled", "_props": {"Caliber": "Caliber545x39", "PenetrationPower": 38, "Damage": 50}},
            "a3": {"_name": "High", "_props": {"Caliber": "Caliber545x39", "PenetrationPower": 50, "Damage": 45}},
        }
        authored = {
            "targetSptVersion": "4.1.3",
            "families": [{"id": "assault-rifles", "caliberHints": ["5.45x39"]}],
        }
        pools = module.build_pools(items, authored)
        family = pools["families"]["assault-rifles"]
        self.assertEqual(family["weaponCount"], 1)
        self.assertEqual(family["ammoCount"], 3)
        self.assertEqual(family["ammoTierCounts"], {"controlled": 1, "high-end": 1, "standard": 1})

    def test_blackout_does_not_alias_366_tkm(self):
        tokens = module.caliber_tokens([".300 Blackout"])
        self.assertNotIn("Caliber366TKM", tokens)
        self.assertIn("Caliber762x35", tokens)


if __name__ == "__main__":
    unittest.main()
