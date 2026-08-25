#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

MOD_GUID = "com.admiralam.spt.admiraltrader"
TRADER_ID = "d5c27bb3169f8dfbc13f6b69"


def level_bucket(level: int) -> str:
    if level <= 10:
        return "01-10"
    if level <= 20:
        return "11-20"
    if level <= 30:
        return "21-30"
    if level <= 40:
        return "31-40"
    return "41+"


def derived_id(slug: str) -> str:
    return hashlib.sha256(f"{MOD_GUID}:{slug}".encode()).hexdigest()[:24]


def validate(spec: dict[str, Any], plan: dict[str, Any], benchmark: dict[str, Any]) -> list[str]:
    errors: list[str] = []
    quests = spec.get("quests") or []
    if not isinstance(quests, list) or not quests:
        return ["authored spec has no quests"]

    if spec.get("traderId") != TRADER_ID:
        errors.append("authored spec trader id drift")

    plan_groups = plan.get("groups") or []
    expected_slugs = {str(group.get("id")) for group in plan_groups if isinstance(group, dict)}
    slugs = [str(quest.get("slug")) for quest in quests if isinstance(quest, dict)]
    ids = [str(quest.get("id")) for quest in quests if isinstance(quest, dict)]

    if len(slugs) != len(set(slugs)):
        errors.append("duplicate authored quest slug")
    if len(ids) != len(set(ids)):
        errors.append("duplicate authored quest id")
    if set(slugs) != expected_slugs:
        errors.append(f"authored group coverage mismatch: expected={sorted(expected_slugs)} actual={sorted(set(slugs))}")
    if len(quests) > int(plan.get("targetQuestCountMax", 0)):
        errors.append("authored quest count exceeds curated plan maximum")

    by_id = {str(quest.get("id")): quest for quest in quests if isinstance(quest, dict)}
    id_set = set(by_id)
    pick_up = (benchmark.get("questTypeBuckets") or {}).get("PickUp") or {}
    level_buckets = benchmark.get("levelBuckets") or {}

    for quest in quests:
        if not isinstance(quest, dict):
            errors.append("authored quest entry is not an object")
            continue
        slug = str(quest.get("slug"))
        qid = str(quest.get("id"))
        if qid != derived_id(slug):
            errors.append(f"{slug}: deterministic quest id mismatch")
        if len(qid) != 24 or any(ch not in "0123456789abcdef" for ch in qid):
            errors.append(f"{slug}: quest id is not lower-case 24-hex")

        level = int(quest.get("minimumLevel", 0))
        if level < 1:
            errors.append(f"{slug}: invalid minimum level")
        for predecessor in quest.get("prerequisites") or []:
            predecessor = str(predecessor)
            if predecessor not in id_set:
                errors.append(f"{slug}: external prerequisite {predecessor}")
            elif int(by_id[predecessor].get("minimumLevel", 0)) > level:
                errors.append(f"{slug}: prerequisite level exceeds child level")

        objective = quest.get("objective") or {}
        if objective.get("sourceGroup") != slug:
            errors.append(f"{slug}: objective source group drift")
        representative_count = int(objective.get("representativeCount", 0))
        if representative_count < 1 or representative_count > 3:
            errors.append(f"{slug}: representative key count outside 1..3")

        reward = quest.get("rewardBudget") or {}
        bucket = level_buckets.get(level_bucket(level)) or {}
        xp_cap = max(float((pick_up.get("xp") or {}).get("p75", 0)), float((bucket.get("xp") or {}).get("p75", 0)))
        rub_cap = max(float(((pick_up.get("currency") or {}).get("RUB") or {}).get("p75", 0)), float((((bucket.get("currency") or {}).get("RUB") or {}).get("p75", 0))))
        standing_cap = max(float((pick_up.get("standing") or {}).get("p75", 0)), float((bucket.get("standing") or {}).get("p75", 0)))
        if float(reward.get("xp", 0)) > xp_cap:
            errors.append(f"{slug}: XP budget exceeds p75 envelope ({reward.get('xp')} > {xp_cap})")
        if float(reward.get("rub", 0)) > rub_cap:
            errors.append(f"{slug}: RUB budget exceeds p75 envelope ({reward.get('rub')} > {rub_cap})")
        if float(reward.get("standing", 0)) > standing_cap:
            errors.append(f"{slug}: standing budget exceeds p75 envelope ({reward.get('standing')} > {standing_cap})")
        unlock_slots = int(reward.get("unlockSlots", 0))
        if unlock_slots < 0 or unlock_slots > 1:
            errors.append(f"{slug}: unlock slot budget outside 0..1")
        if unlock_slots and slug != "keys-labs-clearance":
            errors.append(f"{slug}: permanent unlock reserved for final clearance milestone")

    # Detect prerequisite cycles.
    state: dict[str, int] = {}
    def visit(qid: str) -> None:
        if state.get(qid) == 1:
            errors.append(f"prerequisite cycle detected at {qid}")
            return
        if state.get(qid) == 2:
            return
        state[qid] = 1
        for predecessor in by_id[qid].get("prerequisites") or []:
            predecessor = str(predecessor)
            if predecessor in by_id:
                visit(predecessor)
        state[qid] = 2
    for qid in sorted(by_id):
        visit(qid)

    # Anti-grind structure: map milestones are not serialized into one mandatory chain.
    intro_id = derived_id("keys-intro")
    for slug in ("keys-factory", "keys-customs", "keys-woods", "keys-interchange", "keys-shoreline", "keys-reserve", "keys-lighthouse"):
        quest = next((row for row in quests if row.get("slug") == slug), None)
        if quest is None:
            continue
        if quest.get("prerequisites") != [intro_id]:
            errors.append(f"{slug}: map milestone must branch directly from intro")

    labs = next((row for row in quests if row.get("slug") == "keys-labs"), None)
    if labs is not None:
        expected = {derived_id("keys-reserve"), derived_id("keys-lighthouse")}
        if set(str(value) for value in labs.get("prerequisites") or []) != expected:
            errors.append("keys-labs: late-game gate must require Reserve and Lighthouse milestones")

    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate Admiral Trader authored Keys quest specification.")
    parser.add_argument("spec", type=Path)
    parser.add_argument("plan", type=Path)
    parser.add_argument("benchmark", type=Path)
    args = parser.parse_args()

    spec = json.loads(args.spec.read_text(encoding="utf-8-sig"))
    plan = json.loads(args.plan.read_text(encoding="utf-8-sig"))
    benchmark = json.loads(args.benchmark.read_text(encoding="utf-8-sig"))
    errors = validate(spec, plan, benchmark)
    if errors:
        for error in errors:
            print(error)
        return 1
    print(json.dumps({"questCount": len(spec["quests"]), "status": "valid"}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
