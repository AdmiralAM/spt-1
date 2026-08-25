#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def choose_candidate(ammo: list[dict[str, Any]], ceiling: float, preferred_calibers: list[str] | None = None) -> dict[str, Any]:
    preferred = set(preferred_calibers or [])
    eligible = [
        row for row in ammo
        if row.get("tier") == "controlled"
        and float(row.get("penetration") or 0) <= ceiling
        and (not preferred or str(row.get("caliber")) in preferred)
    ]
    if not eligible:
        raise ValueError(
            f"no controlled ammunition candidate at or below penetration ceiling {ceiling} "
            f"for preferred calibers {sorted(preferred)}"
        )
    eligible.sort(key=lambda row: (-float(row.get("penetration") or 0), -float(row.get("damage") or 0), str(row.get("tpl"))))
    return eligible[0]


def build_selection(pools: dict[str, Any], policy: dict[str, Any]) -> dict[str, Any]:
    if pools.get("targetSptVersion") != "4.1.3" or policy.get("targetSptVersion") != "4.1.3":
        raise ValueError("weapon/ammo selection must remain targeted to SPT 4.1.3")
    output: dict[str, Any] = {
        "schemaVersion": 2,
        "targetSptVersion": "4.1.3",
        "sourceRole": "deterministic-family-distinct-candidate-selection; exact-runtime-4.1.3-template-verification-required",
        "families": {},
    }
    for family_id, family_policy in policy["families"].items():
        pool = pools["families"].get(family_id)
        if pool is None:
            raise ValueError(f"missing candidate pool for {family_id}")
        if family_id == "special-weapons":
            output["families"][family_id] = {
                "permanentUnlock": False,
                "sampleUnits": int(family_policy["sampleUnits"]),
                "reason": "explosive/heavy ammunition is sample-only and never becomes a permanent Admiral faucet"
            }
            continue
        selected = choose_candidate(
            pool.get("ammo") or [],
            float(family_policy["maxPermanentPenetration"]),
            [str(x) for x in family_policy.get("preferredCalibers") or []],
        )
        output["families"][family_id] = {
            "permanentUnlock": True,
            "tpl": selected["tpl"],
            "name": selected["name"],
            "caliber": selected["caliber"],
            "penetration": selected["penetration"],
            "damage": selected["damage"],
            "tier": selected["tier"],
            "sampleUnits": int(family_policy["sampleUnits"]),
            "stockPerReset": int(family_policy["stockPerReset"]),
            "buyRestriction": int(family_policy["buyRestriction"]),
            "penetrationCeiling": family_policy["maxPermanentPenetration"],
            "preferredCalibers": family_policy.get("preferredCalibers") or []
        }
    permanent = [x for x in output["families"].values() if x.get("permanentUnlock")]
    if len(permanent) != int(policy["globalRules"]["permanentUnlockFamilies"]):
        raise ValueError(f"expected {policy['globalRules']['permanentUnlockFamilies']} permanent family unlocks, got {len(permanent)}")
    return output


def main() -> int:
    parser = argparse.ArgumentParser(description="Select family-distinct controlled Admiral ammunition rewards from resolved candidate pools")
    parser.add_argument("pools", type=Path)
    parser.add_argument("policy", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    pools = json.loads(args.pools.read_text(encoding="utf-8"))
    policy = json.loads(args.policy.read_text(encoding="utf-8"))
    result = build_selection(pools, policy)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: (v.get("name") if v.get("permanentUnlock") else "sample-only") for k, v in result["families"].items()}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
