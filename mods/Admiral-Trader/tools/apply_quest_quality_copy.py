#!/usr/bin/env python3
"""Generate explicit, runtime-truthful EN/RU requirements for every Admiral quest."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUB = "5449016a4bdc2d6f028b456f"
LOCALE_FILES = {"en": ["en.json", "arsenal-en.json", "m3-en.json"], "ru": ["ru.json", "arsenal-ru.json", "m3-ru.json"]}
MAPS = {
    "Woods": ("Woods", "Лес"), "Interchange": ("Interchange", "Развязка"),
    "Shoreline": ("Shoreline", "Берег"), "bigmap": ("Customs", "Таможня"),
    "RezervBase": ("Reserve", "Резерв"), "factory4_day": ("Factory (day)", "Завод (день)"),
    "factory4_night": ("Factory (night)", "Завод (ночь)"), "Lighthouse": ("Lighthouse", "Маяк"),
    "laboratory": ("The Lab", "Лаборатория"),
}


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def item_name(locale: dict, tpl: str) -> str:
    return locale.get(f"{tpl} Name") or locale.get(f"{tpl} ShortName") or tpl


def join_names(names: list[str], lang: str) -> str:
    return ", ".join(names)


def equipment_text(c: dict, locale: dict, lang: str) -> str:
    groups = c.get("equipmentInclusive", [])
    rendered = [join_names([item_name(locale, tpl) for tpl in group], lang) for group in groups]
    if lang == "en":
        return "wear " + " and ".join(f"one of [{group}]" for group in rendered)
    return "использовать " + " и ".join(f"один предмет из [{group}]" for group in rendered)


def counter_text(condition: dict, locale: dict, lang: str) -> str:
    pieces, count = [], condition["value"]
    has_location = False
    for inner in condition["counter"]["conditions"]:
        kind = inner["conditionType"]
        if kind == "Equipment":
            pieces.append(equipment_text(inner, locale, lang))
        elif kind == "Kills":
            target = {"Savage": ("Scavs", "Диких"), "AnyPmc": ("PMCs", "бойцов ЧВК"), "Any": ("targets", "целей")}.get(inner["target"], (inner["target"], inner["target"]))[lang == "ru"]
            roles = inner.get("savageRole") or []
            if roles:
                target = {"exUsec": ("Rogues", "Отступников"), "pmcBot": ("Raiders", "Рейдеров")}.get(roles[0], (roles[0], roles[0]))[lang == "ru"]
            distance = (inner.get("distance") or {}).get("value", 0)
            if lang == "en":
                weapon_pool = inner.get("weapon") or []
                weapon_rule = (" using one of [" + join_names([item_name(locale, tpl) for tpl in weapon_pool], lang) + "]") if weapon_pool else ""
                pieces.append(f"eliminate {count} {target}" + (f" from at least {distance} metres" if distance else "") + weapon_rule)
            else:
                weapon_pool = inner.get("weapon") or []
                weapon_rule = (", используя один из вариантов [" + join_names([item_name(locale, tpl) for tpl in weapon_pool], lang) + "]") if weapon_pool else ""
                pieces.append(f"устранить {count} {target}" + (f" с дистанции не менее {distance} м" if distance else "") + weapon_rule)
        elif kind == "Location":
            has_location = True
            names = [MAPS.get(x, (x, x))[lang == "ru"] for x in inner["target"]]
            pieces.append(("on " if lang == "en" else "на локации ") + "/".join(names))
        elif kind == "ExitStatus":
            pieces.append("survive and extract" if lang == "en" else "выжить и выйти из рейда")
        elif kind == "VisitPlace":
            place = {"room206_water": ("Dorm room 206 in the two-storey dormitory", "комната 206 двухэтажного общежития")}.get(inner["target"], (inner["target"], inner["target"]))[lang == "ru"]
            pieces.append(("visit " if lang == "en" else "посетить ") + place)
        else:
            raise ValueError(f"unsupported inner condition {kind}")
    if not has_location:
        pieces.append("on any location" if lang == "en" else "на любой локации")
    if lang == "en":
        prefix = "In one raid, " if condition.get("oneSessionOnly") else "Across any number of raids, "
        suffix = " Found-in-raid status does not apply."
    else:
        prefix = "За один рейд: " if condition.get("oneSessionOnly") else "За любое количество рейдов: "
        suffix = " Статус «Найдено в рейде» не применяется."
    return prefix + "; ".join(pieces) + "." + suffix


def condition_text(condition: dict, locale: dict, lang: str) -> str:
    kind, count = condition["conditionType"], condition["value"]
    if kind == "CounterCreator":
        return counter_text(condition, locale, lang)
    names = [item_name(locale, tpl) for tpl in condition["target"]]
    pool = join_names(names, lang)
    fir = condition.get("onlyFoundInRaid") is True
    if kind == "FindItem":
        if lang == "en":
            noun = "item" if count == 1 else "items"
            return f"Have {count} {noun} in total from this allowed pool: [{pool}]. Found in raid: {'required' if fir else 'not required'}. Items are checked in your inventory and are not handed over."
        return f"Иметь суммарно {count} предмет(а) из допустимого списка: [{pool}]. Статус «Найдено в рейде»: {'обязателен' if fir else 'не требуется'}. Предметы проверяются в инвентаре и не передаются."
    if kind == "HandoverItem":
        if lang == "en":
            return f"Hand over {count} × {pool}. Found in raid: {'required' if fir else 'not required'}. Handed-over items are consumed."
        return f"Передать {count} × {pool}. Статус «Найдено в рейде»: {'обязателен' if fir else 'не требуется'}. Переданные предметы расходуются."
    raise ValueError(f"unsupported finish condition {kind}")


def reward_text(quest: dict, locale: dict, unlocks: dict, assort_tpl: dict, lang: str) -> str:
    parts = []
    for reward in quest["rewards"]["Success"]:
        kind, value = reward["type"], reward["value"]
        if kind == "Experience":
            parts.append(f"{value:,} XP".replace(",", " "))
        elif kind == "TraderStanding":
            parts.append(("+" + str(value) + " Admiral standing") if lang == "en" else ("+" + str(value) + " репутации Адмирала"))
        elif kind == "Item":
            tpl = reward["items"][0]["_tpl"]
            parts.append((f"₽{value:,}".replace(",", " ")) if tpl == RUB else f"{value} × {item_name(locale, tpl)}")
    offer_ids = unlocks.get(quest["_id"], [])
    for offer_id in offer_ids:
        name = item_name(locale, assort_tpl[offer_id])
        parts.append((f"purchase unlock: {name}" if lang == "en" else f"открытие покупки: {name}"))
    return ", ".join(parts) + "."


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("spt_runtime", type=Path)
    args = parser.parse_args()
    global_locales = {lang: load(args.spt_runtime / f"SPT_Data/database/locales/global/{lang}.json") for lang in ("en", "ru")}
    authored = {lang: {} for lang in ("en", "ru")}
    owners = {}
    for lang, files in LOCALE_FILES.items():
        for filename in files:
            payload = load(ROOT / "db/locales" / filename)
            for key, value in payload.items():
                authored[lang][key] = value
                owners[(lang, key)] = filename
    assort = load(ROOT / "db/assort.json")
    assort_tpl = {x["_id"]: x["_tpl"] for x in assort["items"] if x.get("parentId") == "hideout"}
    qa = load(ROOT / "db/questassort.json")["success"]
    unlocks = {}
    for offer, quest in qa.items():
        unlocks.setdefault(quest, []).append(offer)
    report = {"schemaVersion": 1, "status": "runtime-copy-audited", "questCount": 0, "quests": {}}
    quests = [load(path) for path in sorted((ROOT / "db/quests").glob("*.json"))]
    for quest in quests:
        qid = quest["_id"]
        report["questCount"] += 1
        report["quests"][qid] = {"questName": quest["QuestName"], "finishConditionIds": [x["id"] for x in quest["conditions"]["AvailableForFinish"]]}
        for lang in ("en", "ru"):
            locale = global_locales[lang]
            requirements = [condition_text(c, locale, lang) for c in quest["conditions"]["AvailableForFinish"]]
            rewards = reward_text(quest, locale, unlocks, assort_tpl, lang)
            req_label = "Requirements" if lang == "en" else "Требования"
            rew_label = "Rewards" if lang == "en" else "Награды"
            existing_description = authored[lang][f"{qid} description"].split(f"\n\n{req_label}:", 1)[0]
            existing_started = authored[lang][f"{qid} startedMessageText"].split(f"\n\n{req_label}:", 1)[0]
            existing_success = authored[lang][f"{qid} successMessageText"].split(f"\n\n{rew_label}:", 1)[0]
            # EFT renders embedded newlines in quest copy. Keep each runtime objective
            # on its own line so exact requirements stay readable at normal UI scale.
            block = "\n".join(f"- {requirement}" for requirement in requirements)
            updates = {
                f"{qid} description": f"{existing_description}\n\n{req_label}:\n{block}\n\n{rew_label}:\n- {rewards}",
                f"{qid} startedMessageText": f"{existing_started}\n\n{req_label}:\n{block}",
                f"{qid} acceptPlayerMessage": f"{existing_started}\n\n{req_label}:\n{block}",
                f"{qid} successMessageText": f"{existing_success}\n\n{rew_label}: {rewards}",
                f"{qid} completePlayerMessage": f"{existing_success}\n\n{rew_label}: {rewards}",
            }
            for condition, objective in zip(quest["conditions"]["AvailableForFinish"], requirements):
                updates[condition["id"]] = objective
            for key, value in updates.items():
                filename = owners.get((lang, key)) or ("m3-en.json" if lang == "en" and qid in load(ROOT / "manifests/m3-runtime-materialization.json")["questIds"].values() else "m3-ru.json" if lang == "ru" and qid in load(ROOT / "manifests/m3-runtime-materialization.json")["questIds"].values() else "arsenal-en.json" if lang == "en" and quest["QuestName"].startswith("Arsenal") else "arsenal-ru.json" if lang == "ru" and quest["QuestName"].startswith("Arsenal") else f"{lang}.json")
                path = ROOT / "db/locales" / filename
                payload = load(path)
                payload[key] = value
                path.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
                owners[(lang, key)] = filename
                authored[lang][key] = value
    (ROOT / "manifests/quest-quality-runtime.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
