#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from statistics import median
from typing import Any, Iterable


def scalar_values(value: Any) -> list[Any]:
    if value is None:
        return []
    return value if isinstance(value, list) else [value]


def number(value: Any) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return 0.0


def iter_quests(value: Any) -> Iterable[dict[str, Any]]:
    if isinstance(value, dict):
        if "_id" in value and ("conditions" in value or "rewards" in value):
            yield value
            return
        for child in value.values():
            yield from iter_quests(child)
    elif isinstance(value, list):
        for child in value:
            yield from iter_quests(child)


def percentile(values: list[float], p: float) -> float:
    if not values:
        return 0.0
    ordered = sorted(values)
    if len(ordered) == 1:
        return ordered[0]
    position = (len(ordered) - 1) * p
    lower = math.floor(position)
    upper = math.ceil(position)
    if lower == upper:
        return ordered[lower]
    fraction = position - lower
    return ordered[lower] * (1 - fraction) + ordered[upper] * fraction


def distribution(values: list[float]) -> dict[str, float | int]:
    if not values:
        return {"count": 0, "min": 0.0, "p25": 0.0, "median": 0.0, "p75": 0.0, "p90": 0.0, "max": 0.0}
    return {
        "count": len(values),
        "min": min(values),
        "p25": percentile(values, 0.25),
        "median": median(values),
        "p75": percentile(values, 0.75),
        "p90": percentile(values, 0.90),
        "max": max(values),
    }


def minimum_level(quest: dict[str, Any]) -> int:
    result = 1
    start = ((quest.get("conditions") or {}).get("AvailableForStart") or [])
    for condition in start:
        if not isinstance(condition, dict) or condition.get("conditionType") != "Level":
            continue
        compare = condition.get("compareMethod")
        value = int(number(condition.get("value")))
        if compare == ">":
            value += 1
        if compare in {">", ">=", "="}:
            result = max(result, value)
    return result


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


def reward_summary(quest: dict[str, Any]) -> dict[str, Any]:
    rewards = ((quest.get("rewards") or {}).get("Success") or [])
    xp = 0.0
    standing = 0.0
    unlocks = 0
    item_records = 0
    item_units = 0.0
    reward_types: Counter[str] = Counter()
    item_templates: Counter[str] = Counter()

    for reward in rewards:
        if not isinstance(reward, dict):
            continue
        reward_type = str(reward.get("type") or "Unknown")
        reward_types[reward_type] += 1
        if reward_type == "Experience":
            xp += number(reward.get("value"))
        elif reward_type in {"TraderStanding", "TraderStandingRestore"}:
            standing += number(reward.get("value"))
        elif reward_type in {"AssortmentUnlock", "UnlockTrader"}:
            unlocks += 1
        elif reward_type == "Item":
            for item in scalar_values(reward.get("items")):
                if not isinstance(item, dict):
                    continue
                item_records += 1
                tpl = str(item.get("_tpl") or "UNKNOWN")
                upd = item.get("upd") or {}
                count = number(upd.get("StackObjectsCount", 1)) or 1
                item_units += count
                item_templates[tpl] += count

    return {
        "xp": xp,
        "standing": standing,
        "unlocks": unlocks,
        "itemRecords": item_records,
        "itemUnits": item_units,
        "rewardTypes": reward_types,
        "itemTemplates": item_templates,
    }


def build_benchmark(raw: Any) -> dict[str, Any]:
    quests = list(iter_quests(raw))
    xp_values: list[float] = []
    standing_values: list[float] = []
    item_record_values: list[float] = []
    item_unit_values: list[float] = []
    unlock_values: list[float] = []
    quest_types: Counter[str] = Counter()
    reward_types: Counter[str] = Counter()
    item_templates: Counter[str] = Counter()
    buckets: dict[str, dict[str, Any]] = defaultdict(lambda: {
        "questCount": 0,
        "xp": [],
        "standing": [],
        "itemRecords": [],
        "itemUnits": [],
        "unlocks": [],
    })

    for quest in quests:
        summary = reward_summary(quest)
        level = minimum_level(quest)
        bucket = level_bucket(level)
        quest_types[str(quest.get("type") or "Unknown")] += 1
        reward_types.update(summary["rewardTypes"])
        item_templates.update(summary["itemTemplates"])

        xp_values.append(summary["xp"])
        standing_values.append(summary["standing"])
        item_record_values.append(float(summary["itemRecords"]))
        item_unit_values.append(summary["itemUnits"])
        unlock_values.append(float(summary["unlocks"]))

        group = buckets[bucket]
        group["questCount"] += 1
        group["xp"].append(summary["xp"])
        group["standing"].append(summary["standing"])
        group["itemRecords"].append(float(summary["itemRecords"]))
        group["itemUnits"].append(summary["itemUnits"])
        group["unlocks"].append(float(summary["unlocks"]))

    bucket_payload: dict[str, Any] = {}
    for name in sorted(buckets):
        group = buckets[name]
        bucket_payload[name] = {
            "questCount": group["questCount"],
            "xp": distribution(group["xp"]),
            "standing": distribution(group["standing"]),
            "itemRecords": distribution(group["itemRecords"]),
            "itemUnits": distribution(group["itemUnits"]),
            "unlocks": distribution(group["unlocks"]),
        }

    return {
        "schemaVersion": 1,
        "method": "descriptive-vanilla-reward-distribution",
        "notes": [
            "Currency and item values are intentionally not converted to rubles without a separately supplied price/rate baseline.",
            "Difficulty/time/risk/repeatability weighting is intentionally separate from this descriptive vanilla distribution.",
        ],
        "summary": {
            "questCount": len(quests),
            "questTypes": dict(sorted(quest_types.items())),
            "rewardTypes": dict(sorted(reward_types.items())),
            "xp": distribution(xp_values),
            "standing": distribution(standing_values),
            "itemRecords": distribution(item_record_values),
            "itemUnits": distribution(item_unit_values),
            "unlocks": distribution(unlock_values),
            "topRewardItemTemplates": [
                {"tpl": tpl, "units": units}
                for tpl, units in item_templates.most_common(50)
            ],
        },
        "levelBuckets": bucket_payload,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a descriptive vanilla quest reward benchmark for Admiral Trader.")
    parser.add_argument("source", type=Path, help="Vanilla quest JSON file.")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    raw = json.loads(args.source.read_text(encoding="utf-8-sig"))
    payload = build_benchmark(raw)
    if payload["summary"]["questCount"] == 0:
        parser.error("source contains no recognizable quest records")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
