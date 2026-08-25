import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "build_weapon_ammo_runtime_templates.py"
spec = importlib.util.spec_from_file_location("weapon_ammo_runtime", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


class WeaponAmmoRuntimeTemplateTests(unittest.TestCase):
    def test_elimination_condition_uses_exact_weapon_pool(self):
        condition = module.elimination_condition("x", 12, ["w2", "w1"])
        self.assertEqual(condition["type"], "Elimination")
        self.assertEqual(condition["value"], 12)
        kill = condition["counter"]["conditions"][0]
        self.assertEqual(kill["conditionType"], "Kills")
        self.assertEqual(kill["weapon"], ["w2", "w1"])
        self.assertEqual(kill["target"], "Any")

    def test_sample_reward_uses_selected_capability_tpl(self):
        stage = {"xp": 1000, "rub": 2000, "standing": 0.01, "sampleAmmoUnits": 30}
        rewards = module.success_rewards("x", stage, {"tpl": "ammo"})
        ammo = [row for row in rewards if row.get("type") == "Item" and row.get("items", [{}])[0].get("_tpl") == "ammo"]
        self.assertEqual(len(ammo), 1)
        self.assertEqual(ammo[0]["items"][0]["upd"]["StackObjectsCount"], 30)

    def test_special_sample_is_deferred_not_invented(self):
        self.assertEqual(module.success_rewards("x", {"sampleAmmoUnits": 1}, None), [])

    def test_empty_weapon_pool_is_rejected(self):
        with self.assertRaises(ValueError):
            module.elimination_condition("x", 5, [])


if __name__ == "__main__":
    unittest.main()
