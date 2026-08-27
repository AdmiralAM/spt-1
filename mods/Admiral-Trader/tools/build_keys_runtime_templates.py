#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

TRADER_ID = "d5c27bb3169f8dfbc13f6b69"
RUB_TPL = "5449016a4bdc2d6f028b456f"
QUEST_ICON = "/files/quest/icon/5a29222486f77456f50d09e7.jpg"


def mongo_id(namespace: str) -> str:
    return hashlib.sha256(namespace.encode()).hexdigest()[:24]


def load_source_quests(path: Path) -> dict[str, dict[str, Any]]:
    raw = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(raw, dict):
        raise ValueError(f"legacy quest source is not an object: {path}")
    return {str(value.get("_id") or key): value for key, value in raw.items() if isinstance(value, dict)}


def find_item_targets(template: dict[str, Any]) -> set[str]:
    result: set[str] = set()
    conditions = (template.get("conditions") or {}).get("AvailableForFinish") or []
    for condition in conditions:
        if not isinstance(condition, dict) or condition.get("conditionType") != "FindItem":
            continue
        target = condition.get("target")
        if isinstance(target, list):
            result.update(str(value) for value in target if value)
        elif target:
            result.add(str(target))
    return result


def source_key_pool(group: dict[str, Any], inventory_by_id: dict[str, dict[str, Any]], source_root: Path, cache: dict[str, dict[str, dict[str, Any]]], *, allow_empty: bool = False) -> list[str]:
    pool: set[str] = set()
    for source_id in group.get("sourceQuestIds") or []:
        qid = str(source_id)
        row = inventory_by_id.get(qid)
        if row is None:
            raise KeyError(f"curated group references quest absent from inventory: {qid}")
        source_path = str(row.get("sourcePath") or "")
        if not source_path:
            raise ValueError(f"inventory quest {qid} has no sourcePath")
        if source_path not in cache:
            path = source_root / source_path
            if not path.is_file():
                raise FileNotFoundError(path)
            cache[source_path] = load_source_quests(path)
        template = cache[source_path].get(qid)
        if template is None:
            raise KeyError(f"quest {qid} absent from {source_path}")
        pool.update(find_item_targets(template))
    if not pool and not allow_empty:
        raise ValueError(f"curated group {group.get('id')} produced an empty key pool")
    return sorted(pool)


def bounded_key_pool(quest: dict[str, Any], source_pool: list[str]) -> list[str]:
    objective = quest.get("objective") or {}
    explicit_targets = objective.get("explicitTargets")
    if explicit_targets is not None:
        pool = [str(value) for value in explicit_targets if value]
    else:
        pool = list(source_pool)
    representative_count = int(objective.get("representativeCount", 1))
    maximum_size = int(objective.get("maximumTargetPoolSize", 0))
    if maximum_size <= 0:
        raise ValueError(f"authored quest {quest.get('slug')} must define a positive maximumTargetPoolSize")
    if maximum_size < representative_count:
        raise ValueError(f"authored quest {quest.get('slug')} maximumTargetPoolSize {maximum_size} is below representativeCount {representative_count}")
    if len(pool) < representative_count:
        raise ValueError(f"authored quest {quest.get('slug')} source pool has {len(pool)} targets, below representativeCount {representative_count}")
    return pool[:maximum_size]


def condition_id(slug: str, role: str, index: int = 0) -> str:
    return mongo_id(f"admiral-keys:{slug}:condition:{role}:{index}")


def reward_id(slug: str, role: str) -> str:
    return mongo_id(f"admiral-keys:{slug}:reward:{role}")


def start_conditions(quest: dict[str, Any]) -> list[dict[str, Any]]:
    slug = str(quest["slug"])
    result: list[dict[str, Any]] = [{"id": condition_id(slug, "level"), "conditionType": "Level", "compareMethod": ">=", "value": int(quest["minimumLevel"]), "dynamicLocale": False, "index": 0, "parentId": "", "globalQuestCounterId": ""}]
    for index, predecessor in enumerate(quest.get("prerequisites") or [], start=1):
        result.append({"id": condition_id(slug, "prerequisite", index), "conditionType": "Quest", "dynamicLocale": False, "globalQuestCounterId": "", "index": index, "parentId": "", "status": [4], "target": str(predecessor)})
    return result


def finish_conditions(quest: dict[str, Any], key_pool: list[str]) -> list[dict[str, Any]]:
    slug = str(quest["slug"])
    objective = quest.get("objective") or {}
    count = int(objective.get("representativeCount", 1))
    condition_type = "HandoverItem" if objective.get("model") == "handover-explicit-keys" else "FindItem"
    return [{"id": condition_id(slug, "representative-keys"), "conditionType": condition_type, "dogtagLevel": 0, "dynamicLocale": False, "globalQuestCounterId": "", "index": 0, "isEncoded": False, "maxDurability": 100, "minDurability": 0, "onlyFoundInRaid": False, "parentId": "", "target": key_pool, "value": count, "visibilityConditions": []}]


def success_rewards(quest: dict[str, Any]) -> tuple[list[dict[str, Any]], int]:
    slug = str(quest["slug"])
    budget = quest.get("rewardBudget") or {}
    rewards: list[dict[str, Any]] = []
    index = 0
    xp = int(budget.get("xp", 0))
    if xp:
        rewards.append({"id": reward_id(slug, "xp"), "index": index, "type": "Experience", "value": xp}); index += 1
    standing = float(budget.get("standing", 0))
    if standing:
        rewards.append({"id": reward_id(slug, "standing"), "index": index, "type": "TraderStanding", "target": TRADER_ID, "value": standing}); index += 1
    rub = int(budget.get("rub", 0))
    if rub:
        item_id = mongo_id(f"admiral-keys:{slug}:reward:rub:item")
        rewards.append({"id": reward_id(slug, "rub"), "index": index, "type": "Item", "value": rub, "target": item_id, "items": [{"_id": item_id, "_tpl": RUB_TPL, "upd": {"StackObjectsCount": rub}}]})
    return rewards, int(budget.get("unlockSlots", 0))


def build_template(quest: dict[str, Any], key_pool: list[str]) -> tuple[dict[str, Any], int]:
    qid = str(quest["id"])
    rewards, deferred_unlocks = success_rewards(quest)
    template = {"QuestName": str(quest["name"]), "_id": qid, "acceptPlayerMessage": f"{qid} acceptPlayerMessage", "acceptanceAndFinishingSource": "eft", "canShowNotificationsInGame": True, "changeQuestMessageText": f"{qid} changeQuestMessageText", "completePlayerMessage": f"{qid} completePlayerMessage", "conditions": {"AvailableForFinish": finish_conditions(quest, key_pool), "AvailableForStart": start_conditions(quest), "Started": [], "Success": [], "Fail": []}, "declinePlayerMessage": f"{qid} declinePlayerMessage", "description": f"{qid} description", "failMessageText": f"{qid} failMessageText", "image": QUEST_ICON, "instantComplete": False, "isKey": False, "location": str(quest["map"]), "name": f"{qid} name", "note": f"{qid} note", "restartable": False, "rewards": {"Fail": [], "Started": [], "Success": rewards}, "secretQuest": False, "side": "Pmc", "startedMessageText": f"{qid} startedMessageText", "successMessageText": f"{qid} successMessageText", "traderId": TRADER_ID, "type": "PickUp"}
    return template, deferred_unlocks


def build_payload(spec: dict[str, Any], plan: dict[str, Any], inventory: dict[str, Any], source_root: Path) -> dict[str, Any]:
    inventory_by_id = {str(row.get("questId")): row for row in inventory.get("quests") or [] if isinstance(row, dict) and row.get("questId") is not None}
    groups = {str(group.get("id")): group for group in plan.get("groups") or [] if isinstance(group, dict)}
    cache: dict[str, dict[str, dict[str, Any]]] = {}
    templates: dict[str, dict[str, Any]] = {}
    key_pools: dict[str, list[str]] = {}
    deferred_unlocks: list[dict[str, Any]] = []
    source_pools: dict[str, list[str]] = {}
    for group_id, group in groups.items():
        source_pools[group_id] = source_key_pool(group, inventory_by_id, source_root, cache, allow_empty=True)
    for quest in spec.get("quests") or []:
        if not isinstance(quest, dict):
            continue
        slug = str(quest.get("slug"))
        group = groups.get(slug)
        if group is None:
            raise KeyError(f"no curated source group for authored quest {slug}")
        objective = quest.get("objective") or {}
        pool_source = str(objective.get("sourceKeyPoolGroup") or slug)
        if pool_source not in source_pools:
            raise KeyError(f"authored quest {slug} references unknown source key pool group: {pool_source}")
        source_pool = source_pools[pool_source]
        if not source_pool and objective.get("explicitTargets") is None:
            raise ValueError(f"authored quest {slug} has no usable FindItem source pool; set objective.sourceKeyPoolGroup to a validated key-bearing group")
        pool = bounded_key_pool(quest, source_pool)
        key_pools[slug] = pool
        template, unlock_count = build_template(quest, pool)
        templates[str(quest["id"])] = template
        if unlock_count:
            deferred_unlocks.append({"questId": str(quest["id"]), "slug": slug, "unlockSlots": unlock_count})
    return {"schemaVersion": 1, "status": "draft-runtime-templates", "traderId": TRADER_ID, "summary": {"templateCount": len(templates), "deferredUnlockSlotCount": sum(row["unlockSlots"] for row in deferred_unlocks), "minimumKeyPoolSize": min((len(values) for values in key_pools.values()), default=0), "maximumKeyPoolSize": max((len(values) for values in key_pools.values()), default=0)}, "templates": templates, "sourceKeyPools": key_pools, "deferredUnlocks": deferred_unlocks}


def main() -> int:
    parser = argparse.ArgumentParser(description="Build draft native SPT quest templates for Admiral Trader keys domain.")
    parser.add_argument("spec", type=Path); parser.add_argument("plan", type=Path); parser.add_argument("inventory", type=Path); parser.add_argument("source_root", type=Path); parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    spec = json.loads(args.spec.read_text(encoding="utf-8-sig")); plan = json.loads(args.plan.read_text(encoding="utf-8-sig")); inventory = json.loads(args.inventory.read_text(encoding="utf-8-sig"))
    payload = build_payload(spec, plan, inventory, args.source_root)
    args.output.parent.mkdir(parents=True, exist_ok=True); args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
