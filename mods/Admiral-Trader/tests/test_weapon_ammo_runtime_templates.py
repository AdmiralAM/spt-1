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
        self.assertEqual(kill["weaponCaliber"], [])
        self.assertEqual(kill["target"], "Any")

    def test_readiness_condition_is_non_fir_non_consumptive_find_item(self):
        condition = module.readiness_condition("x", ["w2", "w1"])
        self.assertEqual(condition["conditionType"], "FindItem")
        self.assertEqual(condition["target"], ["w2", "w1"])
        self.assertEqual(condition["value"], 1)
        self.assertFalse(condition["onlyFoundInRaid"])

    def test_munitions_condition_adds_selected_capability_caliber(self):
        quest = {"slug": "x-munitions", "stage": "munitions"}
        stage = {"kills": 12}
        condition = module.finish_condition(quest, stage, ["w1", "w2"], {"caliber": "Caliber556x45NATO"})
        kill = condition["counter"]["conditions"][0]
        self.assertEqual(kill["weapon"], ["w1", "w2"])
        self.assertEqual(kill["weaponCaliber"], ["Caliber556x45NATO"])

    def test_three_stages_are_structurally_distinct(self):
        qualification = module.finish_condition(
            {"slug": "q", "stage": "qualification"}, {"readinessCount": 1}, ["w"], None
        )
        fieldwork = module.finish_condition(
            {"slug": "f", "stage": "fieldwork"}, {"kills": 10}, ["w"], None
        )
        munitions = module.finish_condition(
            {"slug": "m", "stage": "munitions"}, {"kills": 10}, ["w"], {"caliber": "Caliber9x19PARA"}
        )
        self.assertEqual(qualification["conditionType"], "FindItem")
        self.assertEqual(fieldwork["conditionType"], "CounterCreator")
        self.assertEqual(munitions["conditionType"], "CounterCreator")
        self.assertEqual(fieldwork["counter"]["conditions"][0]["weaponCaliber"], [])
        self.assertEqual(munitions["counter"]["conditions"][0]["weaponCaliber"], ["Caliber9x19PARA"])

    def test_sample_reward_uses_selected_capability_tpl(self):
        stage = {"xp": 1000, "rub": 2000, "standing": 0.01, "sampleAmmoUnits": 30}
        rewards = module.success_rewards("x", stage, {"tpl": "ammo"})
        ammo = [row for row in rewards if row.get("type") == "Item" and row.get("items", [{}])[0].get("_tpl") == "ammo"]
        self.assertEqual(len(ammo), 1)
        self.assertEqual(ammo[0]["items"][0]["upd"]["StackObjectsCount"], 30)

    def test_special_sample_uses_explicit_safe_tpl(self):
        rewards = module.success_rewards(
            "special-munitions",
            {"sampleAmmoUnits": 1},
            {"tpl": "6217726288ed9f0845317459", "permanentUnlock": False},
        )
        self.assertEqual(len(rewards), 1)
        self.assertEqual(rewards[0]["items"][0]["_tpl"], "6217726288ed9f0845317459")
        self.assertEqual(rewards[0]["items"][0]["upd"]["StackObjectsCount"], 1)

    def test_empty_weapon_pool_is_rejected(self):
        with self.assertRaises(ValueError):
            module.elimination_condition("x", 5, [])
        with self.assertRaises(ValueError):
            module.readiness_condition("x", [])


if __name__ == "__main__":
    unittest.main()
