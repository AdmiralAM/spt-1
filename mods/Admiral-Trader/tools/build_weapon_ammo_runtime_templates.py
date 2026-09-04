#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

TRADER_ID = "d5c27bb3169f8dfbc13f6b69"
RUB_TPL = "5449016a4bdc2d6f028b456f"
QUEST_ICON = "/files/quest/icon/5a27cafa86f77424e20615d6.jpg"


def mongo_id(namespace: str) -> str:
    return hashlib.sha256(namespace.encode()).hexdigest()[:24]


def ident(slug: str, role: str, index: int = 0) -> str:
    return mongo_id(f"admiral-weapon-ammo:{slug}:{role}:{index}")


def start_conditions(quest: dict[str, Any], authored: dict[str, Any]) -> list[dict[str, Any]]:
    stage = authored["stagesBySlug"][quest["slug"]]
    result = [{
        "id": ident(quest["slug"], "level"), "index": 0, "compareMethod": ">=",
        "dynamicLocale": False, "globalQuestCounterId": "", "visibilityConditions": [],
        "parentId": "", "value": int(stage["minimumLevel"]), "conditionType": "Level",
    }]
    for idx, predecessor in enumerate(quest.get("prerequisites") or [], start=1):
        result.append({
            "id": ident(quest["slug"], "prereq", idx), "index": idx, "dynamicLocale": False,
            "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "",
            "target": predecessor, "status": [4], "availableAfter": 0, "dispersion": 0,
            "conditionType": "Quest",
        })
    return result


def elimination_condition(slug: str, kills: int, weapon_ids: list[str]) -> dict[str, Any]:
    if not weapon_ids:
        raise ValueError(f"{slug}: empty weapon pool")
    return {
        "id": ident(slug, "counter"), "index": 0, "dynamicLocale": False,
        "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "", "value": kills,
        "type": "Elimination", "oneSessionOnly": False, "isResetOnConditionFailed": False,
        "isNecessary": False, "doNotResetIfCounterCompleted": False,
        "counter": {
            "id": ident(slug, "counter-inner"),
            "conditions": [{
                "id": ident(slug, "kills"), "dynamicLocale": False, "target": "Any",
                "compareMethod": ">=", "value": 1, "weapon": weapon_ids,
                "distance": {"value": 0, "compareMethod": ">="},
                "weaponModsInclusive": [], "weaponModsExclusive": [],
                "enemyEquipmentInclusive": [], "enemyEquipmentExclusive": [], "weaponCaliber": [],
                "savageRole": [], "bodyPart": [], "daytime": {"from": 0, "to": 0},
                "conditionType": "Kills", "enemyHealthEffects": [], "resetOnSessionEnd": False,
            }],
        },
        "completeInSeconds": 0, "conditionType": "CounterCreator",
    }


def success_rewards(slug: str, stage: dict[str, Any], capability: dict[str, Any] | None) -> list[dict[str, Any]]:
    rewards: list[dict[str, Any]] = []
    index = 0
    xp = int(stage.get("xp", 0))
    if xp:
        rewards.append({"value": xp, "id": ident(slug, "reward-xp"), "type": "Experience", "index": index}); index += 1
    standing = float(stage.get("standing", 0))
    if standing:
        rewards.append({"value": standing, "id": ident(slug, "reward-standing"), "type": "TraderStanding", "target": TRADER_ID, "index": index}); index += 1
    rub = int(stage.get("rub", 0))
    if rub:
        item_id = ident(slug, "reward-rub-item")
        rewards.append({"value": rub, "id": ident(slug, "reward-rub"), "type": "Item", "target": item_id, "index": index,
                        "items": [{"_id": item_id, "_tpl": RUB_TPL, "upd": {"StackObjectsCount": rub}}]}); index += 1
    if capability and stage.get("sampleAmmoUnits") and capability.get("tpl"):
        units = int(stage["sampleAmmoUnits"]); item_id = ident(slug, "reward-ammo-item")
        rewards.append({"value": units, "id": ident(slug, "reward-ammo"), "type": "Item", "target": item_id, "index": index,
                        "items": [{"_id": item_id, "_tpl": capability["tpl"], "upd": {"StackObjectsCount": units}}]})
    return rewards


def authored_index(spec: dict[str, Any]) -> dict[str, Any]:
    stages_by_slug, display_by_family = {}, {}
    for family in spec.get("families") or []:
        fid = str(family["id"]); display_by_family[fid] = str(family.get("displayName") or fid)
        for stage in family.get("stages") or []:
            stages_by_slug[str(stage["slug"])] = stage
    return {"stagesBySlug": stages_by_slug, "displayByFamily": display_by_family}


def build_templates(plan: dict[str, Any], spec: dict[str, Any], capabilities: dict[str, Any], runtime_pools: dict[str, Any]) -> dict[str, Any]:
    if any(x.get("targetSptVersion") != "4.1.4" for x in (plan, spec, capabilities, runtime_pools)):
        raise ValueError("all weapon-ammo runtime inputs must target SPT 4.1.4")
    authored = authored_index(spec); templates: dict[str, dict[str, Any]] = {}; deferred = []
    for quest in plan.get("quests") or []:
        slug, family = str(quest["slug"]), str(quest["family"])
        stage = authored["stagesBySlug"][slug]
        weapon_ids = list(dict.fromkeys(str(x) for x in runtime_pools["families"].get(family, [])))
        capability = capabilities["families"].get(family)
        if family == "special-weapons" and quest["stage"] == "munitions":
            deferred.append({"questId": quest["id"], "slug": slug,
                             "reason": "special sample TPL requires exact SPT 4.1.4 runtime item verification"})
        name = f"Arsenal Protocol: {authored['displayByFamily'][family]} - {str(quest['stage']).title()}"; qid = str(quest["id"])
        templates[qid] = {
            "QuestName": name, "_id": qid, "canShowNotificationsInGame": True,
            "conditions": {"AvailableForFinish": [elimination_condition(slug, int(stage["kills"]), weapon_ids)],
                           "AvailableForStart": start_conditions(quest, authored), "Started": [], "Success": [], "Fail": []},
            "description": f"{qid} description", "failMessageText": f"{qid} failMessageText", "name": f"{qid} name",
            "note": f"{qid} note", "traderId": TRADER_ID, "location": "any", "image": QUEST_ICON, "type": "Elimination",
            "isKey": False, "restartable": False, "instantComplete": False, "secretQuest": False,
            "startedMessageText": f"{qid} startedMessageText", "successMessageText": f"{qid} successMessageText",
            "acceptPlayerMessage": f"{qid} acceptPlayerMessage", "acceptanceAndFinishingSource": "eft",
            "declinePlayerMessage": f"{qid} declinePlayerMessage", "completePlayerMessage": f"{qid} completePlayerMessage",
            "rewards": {"Started": [], "Success": success_rewards(slug, stage, capability if quest["stage"] == "munitions" and family != "special-weapons" else None), "Fail": []},
            "side": "Pmc",
        }
    if len(templates) != 21:
        raise ValueError(f"expected 21 runtime templates, got {len(templates)}")
    return {"schemaVersion": 2, "targetSptVersion": "4.1.4", "templates": templates, "deferredRuntimeItems": deferred}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("plan", type=Path); parser.add_argument("spec", type=Path); parser.add_argument("capabilities", type=Path)
    parser.add_argument("runtime_pools", type=Path); parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    payload = build_templates(*[json.loads(p.read_text(encoding="utf-8")) for p in (args.plan, args.spec, args.capabilities, args.runtime_pools)])
    args.output.parent.mkdir(parents=True, exist_ok=True); args.output.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"questCount": len(payload["templates"]), "deferredRuntimeItems": len(payload["deferredRuntimeItems"])}, sort_keys=True)); return 0


if __name__ == "__main__":
    raise SystemExit(main())
