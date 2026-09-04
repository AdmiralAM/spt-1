import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "validate_weapon_ammo_authored_spec.py"
spec = importlib.util.spec_from_file_location("weapon_ammo_spec_validator", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


class WeaponAmmoAuthoredSpecTests(unittest.TestCase):
    def make_policy(self):
        return {
            "observedReference": {
                "overall": {
                    "xp": {"p75": 22500},
                    "rub": {"p75": 85000},
                    "standing": {"p90": 0.04},
                }
            }
        }

    def make_spec(self):
        families = []
        for family_id in sorted(module.EXPECTED_FAMILY_IDS):
            unlock_slots = 0 if family_id == "special-weapons" else 1
            sample_units = 1 if family_id == "special-weapons" else 30
            families.append({
                "id": family_id,
                "minimumLevel": 5,
                "stages": [
                    {"slug": f"{family_id}-q", "minimumLevel": 5, "kills": 8, "xp": 5000, "rub": 15000, "standing": 0.01},
                    {"slug": f"{family_id}-f", "minimumLevel": 10, "kills": 15, "xp": 8000, "rub": 25000, "standing": 0.02},
                    {"slug": f"{family_id}-m", "minimumLevel": 15, "kills": 20, "xp": 12000, "rub": 35000, "standing": 0.02, "sampleAmmoUnits": sample_units, "unlockSlots": unlock_slots},
                ],
            })
        return {
            "targetSptVersion": "4.1.4",
            "domain": "weaponAmmo",
            "legacySource": {"questCount": 438, "assortmentUnlockCount": 768, "crossBundleEdgeCount": 23},
            "designRules": {
                "legacyTemplateReuse": False,
                "foundInRaidRequired": False,
                "handoverAmmoObjectives": False,
                "currencySpamRewards": False,
                "highEndAmmoUnlimited": False,
                "containerRewards": False,
                "specialWeaponPermanentAmmoUnlock": False,
                "sampleAmmoBeforeUnlock": True,
                "controlledAmmoUnlocksOnly": True,
                "maximumPermanentUnlocksPerQuest": 1,
                "maximumFamilyQuestCount": 3,
            },
            "families": families,
        }

    def test_accepts_compact_seven_family_model(self):
        result = module.validate(self.make_spec(), self.make_policy())
        self.assertEqual(result["familyCount"], 7)
        self.assertEqual(result["questCount"], 21)
        self.assertEqual(result["unlockCount"], 6)

    def test_rejects_unlock_on_qualification(self):
        spec_data = self.make_spec()
        spec_data["families"][0]["stages"][0]["unlockSlots"] = 1
        with self.assertRaises(SystemExit):
            module.validate(spec_data, self.make_policy())

    def test_rejects_ammo_sample_faucet(self):
        spec_data = self.make_spec()
        spec_data["families"][0]["stages"][2]["sampleAmmoUnits"] = 120
        with self.assertRaises(SystemExit):
            module.validate(spec_data, self.make_policy())

    def test_rejects_special_permanent_unlock(self):
        spec_data = self.make_spec()
        special = next(f for f in spec_data["families"] if f["id"] == "special-weapons")
        special["stages"][2]["unlockSlots"] = 1
        with self.assertRaises(SystemExit):
            module.validate(spec_data, self.make_policy())


if __name__ == "__main__":
    unittest.main()
