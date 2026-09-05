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
            "w1": {"_type": "Item", "_name": "Test rifle", "_props": {"weapClass": "assaultRifle", "ammoCaliber": "Caliber545x39"}},
            "a1": {"_type": "Item", "_name": "Standard", "_props": {"Caliber": "Caliber545x39", "PenetrationPower": 25, "Damage": 55}},
            "a2": {"_type": "Item", "_name": "Controlled", "_props": {"Caliber": "Caliber545x39", "PenetrationPower": 38, "Damage": 50}},
            "a3": {"_type": "Item", "_name": "High", "_props": {"Caliber": "Caliber545x39", "PenetrationPower": 50, "Damage": 45}},
        }
        authored = {
            "targetSptVersion": "4.1.5",
            "families": [{"id": "assault-rifles", "caliberHints": ["5.45x39"]}],
        }
        pools = module.build_pools(items, authored)
        family = pools["families"]["assault-rifles"]
        self.assertEqual(family["weaponCount"], 1)
        self.assertEqual(family["ammoCount"], 3)
        self.assertEqual(family["ammoTierCounts"], {"controlled": 1, "high-end": 1, "standard": 1})

    def test_filters_nodes_carbine_leakage_and_shrapnel(self):
        items = {
            "node": {"_type": "Node", "_name": "AssaultRifle", "_props": {"weapClass": "assaultRifle", "ammoCaliber": "Caliber545x39"}},
            "rifle": {"_type": "Item", "_name": "AK test", "_props": {"weapClass": "assaultRifle", "ammoCaliber": "Caliber545x39"}},
            "sks": {"_type": "Item", "_name": "SKS test", "_props": {"weapClass": "assaultCarbine", "ammoCaliber": "Caliber762x39"}},
            "round": {"_type": "Item", "_name": "5.45 controlled", "_props": {"Caliber": "Caliber545x39", "PenetrationPower": 35, "Damage": 50}},
            "frag": {"_type": "Item", "_name": "shrapnel_F1", "_props": {"Caliber": "Caliber545x39", "PenetrationPower": 101, "Damage": 100}},
        }
        authored = {
            "targetSptVersion": "4.1.5",
            "families": [{"id": "assault-rifles", "caliberHints": ["5.45x39", "7.62x39"]}],
        }
        family = module.build_pools(items, authored)["families"]["assault-rifles"]
        self.assertEqual([row["tpl"] for row in family["weapons"]], ["rifle"])
        self.assertEqual([row["tpl"] for row in family["ammo"]], ["round"])
        self.assertEqual([row["tpl"] for row in family["excludedAmmo"]], ["frag"])

    def test_special_explosive_ammo_is_not_unlock_candidate(self):
        items = {
            "launcher": {"_type": "Item", "_name": "Launcher", "_props": {"weapClass": "grenadeLauncher", "ammoCaliber": "Caliber40x46"}},
            "grenade": {"_type": "Item", "_name": "40mm grenade", "_props": {"Caliber": "Caliber40x46", "PenetrationPower": 5, "Damage": 100, "ammoType": "grenade"}},
        }
        authored = {"targetSptVersion": "4.1.5", "families": [{"id": "special-weapons", "caliberHints": []}]}
        family = module.build_pools(items, authored)["families"]["special-weapons"]
        self.assertEqual(family["weaponCount"], 1)
        self.assertEqual(family["ammoCount"], 0)
        self.assertEqual(family["excludedAmmoCount"], 1)

    def test_rejects_stale_runtime_target(self):
        with self.assertRaises(ValueError):
            module.build_pools({}, {"targetSptVersion": "4.1.4", "families": []})

    def test_blackout_does_not_alias_366_tkm(self):
        tokens = module.caliber_tokens([".300 Blackout"])
        self.assertNotIn("Caliber366TKM", tokens)
        self.assertIn("Caliber762x35", tokens)


if __name__ == "__main__":
    unittest.main()
