#!/usr/bin/env python3
"""Correct authored/runtime mismatches in the 40-quest Arsenal rotation."""
from __future__ import annotations

import hashlib
import json
import argparse
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLAN_PATH = ROOT / "manifests/weapon-rotation-expansion-plan.json"
RUNTIME_PATH = ROOT / "manifests/weapon-rotation-runtime.json"
IDS = {
    "A": [
        "738588764e9531bdb8ccfc5f", "7ec59fd018a4332dc9cc598e", "3ccca027eeebaf0a4dcfbb6f",
        "9a2efe4c869cd41beb667e29", "5c7e60f203900e75aba3edc0", "9620e5a3c17b30599df67730",
        "de92651782ad58fce6457af9", "8722d67f966ff1605393222a", "d7f67e37b0c244878aa685e0",
        "a006fa3e96a9c967eda0f716", "b016df9d2bea4269cc59d531", "43d9544a09d068476a1a18df",
        "8d8d81032315f4fdc5a06798", "8cba3e2ec639a4aa2c26c4da", "59ca4829e098dfafa03888d2",
        "cb8a202d7107f39d860ccb38", "73febe7f3f61ca0913410ffc", "f1368cb3b69c3a4917c4f206",
        "88118e994f26cab3bee1521d", "5f62a924076e4b7c2320f2e8",
    ],
    "B": [
        "eb93814dd020bdc131d526aa", "551415adc72a84fa9e6bb571", "96ef708ef07d2b7bd8214653",
        "5a9a0bb3ffddd538e6fe3842", "b15dc178e2982874350db164", "5d3a863ab1f890166c5d96ff",
        "80e2780458250c3aa00dbb35", "fc14500bbc2900a04647083d", "3def8dacb60f04f15fc2271d",
        "7564e60e4c1c2f1b67a594a4", "2568ee0bfe2ee12f24d78f45", "33810921ad5c893b866b3951",
        "a0d05e28971f1ba57639b97d", "153839f368b80b6fbc36d29e", "cd2641c70bede98dac3945d0",
        "f6e51dc4e50e47ee9af50a4d", "4ada822d634041a721b346d5", "570d250679328757614dcbcb",
        "ffb63228a333c8b0755741ea", "ad9233f54a7132d905d6f29d",
    ],
}
MAP_EN = {
    "Ground Zero": "Ground Zero", "Customs": "Customs", "Factory": "Factory",
    "Woods": "Woods", "Shoreline": "Shoreline", "Interchange": "Interchange",
    "Reserve": "Reserve", "Lighthouse": "Lighthouse", "Streets": "Streets of Tarkov",
    "The Lab": "The Lab",
}
MAP_RU = {
    "Ground Zero": "Эпицентр", "Customs": "Таможня", "Factory": "Завод",
    "Woods": "Лес", "Shoreline": "Берег", "Interchange": "Развязка",
    "Reserve": "Резерв", "Lighthouse": "Маяк", "Streets": "Улицы Таркова",
    "The Lab": "Лаборатория",
}
SCAV_IDS = {"9a2efe4c869cd41beb667e29", "551415adc72a84fa9e6bb571"}
PMC_IDS = {
    "5c7e60f203900e75aba3edc0", "de92651782ad58fce6457af9",
    "43d9544a09d068476a1a18df", "fc14500bbc2900a04647083d",
    "33810921ad5c893b866b3951", "f6e51dc4e50e47ee9af50a4d",
}
DISTANCES = {
    "3ccca027eeebaf0a4dcfbb6f": ("<=", 25),
    "de92651782ad58fce6457af9": ("<=", 25),
    "96ef708ef07d2b7bd8214653": ("<=", 60),
    "8cba3e2ec639a4aa2c26c4da": ("<=", 25),
    "88118e994f26cab3bee1521d": ("<=", 25),
}
SURVIVE_AFTER = {
    "9620e5a3c17b30599df67730", "d7f67e37b0c244878aa685e0",
    "8d8d81032315f4fdc5a06798", "b15dc178e2982874350db164",
    "570d250679328757614dcbcb",
}
ONE_RAID = {"88118e994f26cab3bee1521d", "ffb63228a333c8b0755741ea"}
NIGHT_SUPPRESSED = "59ca4829e098dfafa03888d2"
SUCCESS_OVERRIDES = {
    "59ca4829e098dfafa03888d2": ("Night suppressed-sidearm qualification complete. The report is accepted.", "Ночная квалификация с пистолетом и глушителем завершена. Отчёт принят."),
    "73febe7f3f61ca0913410ffc": ("General-purpose machine-gun qualification complete. The report is accepted.", "Квалификация с единым пулемётом завершена. Отчёт принят."),
    "88118e994f26cab3bee1521d": ("Close-combat qualification complete. The report is accepted.", "Квалификация ближнего боя завершена. Отчёт принят."),
    "5f62a924076e4b7c2320f2e8": ("Support-weapon qualification complete. The report is accepted.", "Квалификация с оружием поддержки завершена. Отчёт принят."),
    "7564e60e4c1c2f1b67a594a4": ("9x39 special-purpose weapon qualification complete. The report is accepted.", "Квалификация со специальным оружием 9×39 завершена. Отчёт принят."),
    "4ada822d634041a721b346d5": ("Heavy special-purpose rifle qualification complete. The report is accepted.", "Квалификация с тяжёлой специальной винтовкой завершена. Отчёт принят."),
    "ffb63228a333c8b0755741ea": ("Modular and bullpup rifle qualification complete. The report is accepted.", "Квалификация с модульной винтовкой или булл-папом завершена. Отчёт принят."),
    "ad9233f54a7132d905d6f29d": ("Precision-rifle qualification complete. The report is accepted.", "Квалификация точной стрельбы завершена. Отчёт принят."),
}


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def save(path: Path, value, *, compact=False):
    indent = None if compact else 2
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":") if compact else None, indent=indent) + "\n", encoding="utf-8")


def stable_id(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:24]


def replace_plan_semantics(text: str, rows):
    lines = text.splitlines()
    for row in rows:
        for index, line in enumerate(lines):
            candidate = line.strip().rstrip(",")
            if not candidate.startswith("["):
                continue
            try:
                parsed = json.loads(candidate)
            except json.JSONDecodeError:
                continue
            if isinstance(parsed, list) and len(parsed) == 5 and parsed[:2] == row[:2]:
                comma = "," if line.rstrip().endswith(",") else ""
                indent = line[: len(line) - len(line.lstrip())]
                lines[index] = indent + json.dumps(row, ensure_ascii=False, separators=(",", ":")) + comma
                break
        else:
            raise ValueError(f"could not locate authored rotation plan row {row[:2]}")
    return "\n".join(lines) + "\n"


def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--spt-root", type=Path, required=True, help="Exact SPT runtime used to validate suppressor compatibility and item names")
    args = parser.parse_args()
    plan = load(PLAN_PATH)
    runtime = load(RUNTIME_PATH)
    quest_paths = {load(path)["_id"]: path for path in (ROOT / "db/quests").glob("*.json")}
    optional = load(ROOT / "manifests/optional-weapon-runtime.json")
    optional_by_pool = {}
    for item in optional["acceptedWeapons"]:
        optional_by_pool.setdefault(item["pool"], []).append(item)
    locales = {stem: {lang: load(ROOT / f"db/locales/{stem}-{lang}.json") for lang in ("en", "ru")} for stem in ("arsenal", "m8")}
    original_success = {}
    for quest_id in (row["id"] for row in runtime["assignments"]):
        for locale_set in locales.values():
            for lang, locale in locale_set.items():
                for suffix in ("successMessageText", "completePlayerMessage"):
                    key = f"{quest_id} {suffix}"
                    if key in locale:
                        original_success[(quest_id, lang, suffix)] = locale[key]

    for lane, lane_name, locale_stem in (("A", "A-close-support", "arsenal"), ("B", "B-rifle-precision", "m8")):
        for assignment, quest_id in zip(plan["lanes"][lane_name], IDS[lane]):
            order, pool, _level, locations, _semantics = assignment
            quest = load(quest_paths[quest_id])
            kill = next(node for node in walk(quest["conditions"]["AvailableForFinish"]) if node.get("conditionType") == "Kills")
            if quest_id in SCAV_IDS:
                kill["target"] = "Savage"
            elif quest_id in PMC_IDS:
                kill["target"] = "AnyPmc"
            if quest_id in DISTANCES:
                compare, metres = DISTANCES[quest_id]
                kill["distance"] = {"value": metres, "compareMethod": compare}
            if quest_id == NIGHT_SUPPRESSED:
                items_path = args.spt_root / "SPT_Runtime/SPT_Data/database/templates/items.json"
                if hashlib.sha256(items_path.read_bytes()).hexdigest() != plan["itemsDatabaseSha256"]:
                    raise ValueError("weapon rotation validation input does not match the pinned SPT 4.1.5 items database")
                items = load(items_path)
                suppressors = []
                per_weapon = {}
                for weapon_tpl in kill["weapon"]:
                    weapon = items.get(weapon_tpl)
                    if not weapon:
                        raise ValueError(f"A-15 weapon template {weapon_tpl} is missing from the exact SPT runtime")
                    for slot in weapon.get("_props", {}).get("Slots", []):
                        if slot.get("_name", "").startswith("mod_muzzle"):
                            for filter_row in slot.get("_props", {}).get("filters", []):
                                for tpl in filter_row.get("Filter", []):
                                    props = items.get(tpl, {}).get("_props", {})
                                    if props.get("muzzleModType") in {"silencer", "pms"}:
                                        suppressors.append(tpl)
                    per_weapon[weapon_tpl] = set(suppressors)
                    suppressors.clear()
                all_suppressors = set()
                for weapon_tpl, available in per_weapon.items():
                    if not available:
                        raise ValueError(f"A-15 weapon {weapon_tpl} has no compatible suppressor in exact SPT 4.1.5")
                    all_suppressors.update(available)
                kill["weaponModsInclusive"] = [[tpl] for tpl in sorted(all_suppressors)]
                kill["daytime"] = {"from": 22, "to": 5}
            elimination = next(node for node in quest["conditions"]["AvailableForFinish"] if node.get("conditionType") == "CounterCreator")
            if quest_id in ONE_RAID:
                elimination["oneSessionOnly"] = True
                kill["resetOnSessionEnd"] = True
            if quest_id in SURVIVE_AFTER:
                completion_id = stable_id(f"{quest_id}:survive-after-rotation-kills")
                if not any(node.get("id") == completion_id for node in quest["conditions"]["AvailableForFinish"]):
                    locations_runtime = [x for node in walk(quest["conditions"]["AvailableForFinish"]) if node.get("conditionType") == "Location" for x in (node.get("target") or [])]
                    if not locations_runtime:
                        raise ValueError(f"{quest_id}: cannot find authored location pool for survival objective")
                    next_index = max(node.get("index", -1) for node in quest["conditions"]["AvailableForFinish"]) + 1
                    quest["conditions"]["AvailableForFinish"].append({
                        "id": completion_id, "index": next_index, "dynamicLocale": False,
                        "globalQuestCounterId": "", "visibilityConditions": [{
                            "conditionType": "CompleteCondition", "id": stable_id(f"{quest_id}:survive-after-rotation-kills-visible"), "target": elimination["id"],
                        }], "parentId": "", "value": 1, "type": "Completion", "oneSessionOnly": False,
                        "isResetOnConditionFailed": False, "isNecessary": False,
                        "doNotResetIfCounterCompleted": False,
                        "counter": {"id": stable_id(f"{quest_id}:survive-after-rotation-kills-counter"), "conditions": [
                            {"conditionType": "ExitStatus", "dynamicLocale": False, "id": stable_id(f"{quest_id}:survive-after-rotation-kills-exit"), "status": ["Survived"]},
                            {"conditionType": "Location", "dynamicLocale": False, "id": stable_id(f"{quest_id}:survive-after-rotation-kills-location"), "target": list(dict.fromkeys(locations_runtime))},
                        ]}, "completeInSeconds": 0, "conditionType": "CounterCreator",
                    })

            semantics = assignment[4]
            if quest_id in SCAV_IDS:
                semantics = "Scav eliminations"
            elif quest_id in PMC_IDS:
                semantics = "PMC eliminations across raids"
            elif quest_id == "3ccca027eeebaf0a4dcfbb6f":
                semantics = "close-range eliminations within 25 metres"
            elif quest_id == "de92651782ad58fce6457af9":
                semantics = "close-range PMC eliminations within 25 metres"
            elif quest_id == "96ef708ef07d2b7bd8214653":
                semantics = "compact-rifle eliminations within 60 metres"
            elif quest_id == "8cba3e2ec639a4aa2c26c4da":
                semantics = "high-risk-map close eliminations within 25 metres"
            elif quest_id == "88118e994f26cab3bee1521d":
                semantics = "close-combat eliminations within 25 metres in one raid"
            elif quest_id in SURVIVE_AFTER:
                semantics = "eliminations followed by a survived extraction"
            elif quest_id in ONE_RAID:
                semantics = "all required eliminations in one raid"
            elif quest_id == NIGHT_SUPPRESSED:
                semantics = "night-time suppressed sidearm eliminations"
            elif quest_id in {"9a2efe4c869cd41beb667e29", "551415adc72a84fa9e6bb571"}:
                semantics = "Scav eliminations"
            elif quest_id in {"a006fa3e96a9c967eda0f716", "b016df9d2bea4269cc59d531", "80e2780458250c3aa00dbb35", "7564e60e4c1c2f1b67a594a4", "73febe7f3f61ca0913410ffc", "4ada822d634041a721b346d5"}:
                semantics = "eligible-weapon eliminations against any target"
            elif quest_id == "5a9a0bb3ffddd538e6fe3842":
                semantics = "Scav eliminations at 40 metres or farther"
            elif quest_id == "2568ee0bfe2ee12f24d78f45":
                semantics = "marksman-rifle eliminations across raids"
            elif quest_id == "33810921ad5c893b866b3951":
                semantics = "PMC eliminations with marksman rifles across raids"
            elif quest_id == "f6e51dc4e50e47ee9af50a4d":
                semantics = "PMC eliminations with intermediate-calibre rifles across raids"
            elif quest_id in {"8722d67f966ff1605393222a", "cb8a202d7107f39d860ccb38", "5d3a863ab1f890166c5d96ff"}:
                semantics = "eligible-weapon eliminations against any target"
            elif quest_id == "2568ee0bfe2ee12f24d78f45":
                semantics = "marksman-rifle eliminations across raids"
            elif quest_id == "33810921ad5c893b866b3951":
                semantics = "PMC marksman-rifle eliminations across raids"
            elif quest_id == "cd2641c70bede98dac3945d0":
                semantics = "Scav eliminations at 300 metres or farther"
            elif quest_id == "153839f368b80b6fbc36d29e":
                semantics = "Scav eliminations at 200 metres or farther"
            elif quest_id == "a0d05e28971f1ba57639b97d":
                semantics = "Scav eliminations at 100 metres or farther"
            assignment[4] = semantics
            runtime_assignment = next(item for item in runtime["assignments"] if item["id"] == quest_id)
            runtime_assignment["semantics"] = semantics

            if quest_id == NIGHT_SUPPRESSED:
                quest["QuestName"] = "Ротация «Арсенал» A-15: Ночные пистолеты с глушителем"
            save(quest_paths[quest_id], quest, compact=True)

            locale_stem = "m8" if quest_paths[quest_id].name.startswith("40-") else "arsenal"
            loc_set = locales[locale_stem]
            weapon_ids = plan["pools"][pool]
            optional_rows = optional_by_pool.get(pool, [])
            for lang, name_map, map_names in (("en", "en", MAP_EN), ("ru", "ru", MAP_RU)):
                global_locale = args.spt_root / "SPT_Runtime/SPT_Data/database/locales/global" / f"{lang}.json"
                names = load(global_locale)
                weapon_names = [names.get(tpl + " Name", tpl) for tpl in weapon_ids]
                optional_names = [item["name"] if lang == "en" else item["nameRu"] for item in optional_rows]
                if quest_id in SCAV_IDS:
                    objective = "Target: Scavs." if lang == "en" else "Цели: дикие."
                elif quest_id in PMC_IDS:
                    objective = "Target: PMCs." if lang == "en" else "Цели: ЧВК."
                elif quest_id == "5a9a0bb3ffddd538e6fe3842":
                    objective = "Target: Scavs, at least 40 m away." if lang == "en" else "Цели: дикие не ближе 40 м."
                elif quest_id in {"a0d05e28971f1ba57639b97d", "153839f368b80b6fbc36d29e", "cd2641c70bede98dac3945d0"}:
                    metres = {"a0d05e28971f1ba57639b97d": 100, "153839f368b80b6fbc36d29e": 200, "cd2641c70bede98dac3945d0": 300}[quest_id]
                    objective = f"Target: Scavs, at least {metres} m away." if lang == "en" else f"Цели: дикие не ближе {metres} м."
                elif quest_id == NIGHT_SUPPRESSED:
                    objective = "Use an attached or integral suppressor; raid time 22:00–05:00." if lang == "en" else "Используйте установленный или встроенный глушитель; время рейда 22:00–05:00."
                elif quest_id in SURVIVE_AFTER:
                    objective = "After the eliminations, survive and extract on an eligible map." if lang == "en" else "После устранений выживите и эвакуируйтесь на одной из доступных карт."
                elif quest_id in ONE_RAID:
                    if quest_id == "88118e994f26cab3bee1521d":
                        objective = "Eliminate targets within 25 m in a single raid." if lang == "en" else "Устраняйте противников не дальше 25 м за один рейд."
                    else:
                        objective = "Complete the eliminations in a single raid." if lang == "en" else "Выполните условие по устранениям за один рейд."
                elif quest_id == "3ccca027eeebaf0a4dcfbb6f":
                    objective = "Target: any enemy within 25 m." if lang == "en" else "Цели: любые противники не дальше 25 м."
                elif quest_id == "de92651782ad58fce6457af9":
                    objective = "Target: PMCs within 25 m." if lang == "en" else "Цели: ЧВК не дальше 25 м."
                elif quest_id == "96ef708ef07d2b7bd8214653":
                    objective = "Target: any enemy within 60 m." if lang == "en" else "Цели: любые противники не дальше 60 м."
                elif quest_id == "8cba3e2ec639a4aa2c26c4da":
                    objective = "Target: any enemy within 25 m." if lang == "en" else "Цели: любые противники не дальше 25 м."
                else:
                    objective = "Target: any enemy." if lang == "en" else "Цели: любые противники."
                map_label = "Eligible maps" if lang == "en" else "Доступные карты"
                maps = "Any map" if locations == ["any"] and lang == "en" else "Любая карта" if locations == ["any"] else ", ".join(map_names[x] for x in locations)
                weapon_label = "Eligible weapons" if lang == "en" else "Разрешённое оружие"
                detail = (f"Operational detail:\n{objective}\n{map_label}: {maps}.\n{weapon_label}:\n- {', '.join(weapon_names)}." if lang == "en"
                          else f"Уточнение:\n{objective}\n{map_label}: {maps}.\n{weapon_label}:\n- {', '.join(weapon_names)}.")
                if optional_names:
                    optional_label = "Optional WTT models" if lang == "en" else "Дополнительные модели WTT"
                    detail += f"\n{optional_label}:\n- {', '.join(optional_names)}."
                locale = loc_set[lang]
                name = (f"Arsenal Rotation {lane}-{order}: " + {
                    "service-pistols": "Service Pistols", "compact-ak": "Compact Kalashnikov Rifles",
                    "manual-shotguns": "Manual Shotguns", "early-smg": "Early Submachine Guns",
                    "forty-five-pistols": ".45 ACP Pistols", "service-smg": "Service Submachine Guns",
                    "automatic-pistols": "Automatic Pistols", "compact-pdw": "Compact PDWs",
                    "autoloading-shotguns": "Self-loading Shotguns", "modern-smg": "Modern Submachine Guns",
                    "high-velocity-pistols": "High-velocity Pistols", "special-pdw": "Special-purpose PDWs",
                    "unusual-shotguns": "Specialized Shotguns", "magnum-sidearms": "Magnum Sidearms",
                    "suppressed-sidearms": "Night Suppressed Sidearms", "light-machine-guns": "Light Machine Guns",
                    "general-machine-guns": "General-purpose Machine Guns", "grenade-launchers": "Grenade Launchers",
                    "close-mastery": "Close-combat Mastery", "support-mastery": "Support Weapon Mastery",
                    "starter-service-rifles": "Starter Service Rifles", "civilian-ak": "Civilian Kalashnikov Carbines",
                    "compact-rifles": "Compact Assault Rifles", "sks-hunter": "Self-loading Carbines",
                    "classic-ak-545": "Classic 5.45 Kalashnikov Rifles", "classic-ak-762": "Classic 7.62 Kalashnikov Rifles",
                    "nato-service-rifles": "NATO Service Rifles", "nato-alternatives": "Alternative NATO Rifles",
                    "battle-rifles": "Battle Rifles", "nine-by-thirty-nine": "9x39 Special-purpose Weapons",
                    "entry-dmr": "Starter Marksman Rifles", "service-dmr": "Service Marksman Rifles",
                    "entry-bolt-action": "Starter Bolt-action Rifles", "nato-bolt-action": "NATO Bolt-action Rifles",
                    "magnum-precision": "Magnum Precision Rifles", "advanced-intermediate": "Advanced Intermediate-calibre Rifles",
                    "heavy-special-rifles": "Heavy Special-purpose Rifles", "modern-ak": "Modern Kalashnikov Rifles",
                    "western-bullpup-modular": "Modular and Bullpup Rifles", "precision-mastery": "Precision Mastery",
                }[pool]) if lang == "en" else quest["QuestName"]
                locale[f"{quest_id} name"] = name
                locale[f"{quest_id} description"] = detail
                locale[f"{quest_id} startedMessageText"] = detail
                locale[f"{quest_id} acceptPlayerMessage"] = detail
                if quest_id in SUCCESS_OVERRIDES:
                    success = SUCCESS_OVERRIDES[quest_id][0 if lang == "en" else 1]
                    locale[f"{quest_id} successMessageText"] = success
                    locale[f"{quest_id} completePlayerMessage"] = success
                else:
                    for suffix in ("successMessageText", "completePlayerMessage"):
                        previous = original_success.get((quest_id, lang, suffix))
                        if previous:
                            locale[f"{quest_id} {suffix}"] = previous
                if quest_id in SURVIVE_AFTER:
                    survival = next(node for node in quest["conditions"]["AvailableForFinish"] if node.get("id") == stable_id(f"{quest_id}:survive-after-rotation-kills"))
                    locale[survival["id"]] = "Survive and extract on an eligible map" if lang == "en" else "Выжить и эвакуироваться на доступной карте"

    original_plan = PLAN_PATH.read_text(encoding="utf-8")
    PLAN_PATH.write_text(replace_plan_semantics(original_plan, [row for lane in plan["lanes"].values() for row in lane]), encoding="utf-8")
    quality_path = ROOT / "manifests/quest-quality-runtime.json"
    quality = load(quality_path)
    for quest_id in SURVIVE_AFTER:
        if quest_id in quality.get("quests", {}):
            quest = load(quest_paths[quest_id])
            quality["quests"][quest_id]["finishConditionIds"] = [row["id"] for row in quest["conditions"]["AvailableForFinish"]]
    save(quality_path, quality)
    save(RUNTIME_PATH, runtime)
    for stem in locales:
        for lang in locales[stem]:
            save(ROOT / f"db/locales/{stem}-{lang}.json", locales[stem][lang])


if __name__ == "__main__":
    main()
