#!/usr/bin/env python3
"""Render a compact, player-reviewable list of Admiral runtime quests.

The report is deliberately derived from the committed quest templates.  It
therefore shows the conditions SPT evaluates and the rewards SPT grants, not
marketing copy from the locale descriptions.
"""
from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path
from typing import Any


RUB_TPL = "5449016a4bdc2d6f028b456f"


def load(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def item_name(item_id: str, items: dict[str, Any], locale: dict[str, str], optional: dict[str, str]) -> str:
    name = locale.get(f"{item_id} Name") or locale.get(f"{item_id} ShortName")
    if name:
        return str(name)
    if item_id in optional:
        return f"{optional[item_id]} (WTT, необязательно)"
    item = items.get(item_id) or {}
    return str(item.get("_name") or item.get("_id") or item_id)


def names(values: Any, items: dict[str, Any], locale: dict[str, str], optional: dict[str, str], *, limit: int = 5) -> str:
    if not isinstance(values, list):
        values = [values]
    resolved = [item_name(str(value), items, locale, optional) for value in values if value]
    if len(resolved) > limit:
        return ", ".join(resolved[:limit]) + f" и ещё {len(resolved) - limit}"
    return ", ".join(resolved) or "указанный игровой объект"


def map_name(raw: Any) -> str:
    values = raw if isinstance(raw, list) else [raw]
    mapping = {
        "Ground Zero": "Эпицентр", "Sandbox": "Эпицентр", "Sandbox_high": "Эпицентр (высокие уровни)",
        "Customs": "Таможня", "bigmap": "Таможня", "Woods": "Лес", "woods": "Лес",
        "Shoreline": "Берег", "shoreline": "Берег", "Interchange": "Развязка", "interchange": "Развязка", "interchange": "Развязка", "Factory": "Завод", "factory4_day": "Завод (день)", "factory4_night": "Завод (ночь)",
        "Reserve": "Резерв", "RezervBase": "Резерв", "Lighthouse": "Маяк", "lighthouse": "Маяк", "Streets": "Улицы Таркова", "TarkovStreets": "Улицы Таркова",
        "Laboratory": "Лаборатория", "The Lab": "Лаборатория", "any": "любая локация",
    }
    return ", ".join(mapping.get(str(value), str(value)) for value in values if value) or "указанная локация"


def counter_requirement(condition: dict[str, Any], items: dict[str, Any], locale: dict[str, str], optional: dict[str, str]) -> str:
    value = int(condition.get("value") or 1)
    kind = str(condition.get("type") or "")
    rules = ((condition.get("counter") or {}).get("conditions") or [])
    locations: list[str] = []
    equipment: list[str] = []
    weapons: list[str] = []
    target = ""
    survived = False
    distance = 0
    for rule in rules:
        rule_type = str(rule.get("conditionType") or "")
        if rule_type == "Location":
            locations.extend(str(x) for x in (rule.get("target") or []) if x)
        elif rule_type == "Equipment":
            for pool in rule.get("equipmentInclusive") or []:
                equipment.extend(str(x) for x in pool if x)
        elif rule_type == "ExitStatus":
            survived = "Survived" in (rule.get("status") or [])
        elif rule_type == "Kills":
            weapons.extend(str(x) for x in rule.get("weapon") or [] if x)
            target = str(rule.get("target") or "")
            roles = {str(role) for role in rule.get("savageRole") or []}
            if "exUsec" in roles:
                target = "Отступники"
            elif "pmcBot" in roles:
                target = "Рейдеры"
            distance = int(((rule.get("distance") or {}).get("value") or 0))

    if kind == "Elimination":
        result = f"Устранить {value} целей"
        if target and target.lower() != "any":
            result += f" ({target})"
        if weapons:
            result += f" оружием: {names(weapons, items, locale, optional)}"
        if distance:
            result += f" с дистанции от {distance} м"
    elif kind == "Exploration":
        result = "Выжить в рейде" if survived else "Выполнить рейдовую задачу"
        if equipment:
            result += f" в экипировке: {names(equipment, items, locale, optional)}"
    else:
        result = f"Выполнить условие {value} раз"
    if locations:
        result += f"; локация: {map_name(locations)}"
    return result + "."


def localized_condition_text(condition: dict[str, Any], quest_locale: dict[str, str]) -> list[str]:
    """Use the exact player-facing objective wording when the runtime has it."""
    own = quest_locale.get(str(condition.get("id") or ""))
    if own:
        return [str(own).replace("\n", " ")]
    nested = ((condition.get("counter") or {}).get("conditions") or [])
    result = [str(quest_locale[str(row.get("id") or "")]).replace("\n", " ") for row in nested if quest_locale.get(str(row.get("id") or ""))]
    return result


def requirement(quest: dict[str, Any], items: dict[str, Any], locale: dict[str, str], optional: dict[str, str], quest_locale: dict[str, str]) -> str:
    parts: list[str] = []
    for condition in (quest.get("conditions") or {}).get("AvailableForFinish") or []:
        kind = str(condition.get("conditionType") or "")
        # Pools of keys and weapons must stay visible in the review; their
        # native objective sentence intentionally calls them simply
        # "подходящий" and would hide the meaningful balance decision.
        if kind == "CounterCreator" and str(condition.get("type") or "") == "Elimination":
            parts.append(counter_requirement(condition, items, locale, optional))
            continue
        if kind == "CounterCreator":
            exact = localized_condition_text(condition, quest_locale)
            if exact:
                parts.extend(exact)
            else:
                parts.append(counter_requirement(condition, items, locale, optional))
        elif kind in {"FindItem", "HandoverItem"}:
            count = int(condition.get("value") or 1)
            verb = "Найти" if kind == "FindItem" else "Передать"
            text = f"{verb} {count} шт.: {names(condition.get('target'), items, locale, optional)}"
            if condition.get("onlyFoundInRaid"):
                text += " (найдено в рейде)"
            parts.append(text + ".")
        elif kind == "PlaceBeacon":
            exact = localized_condition_text(condition, quest_locale)
            parts.extend(exact or [f"Установить {int(condition.get('value') or 1)} маркер(а) в заданной точке."])
        else:
            parts.append(f"{kind}: {condition.get('value', 1)}.")
    return " ".join(parts) or "Нет условия завершения."


def reward(quest: dict[str, Any], items: dict[str, Any], locale: dict[str, str], optional: dict[str, str], optional_rewards: dict[str, str], signature_rewards: dict[str, str], early_rewards: dict[str, dict[str, Any]], tactical_rewards: dict[str, dict[str, Any]], pack_rewards: dict[str, dict[str, Any]]) -> str:
    xp = rub = 0
    standing = 0.0
    item_extras: list[str] = []
    extras: list[str] = []
    for row in (quest.get("rewards") or {}).get("Success") or []:
        kind = row.get("type")
        if kind == "Experience":
            xp += int(row.get("value") or 0)
        elif kind == "TraderStanding":
            standing += float(row.get("value") or 0)
        elif kind == "Item":
            for item in row.get("items") or []:
                quantity = int(((item.get("upd") or {}).get("StackObjectsCount") or 1))
                if item.get("_tpl") == RUB_TPL:
                    rub += quantity
                else:
                    item_extras.append(f"{item_name(str(item.get('_tpl')), items, locale, optional)} ×{quantity}")
        elif kind == "AssortmentUnlock":
            extras.append("открытие товара у Адмирала")
        else:
            extras.append(str(kind))
    qid = str(quest.get("_id"))
    if qid in pack_rewards:
        rub -= int(pack_rewards[qid]["cashReductionRub"])
        item_extras.append(f"{pack_rewards[qid]['name']} ×1 (при наличии Pack ’n’ Strap)")
    if qid in early_rewards:
        rub -= int(early_rewards[qid]["cashReductionRub"])
        item_extras.append(f"готовый оружейный комплект: {early_rewards[qid]['name']} ×1")
    if qid in tactical_rewards:
        rub -= int(tactical_rewards[qid]["cashReductionRub"])
        item_extras.append(f"{tactical_rewards[qid]['name']} ×1")
    if qid in signature_rewards:
        item_extras = [f"готовый оружейный комплект: {item_name(signature_rewards[qid], items, locale, optional)} ×1"]
    elif qid in optional_rewards:
        item_extras = [f"при WTT физическая награда заменяется на: {optional_rewards[qid]}"]
    result = [f"{xp:,} XP".replace(",", " "), f"₽{rub:,}".replace(",", " "), f"+{standing:.3f} реп." ]
    result.extend(item_extras)
    result.extend(extras)
    return "; ".join(result)


def group_quests(root: Path, quests: list[dict[str, Any]]) -> list[tuple[str, list[dict[str, Any]]]]:
    story = load(root / "manifests/story-campaign-runtime.json")
    m8 = load(root / "manifests/m8-campaign-expansion-runtime.json")
    access = load(root / "manifests/keys-authored-spec.json")
    weapons = load(root / "manifests/weapon-ammo-authored-spec.json")
    rotation = load(root / "manifests/weapon-rotation-runtime.json")
    story_by_id = {str(row["id"]): row for row in story.get("quests") or []}
    m8_by_id = {str(row["id"]): row for row in m8.get("quests") or []}
    access_ids = {str(row["id"]) for row in access.get("quests") or []}
    weapon_ids = {str(row["id"]) for row in weapons.get("quests") or []}
    rotation_by_id = {str(row["id"]): row for row in rotation.get("assignments") or []}
    groups: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for quest in quests:
        qid = str(quest["_id"])
        if qid in story_by_id:
            row = story_by_id[qid]
            label = f"Сюжет {int(row['chain'])}: {row['map']}"
        elif qid in access_ids:
            label = "Протоколы доступа"
        elif qid in rotation_by_id:
            label = f"Арсенал: ротация, линия {rotation_by_id[qid]['lane']}"
        elif qid in weapon_ids:
            label = "Арсенал: базовые категории"
        elif qid in m8_by_id:
            row = m8_by_id[qid]
            kind = row.get("kind")
            if kind == "weapon":
                label = f"Арсенал: ротация, линия {row.get('lane')}"
            elif kind == "equipment":
                label = "Операции: экипировка"
            else:
                label = "Операции: Эпицентр"
        elif str(quest.get("QuestName") or "").startswith("Протокол «Арсенал»"):
            label = "Арсенал: базовые категории"
        else:
            label = "Операции: основная линия"
        groups[label].append(quest)
    preferred = ["Протоколы доступа", "Арсенал: ротация, линия A", "Арсенал: ротация, линия B", "Арсенал: базовые категории", "Операции: Эпицентр", "Операции: экипировка", "Операции: основная линия"]
    preferred.extend(f"Сюжет {number}: {map_name}" for number, map_name in enumerate(["Эпицентр", "Таможня", "Лес", "Развязка", "Берег", "Резерв", "Маяк", "Улицы", "Завод", "Лаборатория"], 1))
    return [(name, groups[name]) for name in preferred if groups.get(name)]


def main() -> int:
    parser = argparse.ArgumentParser(description="Build a compact Admiral quest review sheet.")
    parser.add_argument("--root", type=Path, required=True)
    parser.add_argument("--spt-locale", type=Path, required=True)
    parser.add_argument("--spt-items", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    root = args.root
    items = load(args.spt_items)
    locale = load(args.spt_locale)
    quest_locale: dict[str, str] = {}
    for locale_path in sorted((root / "db/locales").glob("*ru.json")):
        quest_locale.update(load(locale_path))
    optional_data = load(root / "manifests/optional-weapon-runtime.json")
    optional = {str(row["tpl"]): str(row.get("nameRu") or row.get("name") or row["tpl"]) for row in optional_data.get("acceptedWeapons") or []}
    storefront = load(root / "manifests/optional-storefront-runtime.json")
    optional_rewards = {str(row["questId"]): str(row["name"]) for row in storefront.get("questRewardReplacements") or []}
    reward_wave = load(root / "manifests/campaign-reward-wave.json")
    signature_rewards = {str(row["questId"]): str(row["tpl"]) for row in reward_wave.get("signatureFinales") or []}
    early_rewards = {str(row["questId"]): row for row in reward_wave.get("earlyWeaponTrades") or []}
    tactical_rewards = {str(row["questId"]): row for row in reward_wave.get("tacticalRewardTrades") or []}
    pack_rewards = {str(row["questId"]): row for row in reward_wave.get("packNStrapTrades") or []}
    quests = [load(path) for path in sorted((root / "db/quests").glob("*.json"))]
    groups = group_quests(root, quests)
    lines = [
        "# Карта кампании Адмирала — условия и награды",
        "",
        f"Срез точного runtime: **{len(quests)} основных заданий**. Каждая строка получена из фактических SPT-условий и `Success`-наград, а не из рекламного описания квеста.",
        "",
        "В таблице не показаны постоянный ID, служебные сообщения и технические поля. `Реп.` означает репутацию Адмирала. Открытие товара означает одноразовый unlock, а не бесплатный предмет.",
        "",
        "Для строк Pack ’n’ Strap показан вариант при установленном наборе: контейнер заменяет указанную часть рублей. Без его шаблонов контейнер не выдаётся, а денежная награда остаётся исходной.",
        "",
        "## Состав",
        "",
        "| Кластер | Заданий |",
        "| --- | ---: |",
    ]
    lines.extend(f"| {name} | {len(rows)} |" for name, rows in groups)
    for name, rows in groups:
        lines.extend(["", f"## {name}", "", "| Задание | Условие завершения | Награда |", "| --- | --- | --- |"])
        for quest in rows:
            title = str(quest.get("QuestName") or quest.get("_id"))
            lines.append(f"| {title} | {requirement(quest, items, locale, optional, quest_locale)} | {reward(quest, items, locale, optional, optional_rewards, signature_rewards, early_rewards, tactical_rewards, pack_rewards)} |")
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"wrote {args.output} with {len(quests)} quests in {len(groups)} clusters")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
