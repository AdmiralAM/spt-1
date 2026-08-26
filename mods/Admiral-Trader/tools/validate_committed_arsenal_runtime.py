#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

TRADER_ID = "d5c27bb3169f8dfbc13f6b69"
SPECIAL_QUEST_ID = "f1368cb3b69c3a4917c4f206"
SPECIAL_SAMPLE_TPL = "6217726288ed9f0845317459"


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


def validate_readiness(qid: str, quest: dict[str, Any], finish: dict[str, Any]) -> None:
    if quest.get("type") != "PickUp":
        raise ValueError(f"Arsenal Qualification {qid} must use PickUp quest type")
    if finish.get("conditionType") != "FindItem":
        raise ValueError(f"Arsenal Qualification {qid} must use FindItem readiness proof")
    if finish.get("onlyFoundInRaid") is not False:
        raise ValueError(f"Arsenal Qualification {qid} must not require FIR")
    if finish.get("value") != 1 or not finish.get("target"):
        raise ValueError(f"Arsenal Qualification {qid} must require one weapon from a non-empty family pool")


def validate_elimination(qid: str, quest: dict[str, Any], finish: dict[str, Any]) -> None:
    if quest.get("type") != "Elimination":
        raise ValueError(f"Arsenal combat quest {qid} must use Elimination quest type")
    if finish.get("conditionType") != "CounterCreator":
        raise ValueError(f"Arsenal combat quest {qid} must contain one CounterCreator")
    counter_conditions = (finish.get("counter") or {}).get("conditions") or []
    if len(counter_conditions) != 1:
        raise ValueError(f"Arsenal combat quest {qid} must contain one counter condition")
    kill = counter_conditions[0]
    if kill.get("conditionType") != "Kills" or not kill.get("weapon"):
        raise ValueError(f"Arsenal combat quest {qid} has invalid Kills weapon filter")


def validate(committed: dict[str, dict[str, Any]], generated: dict[str, Any]) -> None:
    templates = generated.get("templates") or {}
    if len(templates) != 21:
        raise ValueError(f"compiler output must contain 21 Arsenal quests, got {len(templates)}")
    if set(committed) != set(templates):
        missing = sorted(set(templates) - set(committed))
        extra = sorted(set(committed) - set(templates))
        raise ValueError(f"Arsenal committed/generated id drift; missing={missing}; extra={extra}")

    readiness_count = 0
    combat_count = 0
    caliber_proof_count = 0
    for qid in sorted(templates):
        quest = committed[qid]
        if quest != templates[qid]:
            raise ValueError(f"committed Arsenal quest differs from compiler output: {qid}")
        if quest.get("traderId") != TRADER_ID:
            raise ValueError(f"Arsenal quest {qid} trader id drift")
        if quest.get("restartable") is not False:
            raise ValueError(f"Arsenal quest {qid} restartability drift")
        finish = (quest.get("conditions") or {}).get("AvailableForFinish") or []
        if len(finish) != 1:
            raise ValueError(f"Arsenal quest {qid} must contain exactly one finish condition")
        condition = finish[0]
        if condition.get("conditionType") == "FindItem":
            validate_readiness(qid, quest, condition)
            readiness_count += 1
            continue

        validate_elimination(qid, quest, condition)
        combat_count += 1
        kill = ((condition.get("counter") or {}).get("conditions") or [{}])[0]
        if kill.get("weaponCaliber"):
            caliber_proof_count += 1

    if readiness_count != 7:
        raise ValueError(f"expected seven Arsenal Qualification readiness proofs, got {readiness_count}")
    if combat_count != 14:
        raise ValueError(f"expected fourteen Arsenal combat quests, got {combat_count}")
    if caliber_proof_count != 6:
        raise ValueError(f"expected six caliber-constrained Munitions proofs, got {caliber_proof_count}")

    deferred = generated.get("deferredRuntimeItems") or []
    if deferred:
        raise ValueError(f"Arsenal runtime must have no deferred items before test candidate: {deferred}")

    special_rewards = (templates[SPECIAL_QUEST_ID].get("rewards") or {}).get("Success") or []
    special_items = [
        reward for reward in special_rewards
        if reward.get("type") == "Item"
        and (reward.get("items") or [{}])[0].get("_tpl") == SPECIAL_SAMPLE_TPL
    ]
    if len(special_items) != 1:
        raise ValueError("Special Weapons Munitions must contain exactly one explicit green RSP-30 sample reward")
    sample_item = special_items[0]["items"][0]
    if (sample_item.get("upd") or {}).get("StackObjectsCount") != 1:
        raise ValueError("Special Weapons sample reward must remain exactly one unit")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate committed Arsenal Protocol quest data against compiler output")
    parser.add_argument("quest_dir", type=Path)
    parser.add_argument("generated_runtime", type=Path)
    args = parser.parse_args()
    generated = load_json(args.generated_runtime)
    expected_ids = {str(qid) for qid in (generated.get("templates") or {})}
    committed = load_committed(args.quest_dir, expected_ids)
    validate(committed, generated)
    print("validated 21 committed Arsenal Protocol templates: 7 readiness + 14 combat, 6 caliber-constrained Munitions proofs")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
