#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

ACTIVE_STATUSES = {2, 3}
SUCCESS_STATUSES = {4}
FAILED_STATUSES = {5, 6, 7, 8}
DELAYED_STATUSES = {9}
PRESTART_STATUSES = {0, 1}


def profile_quest_records(profile: Any) -> list[dict[str, Any]]:
    if not isinstance(profile, dict):
        return []
    raw = profile.get("Quests") or profile.get("quests") or []
    if isinstance(raw, dict):
        raw = list(raw.values())
    return [entry for entry in raw if isinstance(entry, dict)]


def quest_id(record: dict[str, Any]) -> str | None:
    for key in ("qid", "Qid", "questId", "QuestId"):
        value = record.get(key)
        if value is not None and str(value).strip():
            return str(value)
    return None


def quest_status(record: dict[str, Any]) -> int | None:
    value = record.get("status", record.get("Status"))
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def build_plan(inventory: dict[str, Any], profile: Any) -> dict[str, Any]:
    rows = inventory.get("quests") or []
    by_id = {
        str(row.get("questId")): row
        for row in rows
        if isinstance(row, dict) and row.get("questId") is not None
    }

    retained: set[str] = set()
    completed_history: set[str] = set()
    failed_history: set[str] = set()
    delayed_records: set[str] = set()
    stale_records: set[str] = set()
    excluded_restartable: set[str] = set()
    unknown_status: list[dict[str, Any]] = []
    unknown_profile_quests: list[str] = []

    for record in profile_quest_records(profile):
        qid = quest_id(record)
        if qid is None:
            continue
        row = by_id.get(qid)
        if row is None:
            unknown_profile_quests.append(qid)
            continue

        status = quest_status(record)
        restartable = bool(row.get("restartable", False))

        if status in ACTIVE_STATUSES:
            if restartable:
                excluded_restartable.add(qid)
            else:
                retained.add(qid)
        elif status in SUCCESS_STATUSES:
            completed_history.add(qid)
        elif status in FAILED_STATUSES:
            failed_history.add(qid)
            if restartable:
                excluded_restartable.add(qid)
        elif status in DELAYED_STATUSES:
            delayed_records.add(qid)
        elif status in PRESTART_STATUSES:
            stale_records.add(qid)
        else:
            unknown_status.append({"questId": qid, "status": status})

    blocked_successors: set[str] = set()
    for qid in retained:
        row = by_id[qid]
        for successor in row.get("successors") or []:
            successor = str(successor)
            if successor not in retained:
                blocked_successors.add(successor)

    loaded_completion_templates = sorted(retained)
    blocked_successors -= retained

    return {
        "schemaVersion": 1,
        "strategy": "template-suppression-completion-bridge",
        "directProfileWrites": False,
        "summary": {
            "legacyInventoryQuestCount": len(by_id),
            "retainedCompletionTemplateCount": len(loaded_completion_templates),
            "blockedLegacySuccessorCount": len(blocked_successors),
            "excludedRestartableCount": len(excluded_restartable),
            "delayedRecordCount": len(delayed_records),
            "staleRecordCount": len(stale_records),
            "unknownStatusCount": len(unknown_status),
        },
        "retainedCompletionQuestIds": loaded_completion_templates,
        "blockedLegacySuccessorIds": sorted(blocked_successors),
        "completedHistoryQuestIds": sorted(completed_history),
        "failedHistoryQuestIds": sorted(failed_history),
        "excludedRestartableQuestIds": sorted(excluded_restartable),
        "delayedLegacyRecordQuestIds": sorted(delayed_records),
        "staleLegacyRecordQuestIds": sorted(stale_records),
        "unknownStatusRecords": sorted(unknown_status, key=lambda row: row["questId"]),
        "nonLegacyProfileQuestIds": sorted(set(unknown_profile_quests)),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Build Admiral Trader legacy completion-bridge migration plan.")
    parser.add_argument("inventory", type=Path, help="Generated Admiral Trader legacy inventory JSON.")
    parser.add_argument("profile", type=Path, help="PMC profile JSON or minimal fixture containing Quests.")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    inventory = json.loads(args.inventory.read_text(encoding="utf-8-sig"))
    profile = json.loads(args.profile.read_text(encoding="utf-8-sig"))
    plan = build_plan(inventory, profile)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(plan, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 1 if plan["summary"]["unknownStatusCount"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
