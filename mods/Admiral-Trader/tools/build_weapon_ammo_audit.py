#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any

DOMAIN_BUNDLES = {"Weapon Proficiency", "Ammo Proficiency"}


def number(value: Any) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def build_audit(inventory: dict[str, Any]) -> dict[str, Any]:
    rows = [row for row in inventory.get("quests", []) if row.get("bundle") in DOMAIN_BUNDLES]
    by_bundle = Counter(str(row.get("bundle")) for row in rows)
    traders = Counter(str(row.get("legacyTraderId")) for row in rows if row.get("legacyTraderId"))
    quest_types = Counter(str(row.get("questType")) for row in rows if row.get("questType"))
    objective_conditions: Counter[str] = Counter()
    objective_targets: Counter[str] = Counter()
    reward_items: Counter[str] = Counter()
    reward_types: Counter[str] = Counter()
    fir_count = 0
    unlock_quests = 0
    total_unlocks = 0
    large_objective_rows: list[dict[str, Any]] = []
    high_unlock_rows: list[dict[str, Any]] = []

    domain_ids = {str(row.get("questId")) for row in rows}
    cross_edges: list[dict[str, Any]] = []

    for row in rows:
        objectives = row.get("objectives") or {}
        rewards = row.get("rewards") or {}
        objective_conditions.update(objectives.get("conditionTypes") or {})
        for tpl in objectives.get("targets") or []:
            objective_targets[str(tpl)] += 1
        if objectives.get("firRequired") is True:
            fir_count += 1
        max_value = number(objectives.get("maxObjectiveValue"))
        if max_value >= 10:
            large_objective_rows.append({
                "questId": row.get("questId"),
                "questName": row.get("questName"),
                "bundle": row.get("bundle"),
                "maxObjectiveValue": max_value,
            })

        unlocks = int(number(rewards.get("unlockCount")))
        if unlocks:
            unlock_quests += 1
            total_unlocks += unlocks
        if unlocks >= 3:
            high_unlock_rows.append({
                "questId": row.get("questId"),
                "questName": row.get("questName"),
                "bundle": row.get("bundle"),
                "unlockCount": unlocks,
            })
        reward_types.update(rewards.get("types") or {})
        for item in rewards.get("items") or []:
            tpl = item.get("tpl")
            if tpl:
                reward_items[str(tpl)] += 1

        for prereq in row.get("prerequisites") or []:
            if str(prereq) in domain_ids:
                source = next((candidate for candidate in rows if str(candidate.get("questId")) == str(prereq)), None)
                if source and source.get("bundle") != row.get("bundle"):
                    cross_edges.append({
                        "fromQuestId": str(prereq),
                        "fromBundle": source.get("bundle"),
                        "toQuestId": str(row.get("questId")),
                        "toBundle": row.get("bundle"),
                    })

    large_objective_rows.sort(key=lambda row: (-row["maxObjectiveValue"], str(row["questId"])))
    high_unlock_rows.sort(key=lambda row: (-row["unlockCount"], str(row["questId"])))
    cross_edges.sort(key=lambda edge: (edge["fromQuestId"], edge["toQuestId"]))

    return {
        "schemaVersion": 1,
        "domain": "weaponAmmo",
        "bundles": sorted(DOMAIN_BUNDLES),
        "summary": {
            "questCount": len(rows),
            "bundleQuestCounts": dict(sorted(by_bundle.items())),
            "legacyTraderCounts": dict(sorted(traders.items())),
            "questTypeCounts": dict(sorted(quest_types.items())),
            "firQuestCount": fir_count,
            "unlockQuestCount": unlock_quests,
            "totalAssortmentUnlocks": total_unlocks,
            "crossBundleEdgeCount": len(cross_edges),
            "largeObjectiveQuestCount": len(large_objective_rows),
            "highUnlockQuestCount": len(high_unlock_rows),
        },
        "objectiveConditionCounts": dict(sorted(objective_conditions.items())),
        "topObjectiveTargets": [
            {"tpl": tpl, "questReferences": count}
            for tpl, count in objective_targets.most_common(50)
        ],
        "rewardTypeCounts": dict(sorted(reward_types.items())),
        "topRewardItems": [
            {"tpl": tpl, "rewardReferences": count}
            for tpl, count in reward_items.most_common(50)
        ],
        "crossBundleEdges": cross_edges,
        "largestObjectives": large_objective_rows[:50],
        "highestUnlockCounts": high_unlock_rows[:50],
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit legacy Weapon + Ammo Proficiency as one Admiral Trader domain")
    parser.add_argument("inventory", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    inventory = json.loads(args.inventory.read_text(encoding="utf-8"))
    audit = build_audit(inventory)
    if audit["summary"]["questCount"] == 0:
        raise SystemExit("weapon/ammo audit found no domain quests")
    if audit["summary"]["crossBundleEdgeCount"] == 0:
        raise SystemExit("weapon/ammo audit found no cross-bundle edges; pinned legacy semantics changed")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(audit, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(audit["summary"], sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
