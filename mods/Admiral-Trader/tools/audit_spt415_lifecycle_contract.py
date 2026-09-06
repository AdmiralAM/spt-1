#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
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
    status_values = Counter()
    spt_status_values = Counter()
    correlations = Counter()
    correlation_examples: dict[str, list[str]] = defaultdict(list)
    condition_nonempty = Counter()
    reward_nonempty = Counter()
    state_shape = Counter()

    for quest_id, quest in quests.items():
        if not isinstance(quest, dict):
            continue
        acceptance_value = value_key(quest.get("acceptanceAndFinishingSource"))
        progress_value = value_key(quest.get("progressSource"))
        status_value = value_key(quest.get("status"))
        spt_status_value = value_key(quest.get("sptStatus"))

        acceptance[acceptance_value] += 1
        progress[progress_value] += 1
        instant[value_key(quest.get("instantComplete"))] += 1
        restartable[value_key(quest.get("restartable"))] += 1
        status_values[status_value] += 1
        spt_status_values[spt_status_value] += 1

        correlation = f"accept={acceptance_value}|progress={progress_value}|status={status_value}"
        correlations[correlation] += 1
        if len(correlation_examples[correlation]) < 8:
            correlation_examples[correlation].append(str(quest_id))

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

    ordered_correlations = ordered(correlations)
    return {
        "questCount": len(quests),
        "acceptanceAndFinishingSource": ordered(acceptance),
        "progressSource": ordered(progress),
        "instantComplete": ordered(instant),
        "restartable": ordered(restartable),
        "statusValues": ordered(status_values),
        "sptStatusValues": ordered(spt_status_values),
        "statusPresenceCount": len(quests) - status_values[MISSING],
        "sptStatusPresenceCount": len(quests) - spt_status_values[MISSING],
        "lifecycleFieldCorrelations": ordered_correlations,
        "lifecycleFieldCorrelationExamples": {
            key: correlation_examples[key] for key in ordered_correlations
        },
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
        "schemaVersion": 2,
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
