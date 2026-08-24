#!/usr/bin/env python3
"""Apply runtime-proven SPT 4.1 compatibility repairs to imported Artem data."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

RONIN_IDS = {"668bc5cd834c88e06b08b6a9", "668bc5cd834c88e06b08b6aa"}
OPENLAND_ID = "66326bfd46817c660d015145"
OPENLAND_LEFT_TPL = "6570e5674cc0d2ab1e05edbb"
OPENLAND_RIGHT_TPL = "6570e59b0b57c03ec90b970e"
SLOT_PROTO = "64479fdf9731c8fadc0642c1"


def load(path: Path):
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def save(path: Path, value):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, indent=4, ensure_ascii=False)
        handle.write("\n")


def repair_ronin_presets(config: dict) -> int:
    changed = 0
    for preset in config.get("weaponPresets", []):
        for item in preset.get("_items", []):
            slot_id = item.get("slotId")
            if slot_id == "Helmet_eyes":
                item["slotId"] = "helmet_eyes"
                changed += 1
            elif slot_id == "Helmet_jaw":
                item["slotId"] = "helmet_jaw"
                changed += 1
    return changed


def make_soft_slot(parent: str, slot_id: str, name: str, tpl: str, collider: str) -> dict:
    return {
        "_id": slot_id,
        "_mergeSlotWithChildren": True,
        "_name": name,
        "_parent": parent,
        "_props": {
            "filters": [{
                "Filter": [tpl],
                "Plate": tpl,
                "armorColliders": [collider],
                "armorPlateColliders": [],
                "bluntDamageReduceFromSoftArmor": False,
                "locked": True,
            }]
        },
        "_proto": SLOT_PROTO,
        "_required": True,
    }


def repair_openland_slots(config: dict) -> int:
    slots = config.setdefault("overrideProperties", {}).setdefault("Slots", [])
    names = {slot.get("_name") for slot in slots}
    changed = 0
    if "Soft_armor_left" not in names:
        slots.append(make_soft_slot(
            OPENLAND_ID, "67525416b9da71f5f5a5e001", "Soft_armor_left",
            OPENLAND_LEFT_TPL, "LeftSideChestDown"
        ))
        changed += 1
    if "soft_armor_right" not in names:
        slots.append(make_soft_slot(
            OPENLAND_ID, "67525416b9da71f5f5a5e002", "soft_armor_right",
            OPENLAND_RIGHT_TPL, "RightSideChestDown"
        ))
        changed += 1
    return changed


def validate_preset_slots(config: dict) -> list[str]:
    slots = {slot.get("_name") for slot in config.get("overrideProperties", {}).get("Slots", [])}
    errors = []
    for preset in config.get("weaponPresets", []):
        root_ids = {item.get("_id") for item in preset.get("_items", []) if not item.get("parentId")}
        for item in preset.get("_items", []):
            if item.get("parentId") in root_ids and item.get("slotId") not in slots:
                errors.append(f"{config.get('locales', {}).get('en', {}).get('name', 'item')}: {item.get('slotId')}")
    return errors


def repair_resources(resources: Path) -> tuple[int, list[str]]:
    changed = 0
    errors: list[str] = []
    targets = [
        resources / "db/CustomItems/Artem_Helmets.json",
        resources / "db/CustomItems/Artem_Vests.json",
    ]
    for path in targets:
        payload = load(path)
        dirty = False
        for item_id, config in payload.items():
            delta = 0
            if item_id in RONIN_IDS:
                delta += repair_ronin_presets(config)
            if item_id == OPENLAND_ID:
                delta += repair_openland_slots(config)
            if delta:
                changed += delta
                dirty = True
            if item_id in RONIN_IDS or item_id == OPENLAND_ID:
                errors.extend(validate_preset_slots(config))
        if dirty:
            save(path, payload)
    return changed, errors


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("resources", type=Path)
    args = parser.parse_args()
    changed, errors = repair_resources(args.resources.resolve())
    if errors:
        raise SystemExit("preset/slot compatibility validation failed: " + "; ".join(errors))
    print(f"Artem SPT 4.1 armor compatibility repairs applied: {changed}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
