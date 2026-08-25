#!/usr/bin/env python3
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
from typing import Any

ADMIRAL_TRADER_ID = "d5c27bb3169f8dfbc13f6b69"
BLOCKED_UNLOCK_REWARD_TYPES = {"AssortmentUnlock", "UnlockTrader"}
STANDING_REWARD_TYPES = {"TraderStanding", "TraderStandingRestore"}


def quest_map_from_file(path: Path) -> dict[str, dict[str, Any]]:
    raw = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(raw, dict):
        raise ValueError(f"quest source is not an object: {path}")
    result: dict[str, dict[str, Any]] = {}
    for key, value in raw.items():
        if not isinstance(value, dict):
            continue
        qid = str(value.get("_id") or key)
        result[qid] = value
    return result


def source_template(
    row: dict[str, Any],
    source_root: Path,
    cache: dict[str, dict[str, dict[str, Any]]],
) -> dict[str, Any]:
    source_path = str(row.get("sourcePath") or "")
    if not source_path:
        raise ValueError(f"inventory row {row.get('questId')} has no sourcePath")
    if source_path not in cache:
        path = source_root / source_path
        if not path.is_file():
            raise FileNotFoundError(f"legacy quest source missing: {path}")
        cache[source_path] = quest_map_from_file(path)
    qid = str(row["questId"])
    template = cache[source_path].get(qid)
    if template is None:
        raise KeyError(f"quest {qid} not found in {source_path}")
    return copy.deepcopy(template)


def remap_rewards(template: dict[str, Any]) -> list[dict[str, Any]]:
    blocked: list[dict[str, Any]] = []
    rewards = template.get("rewards")
    if not isinstance(rewards, dict):
        return blocked

    for phase, entries in list(rewards.items()):
        if not isinstance(entries, list):
            continue
        kept: list[Any] = []
        for reward in entries:
            if not isinstance(reward, dict):
                kept.append(reward)
                continue
            reward_type = str(reward.get("type") or "")
            if reward_type in BLOCKED_UNLOCK_REWARD_TYPES:
                blocked.append({
                    "phase": str(phase),
                    "type": reward_type,
                    "target": reward.get("target"),
                    "id": reward.get("id"),
                })
                continue
            if reward_type in STANDING_REWARD_TYPES:
                reward["target"] = ADMIRAL_TRADER_ID
            kept.append(reward)
        rewards[phase] = kept
    return blocked


def closed_start_condition(qid: str) -> dict[str, Any]:
    condition_id = hashlib.sha256(f"admiral-completion-bridge:{qid}".encode()).hexdigest()[:24]
    return {
        "id": condition_id,
        "conditionType": "Level",
        "compareMethod": ">=",
        "value": 999,
        "dynamicLocale": False,
    }


def prepare_template(template: dict[str, Any], qid: str, completion_bridge: bool) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    template["_id"] = qid
    template["traderId"] = ADMIRAL_TRADER_ID
    blocked = remap_rewards(template)
    if completion_bridge:
        conditions = template.setdefault("conditions", {})
        if not isinstance(conditions, dict):
            conditions = {}
            template["conditions"] = conditions
        conditions["AvailableForStart"] = [closed_start_condition(qid)]
        template["restartable"] = False
    return template, blocked


def build_materialization(
    inventory: dict[str, Any],
    source_root: Path,
    migration_plan: dict[str, Any] | None = None,
) -> dict[str, Any]:
    rows = [row for row in inventory.get("quests", []) if isinstance(row, dict)]
    by_id = {str(row.get("questId")): row for row in rows if row.get("questId") is not None}
    cache: dict[str, dict[str, dict[str, Any]]] = {}

    keep_ids = sorted(
        qid for qid, row in by_id.items()
        if row.get("curationDecision") == "KEEP"
    )
    keep_set = set(keep_ids)
    curated_templates: dict[str, dict[str, Any]] = {}
    completion_templates: dict[str, dict[str, Any]] = {}
    blocked_unlocks: list[dict[str, Any]] = []
    external_prerequisites: list[dict[str, str]] = []

    for qid in keep_ids:
        row = by_id[qid]
        template = source_template(row, source_root, cache)
        prepared, blocked = prepare_template(template, qid, completion_bridge=False)
        curated_templates[qid] = prepared
        for reward in blocked:
            blocked_unlocks.append({"questId": qid, "scope": "curated", **reward})
        for prerequisite in row.get("prerequisites") or []:
            prerequisite = str(prerequisite)
            if prerequisite not in keep_set:
                external_prerequisites.append({"questId": qid, "prerequisiteQuestId": prerequisite})

    retained_ids: list[str] = []
    blocked_successors: list[str] = []
    if migration_plan is not None:
        retained_ids = sorted(str(qid) for qid in migration_plan.get("retainedCompletionQuestIds", []))
        blocked_successors = sorted(str(qid) for qid in migration_plan.get("blockedLegacySuccessorIds", []))
        for qid in retained_ids:
            row = by_id.get(qid)
            if row is None:
                raise KeyError(f"migration plan references quest absent from inventory: {qid}")
            if bool(row.get("restartable", False)):
                raise ValueError(f"restartable quest cannot enter completion bridge: {qid}")
            template = source_template(row, source_root, cache)
            prepared, blocked = prepare_template(template, qid, completion_bridge=True)
            completion_templates[qid] = prepared
            for reward in blocked:
                blocked_unlocks.append({"questId": qid, "scope": "completion", **reward})

    accidental_decisions = sorted(
        qid for qid in curated_templates
        if by_id[qid].get("curationDecision") in {"DROP", "MIGRATION_ONLY"}
    )

    return {
        "schemaVersion": 1,
        "traderId": ADMIRAL_TRADER_ID,
        "policy": {
            "curatedSourceDecision": "KEEP-only",
            "completionBridge": "active-non-restartable-profile-only",
            "directProfileWrites": False,
            "legacyUnlockRewards": "blocked",
            "legacyTraderStanding": "remap-to-admiral",
            "completionStartGate": "level-999-closed-start",
        },
        "summary": {
            "curatedTemplateCount": len(curated_templates),
            "completionTemplateCount": len(completion_templates),
            "blockedLegacyUnlockRewardCount": len(blocked_unlocks),
            "externalCuratedPrerequisiteCount": len(external_prerequisites),
            "blockedLegacySuccessorCount": len(blocked_successors),
            "accidentalDeprecatedTemplateCount": len(accidental_decisions),
        },
        "curatedTemplates": curated_templates,
        "completionBridgeTemplates": completion_templates,
        "blockedLegacySuccessorIds": blocked_successors,
        "diagnostics": {
            "blockedLegacyUnlockRewards": blocked_unlocks,
            "externalCuratedPrerequisites": external_prerequisites,
            "accidentalDeprecatedTemplateIds": accidental_decisions,
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Materialize Admiral Trader curated and profile-scoped completion quest templates.")
    parser.add_argument("inventory", type=Path)
    parser.add_argument("source_root", type=Path)
    parser.add_argument("--migration-plan", type=Path, default=None)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    inventory = json.loads(args.inventory.read_text(encoding="utf-8-sig"))
    migration_plan = None
    if args.migration_plan is not None:
        migration_plan = json.loads(args.migration_plan.read_text(encoding="utf-8-sig"))

    payload = build_materialization(inventory, args.source_root, migration_plan)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

    summary = payload["summary"]
    if summary["accidentalDeprecatedTemplateCount"]:
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
