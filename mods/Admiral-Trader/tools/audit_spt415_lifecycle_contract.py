#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path
from typing import Any

TARGET = "4.1.5"
MISSING = "<missing>"


def value_key(value: Any) -> str:
    if value is None:
        return MISSING
    if isinstance(value, bool):
        return "true" if value else "false"
    return str(value)


def summarize(quests: dict[str, Any]) -> dict[str, Any]:
    acceptance = Counter()
    progress = Counter()
    instant = Counter()
    restartable = Counter()
    status_presence = 0
    spt_status_presence = 0
    condition_nonempty = Counter()
    reward_nonempty = Counter()
    state_shape = Counter()

    for quest in quests.values():
        if not isinstance(quest, dict):
            continue
        acceptance[value_key(quest.get("acceptanceAndFinishingSource"))] += 1
        progress[value_key(quest.get("progressSource"))] += 1
        instant[value_key(quest.get("instantComplete"))] += 1
        restartable[value_key(quest.get("restartable"))] += 1
        status_presence += int("status" in quest and quest.get("status") is not None)
        spt_status_presence += int("sptStatus" in quest and quest.get("sptStatus") is not None)

        conditions = quest.get("conditions") or {}
        rewards = quest.get("rewards") or {}
        condition_state = []
        for state in ("Started", "Success", "Fail"):
            rows = conditions.get(state)
            nonempty = isinstance(rows, list) and len(rows) > 0
            condition_nonempty[state] += int(nonempty)
            condition_state.append(f"{state}={'nonempty' if nonempty else 'empty'}")
            reward_rows = rewards.get(state)
            reward_nonempty[state] += int(isinstance(reward_rows, list) and len(reward_rows) > 0)
        state_shape["|".join(condition_state)] += 1

    def ordered(counter: Counter[str]) -> dict[str, int]:
        return dict(sorted(counter.items(), key=lambda row: (-row[1], row[0])))

    return {
        "questCount": len(quests),
        "acceptanceAndFinishingSource": ordered(acceptance),
        "progressSource": ordered(progress),
        "instantComplete": ordered(instant),
        "restartable": ordered(restartable),
        "statusPresenceCount": status_presence,
        "sptStatusPresenceCount": spt_status_presence,
        "nonEmptyLifecycleConditions": dict(condition_nonempty),
        "nonEmptyLifecycleRewards": dict(reward_nonempty),
        "lifecycleConditionShapes": ordered(state_shape),
    }


def load_authored(directory: Path) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for path in sorted(directory.glob("*.json")):
        quest = json.loads(path.read_text(encoding="utf-8"))
        qid = str(quest.get("_id") or path.stem)
        if qid in result:
            raise ValueError(f"duplicate authored quest id: {qid}")
        result[qid] = quest
    return result


def audit(exact_quests: dict[str, Any], authored_quests: dict[str, Any] | None = None) -> dict[str, Any]:
    if not isinstance(exact_quests, dict):
        raise ValueError("exact SPT quest database root must be an object")
    output = {
        "schemaVersion": 1,
        "targetSptVersion": TARGET,
        "exactVanilla": summarize(exact_quests),
    }
    if authored_quests is not None:
        output["admiralAuthored"] = summarize(authored_quests)
    return output


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit lifecycle fields against exact SPT 4.1.5 quest templates")
    parser.add_argument("quests", type=Path, help="Exact SPT 4.1.5 templates/quests.json")
    parser.add_argument("--authored-dir", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    exact = json.loads(args.quests.read_text(encoding="utf-8-sig"))
    authored = load_authored(args.authored_dir) if args.authored_dir else None
    result = audit(exact, authored)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
