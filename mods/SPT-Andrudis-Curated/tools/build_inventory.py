#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from collections import Counter, defaultdict
from pathlib import Path
from typing import Any, Iterable

VALID_DECISIONS = {"KEEP", "REWRITE", "MERGE", "DROP", "MIGRATION_ONLY"}


def iter_nodes(value: Any) -> Iterable[Any]:
    yield value
    if isinstance(value, dict):
        for child in value.values():
            yield from iter_nodes(child)
    elif isinstance(value, list):
        for child in value:
            yield from iter_nodes(child)


def scalar_list(value: Any) -> list[str]:
    if value is None:
        return []
    if isinstance(value, list):
        return [str(v) for v in value if v is not None]
    return [str(value)]


def as_number(value: Any) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def bundle_from_path(path: Path, root: Path) -> str:
    rel = path.relative_to(root)
    return rel.parts[0] if len(rel.parts) > 1 else path.parent.name


def load_rules(path: Path | None) -> dict[str, Any]:
    if path is None:
        return {}
    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        raise ValueError("rules file must contain a JSON object")
    return raw


def match_rule(quest: dict[str, Any], bundle: str, rules: dict[str, Any]) -> tuple[str | None, str | None]:
    for rule in rules.get("rules", []):
        decision = rule.get("decision")
        if decision not in VALID_DECISIONS:
            raise ValueError(f"invalid decision in rules: {decision!r}")
        match = rule.get("match", {})
        bundle_equals = match.get("bundleEquals")
        bundle_contains = match.get("bundleContains")
        name_contains = match.get("questNameContains")
        if bundle_equals is not None and bundle != bundle_equals:
            continue
        if bundle_contains is not None and str(bundle_contains).lower() not in bundle.lower():
            continue
        quest_name = str(quest.get("QuestName") or quest.get("name") or "")
        if name_contains is not None and str(name_contains).lower() not in quest_name.lower():
            continue
        return decision, rule.get("reason")

    default = rules.get("defaultDecision")
    if default is not None:
        if default not in VALID_DECISIONS:
            raise ValueError(f"invalid defaultDecision: {default!r}")
        return default, rules.get("defaultReason")
    return None, None


def extract_prerequisites(quest: dict[str, Any]) -> list[str]:
    result: set[str] = set()
    start = (quest.get("conditions") or {}).get("AvailableForStart") or []
    for node in iter_nodes(start):
        if not isinstance(node, dict):
            continue
        condition_type = str(node.get("conditionType") or node.get("_parent") or "").lower()
        if condition_type == "quest":
            result.update(scalar_list(node.get("target")))
    return sorted(result)


def extract_objective_summary(quest: dict[str, Any]) -> dict[str, Any]:
    finish = (quest.get("conditions") or {}).get("AvailableForFinish") or []
    condition_types: Counter[str] = Counter()
    objective_types: Counter[str] = Counter()
    kill_roles: set[str] = set()
    weapons: set[str] = set()
    equipment: set[str] = set()
    targets: set[str] = set()
    fir = False
    objective_values: list[float] = []

    for node in iter_nodes(finish):
        if not isinstance(node, dict):
            continue
        condition_type = node.get("conditionType")
        objective_type = node.get("type")
        if condition_type:
            condition_types[str(condition_type)] += 1
        if objective_type:
            objective_types[str(objective_type)] += 1
        if node.get("onlyFoundInRaid") is True:
            fir = True
        kill_roles.update(scalar_list(node.get("savageRole")))
        targets.update(scalar_list(node.get("target")))
        for key in ("weapon", "weaponModsInclusive", "weaponModsExclusive"):
            weapons.update(scalar_list(node.get(key)))
        for key in ("equipmentInclusive", "equipmentExclusive"):
            equipment.update(scalar_list(node.get(key)))
        if "value" in node:
            number = as_number(node.get("value"))
            if number:
                objective_values.append(number)

    return {
        "conditionTypes": dict(sorted(condition_types.items())),
        "objectiveTypes": dict(sorted(objective_types.items())),
        "firRequired": fir,
        "killRoles": sorted(kill_roles),
        "weaponIds": sorted(weapons),
        "equipmentIds": sorted(equipment),
        "targets": sorted(targets),
        "maxObjectiveValue": max(objective_values, default=0),
    }


def extract_rewards(quest: dict[str, Any]) -> dict[str, Any]:
    rewards = (quest.get("rewards") or {}).get("Success") or []
    reward_types: Counter[str] = Counter()
    item_rewards: list[dict[str, Any]] = []
    experience = 0.0
    standing = 0.0
    unlocks = 0

    for reward in rewards:
        if not isinstance(reward, dict):
            continue
        reward_type = str(reward.get("type") or "Unknown")
        reward_types[reward_type] += 1
        if reward_type == "Experience":
            experience += as_number(reward.get("value"))
        elif reward_type in {"TraderStanding", "TraderStandingRestore"}:
            standing += as_number(reward.get("value"))
        elif reward_type in {"AssortmentUnlock", "UnlockTrader"}:
            unlocks += 1
        if reward_type == "Item":
            for item in reward.get("items") or []:
                if not isinstance(item, dict):
                    continue
                update = item.get("upd") or {}
                item_rewards.append({
                    "tpl": item.get("_tpl"),
                    "count": update.get("StackObjectsCount", 1),
                })

    item_rewards.sort(key=lambda item: (str(item.get("tpl")), str(item.get("count"))))
    return {
        "types": dict(sorted(reward_types.items())),
        "experience": experience,
        "traderStanding": standing,
        "unlockCount": unlocks,
        "items": item_rewards,
    }


def load_quests(root: Path) -> tuple[list[dict[str, Any]], list[dict[str, str]]]:
    rows: list[dict[str, Any]] = []
    errors: list[dict[str, str]] = []

    for path in sorted(root.rglob("quests.json"), key=lambda value: value.as_posix().lower()):
        try:
            data = json.loads(path.read_text(encoding="utf-8-sig"))
        except Exception as exc:  # surfaced in deterministic report
            errors.append({"path": path.as_posix(), "error": str(exc)})
            continue
        if not isinstance(data, dict):
            errors.append({"path": path.as_posix(), "error": "root JSON value is not an object"})
            continue

        bundle = bundle_from_path(path, root)
        for key, quest in sorted(data.items(), key=lambda pair: str(pair[0])):
            if not isinstance(quest, dict):
                errors.append({"path": path.as_posix(), "error": f"quest {key} is not an object"})
                continue
            quest_id = str(quest.get("_id") or key)
            rows.append({
                "questId": quest_id,
                "questName": quest.get("QuestName") or quest.get("name") or quest_id,
                "bundle": bundle,
                "sourcePath": path.relative_to(root).as_posix(),
                "legacyTraderId": quest.get("traderId"),
                "questType": quest.get("type"),
                "location": quest.get("location"),
                "restartable": bool(quest.get("restartable", False)),
                "prerequisites": extract_prerequisites(quest),
                "objectives": extract_objective_summary(quest),
                "rewards": extract_rewards(quest),
                "_raw": quest,
            })

    return rows, errors


def add_successors(rows: list[dict[str, Any]]) -> None:
    successors: dict[str, set[str]] = defaultdict(set)
    for row in rows:
        for prerequisite in row["prerequisites"]:
            successors[prerequisite].add(row["questId"])
    for row in rows:
        row["successors"] = sorted(successors.get(row["questId"], set()))


def find_cycles(graph: dict[str, list[str]]) -> list[list[str]]:
    index = 0
    stack: list[str] = []
    on_stack: set[str] = set()
    indexes: dict[str, int] = {}
    lowlinks: dict[str, int] = {}
    cycles: list[list[str]] = []

    def strong_connect(node: str) -> None:
        nonlocal index
        indexes[node] = index
        lowlinks[node] = index
        index += 1
        stack.append(node)
        on_stack.add(node)

        for successor in graph.get(node, []):
            if successor not in indexes:
                strong_connect(successor)
                lowlinks[node] = min(lowlinks[node], lowlinks[successor])
            elif successor in on_stack:
                lowlinks[node] = min(lowlinks[node], indexes[successor])

        if lowlinks[node] != indexes[node]:
            return

        component: list[str] = []
        while stack:
            member = stack.pop()
            on_stack.remove(member)
            component.append(member)
            if member == node:
                break
        if len(component) > 1 or (len(component) == 1 and node in graph.get(node, [])):
            cycles.append(sorted(component))

    for node in sorted(graph):
        if node not in indexes:
            strong_connect(node)

    cycles.sort()
    return cycles


def build_graph_diagnostics(rows: list[dict[str, Any]]) -> dict[str, Any]:
    rows_by_id: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for row in rows:
        rows_by_id[row["questId"]].append(row)

    duplicates = [
        {
            "questId": quest_id,
            "sources": sorted({entry["sourcePath"] for entry in entries}),
        }
        for quest_id, entries in sorted(rows_by_id.items())
        if len(entries) > 1
    ]

    unique_rows = {quest_id: entries[0] for quest_id, entries in rows_by_id.items()}
    known_ids = set(unique_rows)
    missing: list[dict[str, str]] = []
    cross_bundle: list[dict[str, str]] = []

    for row in sorted(rows, key=lambda value: value["questId"]):
        for prerequisite in row["prerequisites"]:
            if prerequisite not in known_ids:
                missing.append({
                    "questId": row["questId"],
                    "bundle": row["bundle"],
                    "missingPrerequisiteId": prerequisite,
                })
                continue
            prerequisite_row = unique_rows[prerequisite]
            if prerequisite_row["bundle"] != row["bundle"]:
                cross_bundle.append({
                    "fromQuestId": prerequisite,
                    "fromBundle": prerequisite_row["bundle"],
                    "toQuestId": row["questId"],
                    "toBundle": row["bundle"],
                })

    graph = {
        quest_id: [successor for successor in row["successors"] if successor in known_ids]
        for quest_id, row in unique_rows.items()
    }
    cycles = find_cycles(graph)

    return {
        "duplicateQuestIds": duplicates,
        "missingPrerequisites": missing,
        "crossBundleEdges": cross_bundle,
        "cycles": cycles,
    }


def build_payload(root: Path, rules: dict[str, Any]) -> dict[str, Any]:
    rows, errors = load_quests(root)
    add_successors(rows)

    decision_counts: Counter[str] = Counter()
    bundle_counts: Counter[str] = Counter()
    trader_counts: Counter[str] = Counter()
    fir_count = 0
    restartable_count = 0

    for row in rows:
        decision, reason = match_rule(row["_raw"], row["bundle"], rules)
        row["curationDecision"] = decision
        row["curationReason"] = reason
        row.pop("_raw", None)
        bundle_counts[row["bundle"]] += 1
        trader_counts[str(row["legacyTraderId"])] += 1
        if decision:
            decision_counts[decision] += 1
        if row["objectives"]["firRequired"]:
            fir_count += 1
        if row["restartable"]:
            restartable_count += 1

    rows.sort(key=lambda row: (str(row["bundle"]).lower(), str(row["questName"]).lower(), row["questId"]))
    diagnostics = build_graph_diagnostics(rows)

    return {
        "schemaVersion": 2,
        "sourceRoot": root.resolve().as_posix(),
        "summary": {
            "questCount": len(rows),
            "bundleCounts": dict(sorted(bundle_counts.items())),
            "legacyTraderCounts": dict(sorted(trader_counts.items())),
            "decisionCounts": dict(sorted(decision_counts.items())),
            "unclassifiedCount": sum(1 for row in rows if row["curationDecision"] is None),
            "firQuestCount": fir_count,
            "restartableQuestCount": restartable_count,
            "parseErrorCount": len(errors),
            "duplicateQuestIdCount": len(diagnostics["duplicateQuestIds"]),
            "missingPrerequisiteCount": len(diagnostics["missingPrerequisites"]),
            "crossBundleEdgeCount": len(diagnostics["crossBundleEdges"]),
            "cycleCount": len(diagnostics["cycles"]),
        },
        "parseErrors": errors,
        "graphDiagnostics": diagnostics,
        "quests": rows,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Build deterministic Andrudis quest inventory/graph.")
    parser.add_argument("source_root", type=Path, help="Path to legacy db/QuestBundles or converted equivalent.")
    parser.add_argument("--rules", type=Path, default=None, help="Optional curation rules JSON.")
    parser.add_argument("--output", type=Path, required=True, help="Output JSON path.")
    args = parser.parse_args()

    root = args.source_root.resolve()
    if not root.is_dir():
        parser.error(f"source_root is not a directory: {root}")

    payload = build_payload(root, load_rules(args.rules))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 1 if payload["parseErrors"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
