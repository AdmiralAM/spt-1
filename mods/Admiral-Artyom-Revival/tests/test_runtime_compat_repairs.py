import importlib.util
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TOOL = ROOT / "tools" / "repair_runtime_compat.py"

spec = importlib.util.spec_from_file_location("repair_runtime_compat", TOOL)
assert spec and spec.loader
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)

ronin = {
    "locales": {"en": {"name": "Ronin"}},
    "overrideProperties": {"Slots": [{"_name": "helmet_eyes"}, {"_name": "helmet_jaw"}]},
    "weaponPresets": [{"_items": [
        {"_id": "root"},
        {"_id": "a", "parentId": "root", "slotId": "Helmet_eyes"},
        {"_id": "b", "parentId": "root", "slotId": "Helmet_jaw"},
    ]}],
}
assert mod.repair_ronin_presets(ronin) == 2
assert not mod.validate_preset_slots(ronin)

openland = {
    "locales": {"en": {"name": "OPENLAND HEXAGON Plate Carrier"}},
    "overrideProperties": {"Slots": [{"_name": "Soft_armor_front"}, {"_name": "Soft_armor_back"}]},
    "weaponPresets": [{"_items": [
        {"_id": "root"},
        {"_id": "l", "parentId": "root", "slotId": "Soft_armor_left"},
        {"_id": "r", "parentId": "root", "slotId": "soft_armor_right"},
    ]}],
}
assert mod.repair_openland_slots(openland) == 2
assert not mod.validate_preset_slots(openland)
slots = {s["_name"]: s for s in openland["overrideProperties"]["Slots"]}
assert slots["Soft_armor_left"]["_props"]["filters"][0]["Plate"] == mod.OPENLAND_LEFT_TPL
assert slots["soft_armor_right"]["_props"]["filters"][0]["Plate"] == mod.OPENLAND_RIGHT_TPL
print("OK: runtime armor compatibility repairs")
