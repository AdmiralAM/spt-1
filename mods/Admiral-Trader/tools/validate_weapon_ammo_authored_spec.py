#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

EXPECTED_TARGET = "4.1.3"
EXPECTED_FAMILY_IDS = {"handguns","smg-pdw","shotguns","assault-rifles","marksman-battle","precision","special-weapons"}
EXPECTED_STAGE_MODELS = {
    "Qualification": "family-readiness-possession",
    "Fieldwork": "family-eliminations",
    "Munitions": "capability-caliber-eliminations",
}


def fail(message: str) -> None:
    raise SystemExit(message)


def validate(spec: dict[str, Any], reward_policy: dict[str, Any], audit: dict[str, Any] | None = None) -> dict[str, int]:
    if spec.get("targetSptVersion") != EXPECTED_TARGET:
        fail(f"weapon/ammo spec target must be SPT {EXPECTED_TARGET}")
    if spec.get("domain") != "weaponAmmo":
        fail("weapon/ammo authored spec has wrong domain")
    rules = spec.get("designRules") or {}
    required_false = ["legacyTemplateReuse","foundInRaidRequired","handoverAmmoObjectives","currencySpamRewards","highEndAmmoUnlimited","containerRewards","specialWeaponPermanentAmmoUnlock"]
    for key in required_false:
        if rules.get(key) is not False:
            fail(f"design rule {key} must remain false")
    if rules.get("sampleAmmoBeforeUnlock") is not True or rules.get("controlledAmmoUnlocksOnly") is not True:
        fail("ammo capability must remain sample + controlled unlock")
    if int(rules.get("maximumPermanentUnlocksPerQuest", -1)) != 1:
        fail("weapon/ammo quests may grant at most one permanent unlock")
    if int(rules.get("maximumFamilyQuestCount", -1)) != 3:
        fail("weapon/ammo family chains must remain capped at three quests")

    stage_model = spec.get("stageModel") or []
    actual_models = {str(row.get("stage")): str(row.get("objectiveModel")) for row in stage_model if isinstance(row, dict)}
    if actual_models != EXPECTED_STAGE_MODELS:
        fail(f"Arsenal stage objective models drift: {actual_models}")
    if len(set(actual_models.values())) != 3:
        fail("Arsenal stages must use three distinct objective models")

    families = spec.get("families") or []
    ids = [str(family.get("id")) for family in families]
    if set(ids) != EXPECTED_FAMILY_IDS or len(ids) != len(EXPECTED_FAMILY_IDS):
        fail(f"weapon/ammo family set drift: {ids}")

    slugs: set[str] = set()
    quest_count = unlock_count = sample_units = 0
    overall = ((reward_policy.get("observedReference") or {}).get("overall") or {})
    p75_xp = float((overall.get("xp") or {}).get("p75", 0))
    p75_rub = float((overall.get("rub") or {}).get("p75", 0))
    p90_standing = float((overall.get("standing") or {}).get("p90", 0))

    for family in families:
        family_id = str(family.get("id"))
        stages = family.get("stages") or []
        if len(stages) != 3:
            fail(f"family {family_id} must contain exactly three stages")
        levels = [int(stage.get("minimumLevel", -1)) for stage in stages]
        if levels != sorted(levels) or levels[0] < int(family.get("minimumLevel", -1)):
            fail(f"family {family_id} has invalid level progression")
        for index, stage in enumerate(stages):
            quest_count += 1
            slug = str(stage.get("slug") or "")
            if not slug or slug in slugs:
                fail(f"duplicate/empty weapon-ammo slug: {slug}")
            slugs.add(slug)

            if index == 0:
                readiness = int(stage.get("readinessCount", 0))
                if readiness != 1:
                    fail(f"{slug}: Qualification must require possession of exactly one family weapon")
                if "kills" in stage:
                    fail(f"{slug}: Qualification must not regress to a kill-count objective")
            else:
                kills = int(stage.get("kills", 0))
                if kills <= 0 or kills > 25:
                    fail(f"{slug}: elimination objective exceeds compact-chain budget: {kills}")

            xp, rub, standing = float(stage.get("xp", 0)), float(stage.get("rub", 0)), float(stage.get("standing", 0))
            if xp <= 0 or xp > p75_xp:
                fail(f"{slug}: XP {xp} outside ordinary p75 ceiling {p75_xp}")
            if rub < 0 or rub > p75_rub:
                fail(f"{slug}: RUB {rub} outside ordinary p75 ceiling {p75_rub}")
            if standing < 0 or standing > p90_standing:
                fail(f"{slug}: standing {standing} exceeds vanilla p90 {p90_standing}")
            unlock_slots = int(stage.get("unlockSlots", 0))
            if index < 2 and unlock_slots != 0:
                fail(f"{slug}: qualification/fieldwork may not grant permanent unlocks")
            if index == 2:
                expected_unlocks = 0 if family_id == "special-weapons" else 1
                if unlock_slots != expected_unlocks:
                    fail(f"{slug}: expected {expected_unlocks} permanent unlock slot(s), got {unlock_slots}")
                sample = int(stage.get("sampleAmmoUnits", 0))
                if sample <= 0 or sample > 40:
                    fail(f"{slug}: ammo sample must be 1..40 units, got {sample}")
                if family_id == "special-weapons" and sample != 1:
                    fail("special-weapons munitions sample must remain exactly one unit")
                sample_units += sample
            unlock_count += unlock_slots

    if quest_count != 21:
        fail(f"weapon/ammo authored quest count must remain 21, got {quest_count}")
    if unlock_count != 6:
        fail(f"weapon/ammo authored permanent unlock budget must remain 6, got {unlock_count}")
    if audit is not None:
        legacy = audit.get("summary") or {}
        source = spec.get("legacySource") or {}
        if int(legacy.get("questCount", 0)) != int(source.get("questCount", -1)):
            fail("legacy weapon/ammo quest count drift between audit and authored spec")
        if int(legacy.get("totalAssortmentUnlocks", 0)) != int(source.get("assortmentUnlockCount", -1)):
            fail("legacy weapon/ammo unlock count drift between audit and authored spec")
        if int(legacy.get("crossBundleEdgeCount", 0)) != int(source.get("crossBundleEdgeCount", -1)):
            fail("legacy weapon/ammo cross-edge count drift between audit and authored spec")
    return {"familyCount": len(families), "questCount": quest_count, "unlockCount": unlock_count, "sampleAmmoUnits": sample_units}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("spec", type=Path)
    parser.add_argument("reward_policy", type=Path)
    parser.add_argument("--audit", type=Path)
    args = parser.parse_args()
    spec = json.loads(args.spec.read_text(encoding="utf-8"))
    policy = json.loads(args.reward_policy.read_text(encoding="utf-8"))
    audit = json.loads(args.audit.read_text(encoding="utf-8")) if args.audit else None
    print(json.dumps(validate(spec, policy, audit), sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
