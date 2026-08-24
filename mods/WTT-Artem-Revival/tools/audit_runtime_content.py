#!/usr/bin/env python3
"""Conservative Artem runtime audit.

Reads an imported/repaired Resources tree and reports economy/content ownership
without mutating runtime data. The classification is intentionally conservative:
quest-referenced custom items are campaign-required, other trader-root custom
items are core trader catalog, and unreferenced custom items are only candidates
for orphan/stale cleanup until runtime/package validation is complete.
"""

from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path

ROUBLES_TPL = "5449016a4bdc2d6f028b456f"


def load(path: Path):
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def item_name(item: dict) -> str:
    return item.get("locales", {}).get("en", {}).get("name", "<unnamed>")


def collect_custom_items(resources: Path):
    result = {}
    for path in sorted((resources / "db/CustomItems").glob("*.json")):
        for tpl, item in load(path).items():
            result[tpl] = {"source": path.name, "name": item_name(item), "data": item}
    return result


def collect_string_refs(value, known: set[str], output: Counter):
    if isinstance(value, dict):
        for child in value.values():
            collect_string_refs(child, known, output)
    elif isinstance(value, list):
        for child in value:
            collect_string_refs(child, known, output)
    elif isinstance(value, str) and value in known:
        output[value] += 1


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("resources", type=Path, help="Imported/repaired Artem Resources directory")
    parser.add_argument("--json", dest="json_output", type=Path)
    args = parser.parse_args()

    resources = args.resources.resolve()
    if not (resources / "db/assort.json").is_file():
        raise SystemExit(f"not an Artem Resources tree: {resources}")

    custom = collect_custom_items(resources)
    custom_tpls = set(custom)
    assort = load(resources / "db/assort.json")
    roots = [item for item in assort["items"] if item.get("parentId") == "hideout"]
    trader_custom = {item["_tpl"] for item in roots if item.get("_tpl") in custom_tpls}

    quest_files = sorted((resources / "db/CustomQuests").glob("*/Quests/*.json"))
    quest_refs = Counter()
    quests = {}
    for path in quest_files:
        payload = load(path)
        quests.update(payload)
        collect_string_refs(payload, custom_tpls, quest_refs)
    campaign = set(quest_refs)

    orphan_candidates = custom_tpls - trader_custom - campaign
    trader_only = trader_custom - campaign

    rouble_prices = []
    barter_custom = []
    ll_counter = Counter()
    for root in roots:
        oid = root["_id"]
        ll_counter[assort["loyal_level_items"].get(oid)] += 1
        if root.get("_tpl") not in custom_tpls:
            continue
        alternatives = assort["barter_scheme"].get(oid, [])
        rouble_price = None
        for alternative in alternatives:
            for cost in alternative:
                if cost.get("_tpl") == ROUBLES_TPL:
                    rouble_price = cost.get("count")
                    break
            if rouble_price is not None:
                break
        if rouble_price is None:
            barter_custom.append(oid)
        else:
            rouble_prices.append(int(rouble_price))

    reward_types = Counter()
    duplicate_xp = []
    multi_standing = []
    for quest in quests.values():
        success = quest.get("rewards", {}).get("Success", [])
        for reward in success:
            reward_types[reward.get("type")] += 1
        xp = [reward for reward in success if reward.get("type") == "Experience"]
        standing = [reward for reward in success if reward.get("type") == "TraderStanding"]
        if len(xp) > 1:
            duplicate_xp.append({"quest": quest.get("QuestName"), "values": [r.get("value") for r in xp]})
        if len(standing) > 1:
            multi_standing.append(
                {
                    "quest": quest.get("QuestName"),
                    "rewards": [{"target": r.get("target"), "value": r.get("value")} for r in standing],
                }
            )

    report = {
        "custom_items": len(custom),
        "campaign_required_custom_items": len(campaign),
        "core_trader_catalog_custom_items": len(trader_only),
        "orphan_stale_candidates": [
            {"tpl": tpl, "name": custom[tpl]["name"], "source": custom[tpl]["source"]}
            for tpl in sorted(orphan_candidates)
        ],
        "root_offers": len(roots),
        "custom_root_offers": len(trader_custom),
        "custom_rouble_offers": len(rouble_prices),
        "custom_barter_offers": len(barter_custom),
        "custom_rouble_price_min": min(rouble_prices) if rouble_prices else None,
        "custom_rouble_price_max": max(rouble_prices) if rouble_prices else None,
        "loyalty_level_distribution": dict(sorted((str(k), v) for k, v in ll_counter.items())),
        "quest_count": len(quests),
        "success_reward_types": dict(sorted(reward_types.items())),
        "duplicate_experience_reward_quests": duplicate_xp,
        "multi_trader_standing_reward_quests": multi_standing,
    }

    print(json.dumps(report, indent=2, ensure_ascii=False))
    if args.json_output:
        args.json_output.parent.mkdir(parents=True, exist_ok=True)
        with args.json_output.open("w", encoding="utf-8", newline="\n") as handle:
            json.dump(report, handle, indent=2, ensure_ascii=False)
            handle.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
