#!/usr/bin/env python3
"""Materialize the approved M3 operation wave into deterministic SPT quests."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRADER = "d5c27bb3169f8dfbc13f6b69"
RUB = "5449016a4bdc2d6f028b456f"
NS = "com.admiralam.spt.admiraltrader:m3"

HEADSETS = ["5b432b965acfc47a8774094e", "5c165d832e2216398b5a7e36", "5e4d34ca86f774264f758330", "6033fa48ffd42c541047f728", "628e4e576d783146b124c64d"]
LIGHT_ARMOR = ["5df8a2ca86f7740bfe6df777", "5c0e5edb86f77461f55ed1f7", "5c0e57ba86f7747fa141986d", "5b44d22286f774172b0c9de8"]
HEAVY_ARMOR = ["5ca2151486f774244a3b8d30", "5ca21c6986f77479963115a7", "5e9dacf986f774054d6b89f4", "5f5f41476bdad616ad46d631"]
HEAVY_HELMETS = ["5aa7e276e5b5b000171d0647", "5ca20ee186f774799474abc2", "5f60c74e3b85f6263c145586"]


def oid(*parts: str) -> str:
    return hashlib.sha256(":".join((NS, *parts)).encode()).hexdigest()[:24]


def inner(key: str, n: int, condition_type: str, **values):
    result = {"id": oid(key, "inner", str(n)), "dynamicLocale": False, "conditionType": condition_type}
    result.update(values)
    return result


def counter(key: str, index: int, value: int, kind: str, conditions: list[dict]):
    return {
        "id": oid(key, "finish", str(index)), "index": index, "dynamicLocale": False,
        "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "", "value": value,
        "type": kind, "oneSessionOnly": True, "isResetOnConditionFailed": False,
        "isNecessary": False, "doNotResetIfCounterCompleted": False,
        "counter": {"id": oid(key, "counter", str(index)), "conditions": conditions},
        "completeInSeconds": 0, "conditionType": "CounterCreator",
    }


def equipment(key: str, n: int, groups: list[list[str]]):
    return inner(key, n, "Equipment", equipmentInclusive=groups, equipmentExclusive=[], IncludeNotEquippedItems=False)


def location(key: str, n: int, target: str):
    return inner(key, n, "Location", target=[target])


def exit_status(key: str, n: int):
    return inner(key, n, "ExitStatus", status=["Survived"])


def kills(key: str, n: int, target: str, roles=None, distance=0):
    return inner(key, n, "Kills", target=target, compareMethod=">=", value=1,
                 weapon=[], distance={"value": distance, "compareMethod": ">="},
                 weaponModsInclusive=[], weaponModsExclusive=[], enemyEquipmentInclusive=[],
                 enemyEquipmentExclusive=[], weaponCaliber=[], savageRole=roles or [], bodyPart=[],
                 daytime={"from": 0, "to": 0}, enemyHealthEffects=[], resetOnSessionEnd=False)


def handover(key: str, index: int, tpl: str, count: int):
    return {
        "id": oid(key, "finish", str(index)), "conditionType": "HandoverItem", "dogtagLevel": 0,
        "dynamicLocale": False, "globalQuestCounterId": "", "index": index, "isEncoded": False,
        "maxDurability": 100, "minDurability": 0, "onlyFoundInRaid": False, "parentId": "",
        "target": [tpl], "value": count, "visibilityConditions": [],
    }


def mechanics(key: str):
    maps = {"woods": "Woods", "interchange": "Interchange", "shoreline": "Shoreline",
            "customs": "bigmap", "reserve": "RezervBase", "factory": "factory4_day",
            "lighthouse": "Lighthouse", "labs": "laboratory"}
    if key == "acoustic-discipline":
        return [counter(key, 0, 1, "Exploration", [equipment(key, 0, [HEADSETS]), location(key, 1, maps["woods"]), exit_status(key, 2)])]
    if key == "forward-reserve":
        return [handover(key, 0, "590c5bbd86f774785762df04", 2), handover(key, 1, "57347c1124597737fb1379e3", 3), handover(key, 2, "61bf83814088ec1a363d7097", 1)]
    if key == "low-profile":
        return [counter(key, 0, 1, "Exploration", [equipment(key, 0, [["572b7adb24597762ae139821"], ["56e33680d2720be2748b4576"]]), location(key, 1, maps["interchange"]), exit_status(key, 2)])]
    if key == "mobility-doctrine":
        return [counter(key, 0, 3, "Elimination", [equipment(key, 0, [LIGHT_ARMOR]), kills(key, 1, "Savage"), location(key, 2, maps["shoreline"])]), counter(key, 1, 1, "Exploration", [location(key, 3, maps["shoreline"]), exit_status(key, 4)])]
    if key == "borrowed-access":
        return [counter(key, 0, 1, "Exploration", [location(key, 0, maps["customs"]), inner(key, 1, "VisitPlace", target="room206_water", value=1)]), counter(key, 1, 1, "Exploration", [location(key, 2, maps["customs"]), exit_status(key, 3)])]
    if key == "acoustic-contact":
        return [counter(key, 0, 2, "Elimination", [equipment(key, 0, [HEADSETS]), kills(key, 1, "Savage"), location(key, 2, maps["woods"])]), counter(key, 1, 1, "Exploration", [location(key, 3, maps["woods"]), exit_status(key, 4)])]
    if key == "route-security":
        return [counter(key, 0, 6, "Elimination", [kills(key, 0, "Savage"), location(key, 1, maps["customs"])]), counter(key, 1, 1, "Exploration", [location(key, 2, maps["customs"]), exit_status(key, 3)])]
    if key == "contractor-intercept":
        return [counter(key, 0, 4, "Elimination", [kills(key, 0, "AnyPmc"), location(key, 1, maps["reserve"])]), counter(key, 1, 1, "Exploration", [location(key, 2, maps["reserve"]), exit_status(key, 3)])]
    if key == "observation-window":
        return [counter(key, 0, 2, "Elimination", [kills(key, 0, "Savage", distance=80), location(key, 1, maps["shoreline"])]), counter(key, 1, 1, "Exploration", [location(key, 2, maps["shoreline"]), exit_status(key, 3)])]
    if key == "heavy-assault":
        return [counter(key, 0, 4, "Elimination", [equipment(key, 0, [HEAVY_ARMOR, HEAVY_HELMETS]), kills(key, 1, "Savage"), location(key, 2, maps["factory"])]), counter(key, 1, 1, "Exploration", [location(key, 3, maps["factory"]), exit_status(key, 4)])]
    if key == "break-the-perimeter":
        return [counter(key, 0, 6, "Elimination", [kills(key, 0, "Any", ["exUsec"]), location(key, 1, maps["lighthouse"])]), counter(key, 1, 1, "Exploration", [location(key, 2, maps["lighthouse"]), exit_status(key, 3)])]
    if key == "internal-security":
        return [counter(key, 0, 4, "Elimination", [kills(key, 0, "Any", ["pmcBot"]), location(key, 1, maps["labs"])]), counter(key, 1, 1, "Exploration", [location(key, 2, maps["labs"]), exit_status(key, 3)])]
    raise KeyError(key)


def main():
    spec = json.loads((ROOT / "manifests/m3-campaign-product-spec.json").read_text(encoding="utf-8"))
    progression = json.loads((ROOT / "manifests/m3-campaign-progression.json").read_text(encoding="utf-8"))
    copy = json.loads((ROOT / "manifests/m3-campaign-editorial-copy.json").read_text(encoding="utf-8"))
    copy_by_key = {x["key"]: x for x in copy["quests"]}
    spec_by_key = {x["key"]: x for x in spec["operations"]}
    ids = {key: oid(key, "quest") for key in progression["levels"]}
    locale = {"en": {}, "ru": {}}
    runtime = {"schemaVersion": 1, "status": "M3-runtime-materialized", "questIds": ids,
               "questCount": 12, "totalCampaignQuestCount": 43, "baselineQuestCount": 31,
               "equipmentAllowlists": {"headsets": HEADSETS, "lightArmor": LIGHT_ARMOR,
                                       "heavyArmor": HEAVY_ARMOR, "heavyHelmets": HEAVY_HELMETS}}
    for order, key in enumerate(progression["levels"], 1):
        qid, text, entry = ids[key], copy_by_key[key], spec_by_key[key]
        finishes = mechanics(key)
        starts = [{"id": oid(key, "start", "level"), "index": 0, "compareMethod": ">=", "dynamicLocale": False,
                   "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "",
                   "value": progression["levels"][key], "conditionType": "Level"}]
        prereqs = progression["prerequisites"][key] + progression["externalPrerequisites"].get(key, [])
        for n, prereq in enumerate(prereqs, 1):
            starts.append({"id": oid(key, "start", str(n)), "index": n, "dynamicLocale": False,
                           "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "",
                           "target": ids.get(prereq, prereq), "status": [4], "availableAfter": 0,
                           "dispersion": 0, "conditionType": "Quest"})
        reward = entry["reward"]
        rewards = [
            {"value": reward["xp"], "id": oid(key, "reward", "xp"), "type": "Experience", "index": 0},
            {"value": reward["standing"], "id": oid(key, "reward", "standing"), "type": "TraderStanding", "target": TRADER, "index": 1},
            {"value": reward["rub"], "id": oid(key, "reward", "rub"), "type": "Item", "target": oid(key, "reward", "rub-item"), "index": 2,
             "items": [{"_id": oid(key, "reward", "rub-item"), "_tpl": RUB, "upd": {"StackObjectsCount": reward["rub"]}}]},
        ]
        quest_type = "Elimination" if any(c.get("type") == "Elimination" for c in finishes) else ("Completion" if key == "forward-reserve" else "Exploration")
        quest = {"QuestName": text["title"]["en"], "_id": qid, "canShowNotificationsInGame": True,
                 "conditions": {"AvailableForFinish": finishes, "AvailableForStart": starts, "Fail": []},
                 "description": f"{qid} description", "failMessageText": f"{qid} failMessageText", "name": f"{qid} name", "note": f"{qid} note",
                 "traderId": TRADER, "location": "any", "image": "/files/quest/icon/5a27cafa86f77424e20615d6.jpg", "type": quest_type,
                 "isKey": False, "restartable": False, "instantComplete": False, "secretQuest": False,
                 "startedMessageText": f"{qid} startedMessageText", "successMessageText": f"{qid} successMessageText",
                 "acceptPlayerMessage": f"{qid} acceptPlayerMessage", "acceptanceAndFinishingSource": "eft",
                 "declinePlayerMessage": f"{qid} declinePlayerMessage", "completePlayerMessage": f"{qid} completePlayerMessage",
                 "changeQuestMessageText": f"{qid} changeQuestMessageText", "rewards": {"Started": [], "Success": rewards, "Fail": []},
                 "side": "Pmc", "status": 0, "progressSource": "eft", "gameModes": [], "rankingModes": [], "arenaLocations": []}
        (ROOT / "db/quests" / f"30-{order:02d}-{qid}.json").write_text(json.dumps(quest, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
        for lang in ("en", "ru"):
            objectives = text["objective"][lang]
            fields = {"name": text["title"][lang], "description": text["briefing"][lang], "note": "",
                      "startedMessageText": text["briefing"][lang], "successMessageText": text["success"][lang],
                      "failMessageText": "", "acceptPlayerMessage": text["briefing"][lang], "declinePlayerMessage": "",
                      "completePlayerMessage": text["success"][lang], "changeQuestMessageText": ""}
            locale[lang].update({f"{qid} {field}": value for field, value in fields.items()})
            for n, finish in enumerate(finishes):
                locale[lang][finish["id"]] = objectives[min(n, len(objectives) - 1)]
    for lang in ("en", "ru"):
        (ROOT / "db/locales" / f"m3-{lang}.json").write_text(json.dumps(locale[lang], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (ROOT / "manifests/m3-runtime-materialization.json").write_text(json.dumps(runtime, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
