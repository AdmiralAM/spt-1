import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "select_weapon_ammo_rewards.py"
spec = importlib.util.spec_from_file_location("weapon_ammo_selection", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


class WeaponAmmoSelectionTests(unittest.TestCase):
    def test_prefers_highest_controlled_penetration_inside_ceiling(self):
        ammo = [
            {"tpl":"a","name":"A","tier":"controlled","penetration":34,"damage":50},
            {"tpl":"b","name":"B","tier":"controlled","penetration":38,"damage":44},
            {"tpl":"c","name":"C","tier":"controlled","penetration":40,"damage":60},
            {"tpl":"d","name":"D","tier":"high-end","penetration":44,"damage":70},
        ]
        self.assertEqual(module.choose_candidate(ammo, 38)["tpl"], "b")

    def test_tie_prefers_damage_then_tpl(self):
        ammo = [
            {"tpl":"b","name":"B","tier":"controlled","penetration":36,"damage":55},
            {"tpl":"a","name":"A","tier":"controlled","penetration":36,"damage":55},
            {"tpl":"c","name":"C","tier":"controlled","penetration":36,"damage":60},
        ]
        self.assertEqual(module.choose_candidate(ammo, 36)["tpl"], "c")

    def test_special_weapons_never_get_permanent_unlock(self):
        pools = {"targetSptVersion":"4.1.4","families":{"special-weapons":{"ammo":[]}}}
        policy = {
            "targetSptVersion":"4.1.4",
            "globalRules":{"permanentUnlockFamilies":0},
            "families":{"special-weapons":{"maxPermanentPenetration":None,"sampleUnits":1,"stockPerReset":0,"buyRestriction":0,"permanentUnlock":False}},
        }
        result = module.build_selection(pools, policy)
        self.assertFalse(result["families"]["special-weapons"]["permanentUnlock"])
        self.assertEqual(result["families"]["special-weapons"]["sampleUnits"], 1)

    def test_rejects_non_413_inputs(self):
        with self.assertRaises(ValueError):
            module.build_selection({"targetSptVersion":"4.1.2","families":{}}, {"targetSptVersion":"4.1.4","globalRules":{"permanentUnlockFamilies":0},"families":{}})


if __name__ == "__main__":
    unittest.main()
