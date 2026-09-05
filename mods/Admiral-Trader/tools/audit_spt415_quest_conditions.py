#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any

TARGET_SPT_VERSION = "4.1.5"


def walk_conditions(value: Any):
    if isinstance(value, dict):
        if isinstance(value.get("conditionType"), str):
            yield value
        for child in value.values():
            yield from walk_conditions(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk_conditions(child)


def normalized_values(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(x) for x in value]
    return [str(value)]


def audit(quests_raw: Any) -> dict[str, Any]:
    if not isinstance(quests_raw, dict):
        raise ValueError("exact SPT quest database root must be an object")

    condition_counts: Counter[str] = Counter()
    examples: dict[str, list[dict[str, Any]]] = defaultdict(list)
    quest_locations: Counter[str] = Counter()
    visit_place_targets: Counter[str] = Counter()
    location_targets: Counter[str] = Counter()
    kill_targets: Counter[str] = Counter()
    savage_roles: Counter[str] = Counter()
    exit_statuses: Counter[str] = Counter()
    equipment_slots: Counter[str] = Counter()
    daytime_windows: Counter[str] = Counter()
    distance_shapes: Counter[str] = Counter()

    for quest_id, quest in quests_raw.items():
        if not isinstance(quest, dict):
            continue
        location = quest.get("location")
        if location is not None:
            quest_locations[str(location)] += 1

        for condition in walk_conditions(quest.get("conditions") or {}):
            ctype = str(condition["conditionType"])
            condition_counts[ctype] += 1
            if len(examples[ctype]) < 3:
                examples[ctype].append({
                    "questId": str(quest_id),
                    "condition": condition,
                })

            if ctype == "VisitPlace":
                for target in normalized_values(condition.get("target")):
                    visit_place_targets[target] += 1
            elif ctype == "Location":
                for target in normalized_values(condition.get("target")):
                    location_targets[target] += 1
            elif ctype == "Kills":
                for target in normalized_values(condition.get("target")):
                    kill_targets[target] += 1
                for role in normalized_values(condition.get("savageRole")):
                    savage_roles[role] += 1
                daytime = condition.get("daytime")
                if isinstance(daytime, dict):
                    daytime_windows[f"{daytime.get('from')}->{daytime.get('to')}"] += 1
                distance = condition.get("distance")
                if isinstance(distance, dict):
                    distance_shapes[f"{distance.get('compareMethod')}:{distance.get('value')}"] += 1
            elif ctype == "ExitStatus":
                for status in normalized_values(condition.get("status") or condition.get("target")):
                    exit_statuses[status] += 1
            elif ctype == "Equipment":
                for slot in normalized_values(condition.get("equipmentInclusive") or condition.get("equipmentExclusive") or condition.get("target")):
                    equipment_slots[slot] += 1

    def ordered(counter: Counter[str]) -> list[dict[str, Any]]:
        return [{"value": key, "count": value} for key, value in sorted(counter.items(), key=lambda row: (-row[1], row[0]))]

    return {
        "schemaVersion": 1,
        "targetSptVersion": TARGET_SPT_VERSION,
        "questCount": len(quests_raw),
        "conditionTypeCounts": dict(sorted(condition_counts.items())),
        "questLocationValues": ordered(quest_locations),
        "visitPlaceTargets": ordered(visit_place_targets),
        "locationConditionTargets": ordered(location_targets),
        "killTargets": ordered(kill_targets),
        "savageRoles": ordered(savage_roles),
        "exitStatuses": ordered(exit_statuses),
        "equipmentSelectors": ordered(equipment_slots),
        "daytimeWindows": ordered(daytime_windows),
        "distanceShapes": ordered(distance_shapes),
        "examples": dict(sorted(examples.items())),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit native condition shapes in the exact SPT 4.1.5 quest database")
    parser.add_argument("quests", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    quests = json.loads(args.quests.read_text(encoding="utf-8-sig"))
    result = audit(quests)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    summary = {
        "questCount": result["questCount"],
        "conditionTypeCounts": result["conditionTypeCounts"],
        "questLocations": [row["value"] for row in result["questLocationValues"]],
        "killTargets": result["killTargets"][:20],
        "savageRoles": result["savageRoles"][:40],
        "exitStatuses": result["exitStatuses"],
        "daytimeWindows": result["daytimeWindows"][:20],
        "distanceShapes": result["distanceShapes"][:20],
    }
    print(json.dumps(summary, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
