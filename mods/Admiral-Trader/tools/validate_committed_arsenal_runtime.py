#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

TRADER_ID = "d5c27bb3169f8dfbc13f6b69"


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_committed(directory: Path, expected_ids: set[str]) -> dict[str, dict[str, Any]]:
    result: dict[str, dict[str, Any]] = {}
    for path in sorted(directory.glob("*.json")):
        quest = load_json(path)
        if not isinstance(quest, dict):
            raise ValueError(f"quest file is not an object: {path}")
        qid = str(quest.get("_id") or "")
        if qid not in expected_ids:
            continue
        if qid in result:
            raise ValueError(f"duplicate committed Arsenal quest id: {qid}")
        result[qid] = quest
    return result


def validate(committed: dict[str, dict[str, Any]], generated: dict[str, Any]) -> None:
    templates = generated.get("templates") or {}
    if len(templates) != 21:
        raise ValueError(f"compiler output must contain 21 Arsenal quests, got {len(templates)}")
    if set(committed) != set(templates):
        missing = sorted(set(templates) - set(committed))
        extra = sorted(set(committed) - set(templates))
        raise ValueError(f"Arsenal committed/generated id drift; missing={missing}; extra={extra}")

    for qid in sorted(templates):
        quest = committed[qid]
        if quest != templates[qid]:
            raise ValueError(f"committed Arsenal quest differs from compiler output: {qid}")
        if quest.get("traderId") != TRADER_ID:
            raise ValueError(f"Arsenal quest {qid} trader id drift")
        if quest.get("restartable") is not False or quest.get("type") != "Elimination":
            raise ValueError(f"Arsenal quest {qid} type/restartability drift")
        finish = (quest.get("conditions") or {}).get("AvailableForFinish") or []
        if len(finish) != 1 or finish[0].get("conditionType") != "CounterCreator":
            raise ValueError(f"Arsenal quest {qid} must contain one CounterCreator")
        counter_conditions = (finish[0].get("counter") or {}).get("conditions") or []
        if len(counter_conditions) != 1:
            raise ValueError(f"Arsenal quest {qid} must contain one counter condition")
        kill = counter_conditions[0]
        if kill.get("conditionType") != "Kills" or not kill.get("weapon"):
            raise ValueError(f"Arsenal quest {qid} has invalid Kills weapon filter")
        finish_types = {condition.get("conditionType") for condition in finish}
        if "FindItem" in finish_types or "HandoverItem" in finish_types:
            raise ValueError(f"Arsenal quest {qid} leaked item grind")

    deferred = generated.get("deferredRuntimeItems") or []
    if len(deferred) != 1 or deferred[0].get("questId") != "f1368cb3b69c3a4917c4f206":
        raise ValueError("Special Weapons sample must be the only deferred Arsenal runtime item")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate committed Arsenal Protocol quest data against compiler output")
    parser.add_argument("quest_dir", type=Path)
    parser.add_argument("generated_runtime", type=Path)
    args = parser.parse_args()
    generated = load_json(args.generated_runtime)
    expected_ids = {str(qid) for qid in (generated.get("templates") or {})}
    committed = load_committed(args.quest_dir, expected_ids)
    validate(committed, generated)
    print("validated 21 committed Arsenal Protocol quest templates")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
