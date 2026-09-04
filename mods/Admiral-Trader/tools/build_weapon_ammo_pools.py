#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any

FAMILY_CLASSES = {
    "handguns": {"pistol", "revolver"},
    "smg-pdw": {"smg"},
    "shotguns": {"shotgun"},
    "assault-rifles": {"assaultRifle"},
    "marksman-battle": {"marksmanRifle"},
    "precision": {"sniperRifle"},
    "special-weapons": {"machinegun", "grenadeLauncher", "specialWeapon"},
}

CALIBER_TOKENS = {
    "9x18": {"Caliber9x18PM"},
    "9x19": {"Caliber9x19PARA"},
    "9x21": {"Caliber9x21"},
    ".45 ACP": {"Caliber1143x23ACP"},
    "4.6x30": {"Caliber46x30"},
    "5.7x28": {"Caliber57x28"},
    "12/70": {"Caliber12g"},
    "20/70": {"Caliber20g"},
    "23x75": {"Caliber23x75"},
    "5.45x39": {"Caliber545x39"},
    "5.56x45": {"Caliber556x45NATO"},
    "7.62x39": {"Caliber762x39"},
    ".300 Blackout": {"Caliber762x35", "Caliber300BLK"},
    "7.62x51": {"Caliber762x51"},
    "7.62x54": {"Caliber762x54R"},
    "6.8x51": {"Caliber68x51"},
    ".366 TKM": {"Caliber366TKM"},
    ".338 Lapua": {"Caliber86x70"},
}

BLOCKED_AMMO_NAME_TOKENS = ("shrapnel", "fragment", "explosion", "fuze", "fuse")


def props(item: dict[str, Any]) -> dict[str, Any]:
    value = item.get("_props")
    return value if isinstance(value, dict) else {}


def item_name(item: dict[str, Any], tpl: str) -> str:
    for key in ("_name", "Name", "name"):
        value = item.get(key)
        if value:
            return str(value)
    p = props(item)
    for key in ("Name", "ShortName"):
        value = p.get(key)
        if value:
            return str(value)
    return tpl


def is_real_item(item: dict[str, Any]) -> bool:
    return str(item.get("_type") or "").lower() == "item"


def normalize_items(raw: Any) -> dict[str, dict[str, Any]]:
    if isinstance(raw, dict):
        if "Items" in raw and isinstance(raw["Items"], dict):
            raw = raw["Items"]
        return {str(k): v for k, v in raw.items() if isinstance(v, dict)}
    raise ValueError("items database root must be an object")


def caliber_tokens(hints: list[str]) -> set[str]:
    result: set[str] = set()
    for hint in hints:
        result.update(CALIBER_TOKENS.get(str(hint), set()))
    return result


def ammo_tier(penetration: float) -> str:
    if penetration >= 45:
        return "high-end"
    if penetration >= 32:
        return "controlled"
    return "standard"


def is_explosive_or_fragment_ammo(item: dict[str, Any], family_id: str) -> bool:
    name = item_name(item, "").lower()
    if any(token in name for token in BLOCKED_AMMO_NAME_TOKENS):
        return True
    p = props(item)
    ammo_type = str(p.get("ammoType") or p.get("AmmoType") or "").lower()
    if ammo_type in {"grenade", "explosive", "fragment", "shrapnel"}:
        return True
    if family_id == "special-weapons":
        caliber = str(p.get("Caliber") or "").lower()
        if any(token in caliber for token in ("40x", "40mm", "26x", "flare", "grenade")):
            return True
    return False


def build_pools(items_raw: Any, spec: dict[str, Any]) -> dict[str, Any]:
    items = normalize_items(items_raw)
    result: dict[str, Any] = {
        "schemaVersion": 3,
        "targetSptVersion": spec.get("targetSptVersion"),
        "sourceRole": "pinned-backend-item-candidate-resolution; exact-runtime-4.1.4-verification-required",
        "families": {},
    }

    class_counts: Counter[str] = Counter()
    for item in items.values():
        if not is_real_item(item):
            continue
        weapon_class = props(item).get("weapClass")
        if weapon_class:
            class_counts[str(weapon_class)] += 1
    result["observedWeaponClasses"] = dict(sorted(class_counts.items()))

    for family in spec.get("families") or []:
        family_id = str(family.get("id"))
        wanted_classes = FAMILY_CLASSES.get(family_id, set())
        wanted_calibers = caliber_tokens([str(x) for x in family.get("caliberHints") or []])

        weapon_rows: list[dict[str, Any]] = []
        ammo_rows: list[dict[str, Any]] = []
        excluded_ammo_rows: list[dict[str, Any]] = []
        observed_weapon_calibers: set[str] = set()

        for tpl, item in items.items():
            if not is_real_item(item):
                continue
            p = props(item)
            weapon_class = str(p.get("weapClass") or "")
            weapon_caliber = str(p.get("ammoCaliber") or p.get("Caliber") or "")
            if weapon_class not in wanted_classes:
                continue
            # Explicit authored calibers are a curated allowlist. This prevents exotic
            # or class-mislabelled weapons from widening normal family ammo capability.
            if wanted_calibers and weapon_caliber and weapon_caliber not in wanted_calibers:
                continue
            weapon_rows.append({
                "tpl": tpl,
                "name": item_name(item, tpl),
                "weapClass": weapon_class,
                "caliber": weapon_caliber,
            })
            if weapon_caliber:
                observed_weapon_calibers.add(weapon_caliber)

        effective_calibers = set(wanted_calibers) if wanted_calibers else observed_weapon_calibers

        for tpl, item in items.items():
            if not is_real_item(item):
                continue
            p = props(item)
            caliber = str(p.get("Caliber") or "")
            if not caliber or caliber not in effective_calibers:
                continue
            if "PenetrationPower" not in p and "Damage" not in p:
                continue
            penetration = float(p.get("PenetrationPower") or 0)
            damage = float(p.get("Damage") or 0)
            row = {
                "tpl": tpl,
                "name": item_name(item, tpl),
                "caliber": caliber,
                "penetration": penetration,
                "damage": damage,
                "tier": ammo_tier(penetration),
            }
            if is_explosive_or_fragment_ammo(item, family_id):
                row["reason"] = "fragment/explosive/heavy ammo excluded from automatic unlock candidates"
                excluded_ammo_rows.append(row)
                continue
            ammo_rows.append(row)

        weapon_rows.sort(key=lambda row: (row["weapClass"], row["caliber"], row["name"], row["tpl"]))
        ammo_rows.sort(key=lambda row: (row["caliber"], row["penetration"], row["damage"], row["name"], row["tpl"]))
        excluded_ammo_rows.sort(key=lambda row: (row["caliber"], row["name"], row["tpl"]))
        tiers = Counter(row["tier"] for row in ammo_rows)
        result["families"][family_id] = {
            "requestedWeaponClasses": sorted(wanted_classes),
            "authoredCaliberHints": family.get("caliberHints") or [],
            "resolvedCalibers": sorted(effective_calibers),
            "weaponCount": len(weapon_rows),
            "ammoCount": len(ammo_rows),
            "excludedAmmoCount": len(excluded_ammo_rows),
            "ammoTierCounts": dict(sorted(tiers.items())),
            "weapons": weapon_rows,
            "ammo": ammo_rows,
            "excludedAmmo": excluded_ammo_rows,
        }

    return result


def main() -> int:
    parser = argparse.ArgumentParser(description="Resolve Admiral weapon/ammo candidates from a pinned EFT backend item database")
    parser.add_argument("items", type=Path)
    parser.add_argument("spec", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    items = json.loads(args.items.read_text(encoding="utf-8-sig"))
    spec = json.loads(args.spec.read_text(encoding="utf-8"))
    pools = build_pools(items, spec)
    missing_weapons = [fid for fid, data in pools["families"].items() if data["weaponCount"] == 0]
    missing_ammo = [fid for fid, data in pools["families"].items() if fid != "special-weapons" and data["ammoCount"] == 0]
    if missing_weapons:
        raise SystemExit(f"no weapons resolved for families: {missing_weapons}; inspect observedWeaponClasses")
    if missing_ammo:
        raise SystemExit(f"no ammunition resolved for families: {missing_ammo}")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(pools, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    summary = {fid: {"weapons": d["weaponCount"], "ammo": d["ammoCount"], "excludedAmmo": d["excludedAmmoCount"], "tiers": d["ammoTierCounts"]} for fid, d in pools["families"].items()}
    print(json.dumps(summary, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
