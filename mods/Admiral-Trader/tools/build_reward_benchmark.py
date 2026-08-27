#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
import math
from collections import Counter, defaultdict
from pathlib import Path
from statistics import median
from typing import Any, Iterable

CURRENCY_TEMPLATES = {
    "5449016a4bdc2d6f028b456f": "RUB",
    "5696686a4bdc2da3298b456a": "USD",
    "569668774bdc2da2298b4568": "EUR",
}


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
    assortment_unlocks = 0
    trader_unlocks = 0
    production_unlocks = 0
    physical_item_records = 0
    physical_item_units = 0.0
    reward_types: Counter[str] = Counter()
    physical_templates: Counter[str] = Counter()
    currencies: Counter[str] = Counter()

    for reward in rewards:
        if not isinstance(reward, dict):
            continue
        reward_type = str(reward.get("type") or "Unknown")
        reward_types[reward_type] += 1
        if reward_type == "Experience":
            xp += number(reward.get("value"))
        elif reward_type in {"TraderStanding", "TraderStandingRestore"}:
            standing += number(reward.get("value"))
        elif reward_type == "AssortmentUnlock":
            assortment_unlocks += 1
        elif reward_type in {"TraderUnlock", "UnlockTrader"}:
            trader_unlocks += 1
        elif reward_type == "ProductionScheme":
            production_unlocks += 1
        elif reward_type == "Item":
            for item in scalar_values(reward.get("items")):
                if not isinstance(item, dict):
                    continue
                tpl = str(item.get("_tpl") or "UNKNOWN")
                upd = item.get("upd") or {}
                count = number(upd.get("StackObjectsCount", 1)) or 1
                currency = CURRENCY_TEMPLATES.get(tpl)
                if currency:
                    currencies[currency] += count
                    continue
                physical_item_records += 1
                physical_item_units += count
                physical_templates[tpl] += count

    return {
        "xp": xp,
        "standing": standing,
        "unlocks": assortment_unlocks + trader_unlocks + production_unlocks,
        "assortmentUnlocks": assortment_unlocks,
        "traderUnlocks": trader_unlocks,
        "productionUnlocks": production_unlocks,
        "physicalItemRecords": physical_item_records,
        "physicalItemUnits": physical_item_units,
        "currencies": currencies,
        "rewardTypes": reward_types,
        "physicalTemplates": physical_templates,
    }


def new_group() -> dict[str, Any]:
    return {
        "questCount": 0,
        "xp": [],
        "standing": [],
        "physicalItemRecords": [],
        "physicalItemUnits": [],
        "unlocks": [],
        "RUB": [],
        "USD": [],
        "EUR": [],
    }


def add_to_group(group: dict[str, Any], summary: dict[str, Any]) -> None:
    group["questCount"] += 1
    group["xp"].append(summary["xp"])
    group["standing"].append(summary["standing"])
    group["physicalItemRecords"].append(float(summary["physicalItemRecords"]))
    group["physicalItemUnits"].append(summary["physicalItemUnits"])
    group["unlocks"].append(float(summary["unlocks"]))
    for currency in ("RUB", "USD", "EUR"):
        group[currency].append(float(summary["currencies"].get(currency, 0)))


def group_payload(group: dict[str, Any]) -> dict[str, Any]:
    return {
        "questCount": group["questCount"],
        "xp": distribution(group["xp"]),
        "standing": distribution(group["standing"]),
        "physicalItemRecords": distribution(group["physicalItemRecords"]),
        "physicalItemUnits": distribution(group["physicalItemUnits"]),
        "unlocks": distribution(group["unlocks"]),
        "currency": {
            currency: distribution(group[currency])
            for currency in ("RUB", "USD", "EUR")
        },
    }


def build_benchmark(raw: Any) -> dict[str, Any]:
    quests = list(iter_quests(raw))
    overall = new_group()
    quest_types: Counter[str] = Counter()
    reward_types: Counter[str] = Counter()
    physical_templates: Counter[str] = Counter()
    level_groups: dict[str, dict[str, Any]] = defaultdict(new_group)
    type_groups: dict[str, dict[str, Any]] = defaultdict(new_group)

    for quest in quests:
        summary = reward_summary(quest)
        quest_type = str(quest.get("type") or "Unknown")
        bucket = level_bucket(minimum_level(quest))
        quest_types[quest_type] += 1
        reward_types.update(summary["rewardTypes"])
        physical_templates.update(summary["physicalTemplates"])
        add_to_group(overall, summary)
        add_to_group(level_groups[bucket], summary)
        add_to_group(type_groups[quest_type], summary)

    result = group_payload(overall)
    result["questTypes"] = dict(sorted(quest_types.items()))
    result["rewardTypes"] = dict(sorted(reward_types.items()))
    result["topPhysicalRewardTemplates"] = [
        {"tpl": tpl, "units": units}
        for tpl, units in physical_templates.most_common(50)
    ]

    return {
        "schemaVersion": 2,
        "method": "descriptive-vanilla-reward-distribution",
        "notes": [
            "Currency stacks are separated from physical item rewards and are not cross-converted between RUB/USD/EUR.",
            "Explicit minimum-level buckets are not equivalent to true progression tier because many chained quests have no direct level gate.",
            "Difficulty/time/risk/repeatability weighting is intentionally separate from this descriptive vanilla distribution.",
        ],
        "summary": result,
        "levelBuckets": {name: group_payload(level_groups[name]) for name in sorted(level_groups)},
        "questTypeBuckets": {name: group_payload(type_groups[name]) for name in sorted(type_groups)},
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
