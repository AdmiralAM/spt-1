#!/usr/bin/env python3
"""Materialize the approved 100-quest story campaign using native SPT conditions."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRADER = "d5c27bb3169f8dfbc13f6b69"
ROUBLES = "5449016a4bdc2d6f028b456f"
MARKER = "5991b51486f77447b112d44f"
STORY_UNLOCKS = {
    "Эпицентр": ("590a358486f77429692b2790", 18500, 4, 2),
    "Таможня": ("619cbf7d23893217ec30b689", 42000, 3, 2),
    "Лес": ("618ba27d9008e4636a67f61d", 68000, 2, 1),
    "Развязка": ("5e2aee0a86f774755a234b62", 55000, 3, 1),
    "Берег": ("60098ad7c2240c0fe85c570a", 72000, 2, 1),
    "Резерв": ("5d03794386f77420415576f5", 62000, 2, 1),
    "Маяк": ("62e910aaf957f2915e0a5e36", 78000, 2, 1),
    "Улицы": ("660bbc47c38b837877075e47", 85000, 2, 1),
    "Завод": ("5d1b5e94d7ad1a2b865a96b0", 39000, 3, 1),
    "Лаборатория": ("68666887df54e1190902df57", 125000, 1, 1),
}


def hid(value: str) -> str:
    return hashlib.sha256(("admiral-story-runtime:" + value).encode()).hexdigest()[:24]


VISIT_ZONES = {
    "Эпицентр": ["Sandbox_1_MedicalArea_exploration", "Sandbox_2_Kord_exploration", "Sandbox_2_AGS_exploration", "Sandbox_3_Vino_exploration", "Sandbox_5_DeadGroup_exploration", "Sandbox_5_Laborant_exploration", "Sandbox_5_Office_exploration"],
    "Таможня": ["room214", "room206_water", "vaz_feld", "room114", "dead_posylni", "vremyan_case", "bomj_place", "gazel", "place_SADOVOD_03", "exit777"],
    "Лес": ["bunker2", "ter_015_area_1", "huntsman_001", "pr_scout_col", "pr_scout_base", "Depo_Zone_1", "Depo_Zone_2", "Depo_Zone_3", "Lost_caravan", "Bunker_enter"],
    "Развязка": ["place_SALE_03_AVOKADO", "place_SALE_03_KOSTIN", "place_SALE_03_TREND", "place_SALE_03_DINO", "place_SALE_03_TOPBRAND", "place_WARBLOOD_04_1", "place_WARBLOOD_04_2", "place_WARBLOOD_04_3", "place_merch_21_1", "place_merch_21_2"],
    "Берег": ["place_peacemaker_008_4_N1", "place_peacemaker_008_4_N2", "place_peacemaker_001", "place_peacemaker_004_N1", "place_peacemaker_004_N2", "place_peacemaker_005_N1", "place_peacemaker_005_N2", "place_peacemaker_007_1_N1", "place_peacemaker_009_2", "place_peacemaker_009_3_N1"],
    "Резерв": ["huntsman_029", "prapor_024_area_2", "prapor_024_area_1", "prapor_025_area_1", "prapor_025_area_2", "prapor_025_area_3", "prapor_025_area_4", "tadeush_bmp2_area_check_2", "tadeush_bmp2_area_check_11", "tadeush_bmp2_area_check_12"],
    "Маяк": ["qlight_pr1_heli1_find", "qlight_find_scav_group1", "qlight_find_crushed_heli", "qlight_hunt_fr_find", "meh_48_transponder_area_check_1", "meh_50_visit_area_check_1", "qlight_extension_medic1_exploration1", "qlight_extension_prapor1_exploration1", "qlight_extension_prapor1_exploration2", "qlight_extension_prapor1_exploration3"],
    "Улицы": ["quest_zone_c5_mar", "quest_zone_c6_kpss", "quest_zone_c7_mel", "quest_zone_c8_dom1", "quest_zone_c8_dom2", "quest_zone_c11_gmed", "quest_zone_c16_koll_1", "quest_zone_c16_koll_2", "quest_zone_c21_look", "quest_zone_c25_cinem"],
    "Завод": ["locked_office", "Check_mine_zone_factory", "ter_017_area_1"],
    "Лаборатория": ["peace_027_area", "Halloween_lab_section1", "Halloween_lab_section2", "Halloween_closed_places"],
}

PLACE_ZONES = {
    "Эпицентр": ["nt2024_5_throtil_epicentr"],
    "Таможня": ["gazel", "fuel1", "fuel2", "fuel3", "fuel4", "TerragroupBOX_2", "TerragroupBOX_4"],
    "Лес": ["bar_fuel3_1", "bar_fuel3_2", "bar_fuel3_3", "meh_45_radio_area_mark_1", "meh_45_radio_area_mark_2", "meh_45_radio_area_mark_3"],
    "Развязка": ["place_WARBLOOD_04_1", "place_WARBLOOD_04_2", "place_WARBLOOD_04_3", "place_merch_21_1", "place_merch_21_2", "place_merch_21_3"],
    "Берег": ["place_peacemaker_008_2_N1", "place_peacemaker_008_2_N2", "place_peacemaker_003_N1", "place_peacemaker_003_N2", "place_peacemaker_003_N3", "place_SIGNAL_03_1"],
    "Резерв": ["tadeush_bmp2_area_mark_2", "tadeush_bmp2_area_mark_11", "tadeush_bmp2_area_mark_12", "tadeush_bmp2_area_mark_13", "tadeush_stryker_area_mark_3"],
    "Маяк": ["qlight_pr1_heli1_mark", "qlight_mark_vech1", "qlight_mark_vech2", "qlight_mark_vech3", "meh_42_radio_area_mark_1", "meh_42_radio_area_mark_2"],
    "Улицы": ["quest_zone_place_c14_revx_1", "quest_zone_place_c14_revx_2", "quest_zone_place_c14_revx_3", "quest_zone_keeper10_place", "Mark_BTR_1", "Mark_BTR_2"],
    "Завод": ["ter_017_area_1", "Place_accurate_tools"],
    "Лаборатория": ["quest_city_trotil2"],
}

RECOVERY_ITEMS = {
    "Эпицентр": ["63a0b2eabea67a6d93009e52", "590c392f86f77444754deb29", "590c621186f774138d11ea29"],
    "Таможня": ["590c645c86f77412b01304d9", "590c621186f774138d11ea29", "5c12613b86f7743bbe2c3f76"],
    "Лес": ["5c052f6886f7746b1e3db148", "590c651286f7741e566b6461", "5c12613b86f7743bbe2c3f76"],
    "Развязка": ["590c392f86f77444754deb29", "590c621186f774138d11ea29", "5c12613b86f7743bbe2c3f76"],
    "Берег": ["619cc01e0a7c3a1a2731940c", "590c621186f774138d11ea29", "61bf7c024770ee6f9c6b8b53"],
    "Резерв": ["5d0376a486f7747d8050965c", "62a0a16d0b9d3c46de5b6e97", "5c12613b86f7743bbe2c3f76"],
    "Маяк": ["62e910aaf957f2915e0a5e36", "5c052f6886f7746b1e3db148", "5ac78a9b86f7741cca0bbd8d"],
    "Улицы": ["590c645c86f77412b01304d9", "660bbc47c38b837877075e47", "5c12613b86f7743bbe2c3f76"],
    "Завод": ["5d0376a486f7747d8050965c", "590c392f86f77444754deb29", "590c621186f774138d11ea29"],
    "Лаборатория": ["61bf7c024770ee6f9c6b8b53", "660bbc47c38b837877075e47", "68666887df54e1190902df57"],
}

REWARD_ITEMS = {
    "Эпицентр": ["590a358486f77429692b2790", "5b4391a586f7745321235ab2", "63a0b2eabea67a6d93009e52"],
    "Таможня": ["619cbf7d23893217ec30b689", "591094e086f7747caa7bb2ef", "5d1b5e94d7ad1a2b865a96b0"],
    "Лес": ["5c052f6886f7746b1e3db148", "5b4391a586f7745321235ab2", "618ba27d9008e4636a67f61d"],
    "Развязка": ["5e2aee0a86f774755a234b62", "5d1b309586f77425227d1676", "5c12620d86f7743f8b198b72"],
    "Берег": ["60098ad7c2240c0fe85c570a", "5c0e530286f7747fa1419862", "5ed51652f6c34d2cc26336a1"],
    "Резерв": ["5d0376a486f7747d8050965c", "5d03794386f77420415576f5", "5c12613b86f7743bbe2c3f76"],
    "Маяк": ["5ac78a9b86f7741cca0bbd8d", "5c052f6886f7746b1e3db148", "62e910aaf957f2915e0a5e36"],
    "Улицы": ["590c392f86f77444754deb29", "5c12613b86f7743bbe2c3f76", "660bbc47c38b837877075e47"],
    "Завод": ["5d1b5e94d7ad1a2b865a96b0", "5d0376a486f7747d8050965c", "619cbf7d23893217ec30b689"],
    "Лаборатория": ["5ed51652f6c34d2cc26336a1", "62a0a16d0b9d3c46de5b6e97", "68666887df54e1190902df57"],
}

CHAIN_EN = ["First Circuit", "Missing Convoy", "Observation Net", "Dead Warehouse", "Sanitary Corridor", "Mobilization Protocol", "Coastal Blockade", "Archive of Collapse", "Black Shift", "Final Protocol"]
CODENAMES_EN = [
    ["Foreign Frequency", "Zero Mark", "Last Crew", "Locked Airwaves", "Blind Spot", "Uninvited Listeners", "Reserve Power", "Control Package", "Open Channel", "First Circuit"],
    ["Convoy Tracks", "Driverless Truck", "Dispatcher Key", "Night Manifest", "False Labels", "Extra Middleman", "False Bottom", "Broken Transfer", "Recipient", "Close the Route"],
    ["Old Bearings", "Optics in the Grass", "Quiet Installation", "Patrol Map", "Missing Observer", "Distance Check", "False Campfire", "Frequency Change", "Clean Withdrawal", "See Farther"],
    ["Closed Displays", "Service Entrance", "No Power", "Warehouse Ledger", "Wrong Crates", "Security Archive", "After Closing", "Foreign Inventory", "Wall Sample", "Our Shelf"],
    ["Unanswered Call", "Empty Stretcher", "Reserve Office", "Cold Chain", "Environmental Control", "Wrong Patient", "Debt Returned", "Clean Ward", "Last Sample", "Sanitary Corridor"],
    ["Alarm Signal", "Duty Officer", "Lower Level", "Dead Terminal", "Mobilization File", "Accounted Hardware", "Signal Intercept", "Sealed Crate", "Heavy Exit", "Admiral Reserve"],
    ["Far Shore", "Silent Observer", "Control the Height", "Tide Schedule", "Behind the Line", "Foreign Navigation", "False Coordinates", "Blockade Control", "Last Boat", "Coast Closed"],
    ["Old Address", "Archive Card", "Official Seal", "Vacant Room", "Three Witnesses", "Quiet Mailboxes", "Intercepted Package", "Erase the Address", "Living List", "City Network"],
    ["After the Whistle", "Shift Log", "Foreman Key", "Technical Floor", "Batch Number", "Unfinished Part", "Quality Control", "Stop the Line", "Reference Sample", "Black Shift Ends"],
    ["Restricted Clearance", "Empty Team", "Backup Copy", "Cold Sector", "Leak Control", "Internal Security", "Clean Room", "Substitution", "Final Container", "Admiral's Decision"],
]

NATALYA_QUESTS = {(1, 3), (1, 7), (2, 6), (4, 2), (4, 4), (4, 9), (4, 10), (5, 2), (5, 6), (5, 9), (7, 2), (7, 6), (8, 4), (8, 6), (8, 9), (10, 2), (10, 7), (10, 10)}


def level_condition(qid: str, level: int, index: int = 0) -> dict:
    return {"id": hid(f"{qid}:level"), "index": index, "compareMethod": ">=", "dynamicLocale": False, "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "", "value": level, "conditionType": "Level"}


def prerequisite(qid: str, target: str, index: int) -> dict:
    return {"id": hid(f"{qid}:pre:{target}"), "index": index, "dynamicLocale": False, "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "", "target": target, "status": [4], "availableAfter": 0, "dispersion": 0, "conditionType": "Quest"}


def counter(qid: str, suffix: str, value: int, conditions: list[dict], qtype: str, index: int, one: bool = False) -> dict:
    return {"id": hid(f"{qid}:{suffix}:finish"), "index": index, "dynamicLocale": False, "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "", "value": value, "type": qtype, "oneSessionOnly": one, "isResetOnConditionFailed": False, "isNecessary": False, "doNotResetIfCounterCompleted": False, "counter": {"id": hid(f"{qid}:{suffix}:counter"), "conditions": conditions}, "completeInSeconds": 0, "conditionType": "CounterCreator"}


def location(qid: str, suffix: str, maps: list[str]) -> dict:
    return {"id": hid(f"{qid}:{suffix}:location"), "dynamicLocale": False, "conditionType": "Location", "target": maps}


def visit(qid: str, suffix: str, zone: str) -> dict:
    return {"id": hid(f"{qid}:{suffix}:visit"), "dynamicLocale": False, "conditionType": "VisitPlace", "target": zone, "value": 1}


def item_condition(qid: str, suffix: str, tpl: str, kind: str, index: int, fir: bool) -> dict:
    return {"conditionType": kind, "countInRaid": False, "dogtagLevel": 0, "dynamicLocale": False, "globalQuestCounterId": "", "id": hid(f"{qid}:{suffix}:{kind}"), "index": index, "isEncoded": False, "maxDurability": 100, "minDurability": 0, "onlyFoundInRaid": fir, "parentId": "", "target": [tpl], "value": 1, "visibilityConditions": []}


def build_finish(q: dict, chain: dict, names_en: dict[str, str], names_ru: dict[str, str]) -> tuple[list[dict], list[str], list[str]]:
    qid, map_name, maps = q["id"], chain["map"], chain["runtimeLocations"]
    visits, places = VISIT_ZONES[map_name], PLACE_ZONES[map_name]
    item_tpl = RECOVERY_ITEMS[map_name][(q["order"] - 1) % 3]
    rows, en, ru = [], [], []
    index = 0
    for objective_index, objective in enumerate(q["objectives"]):
        kind, quantity = objective["kind"], int(objective["quantity"])
        if kind == "visit":
            for n in range(quantity):
                zone = visits[(q["order"] + objective_index + n - 1) % len(visits)]
                suffix = f"visit-{objective_index}-{n}"
                rows.append(counter(qid, suffix, 1, [location(qid, suffix, maps), visit(qid, suffix, zone)], "Exploration", index))
                en.append(f"Inspect operational point {n + 1}/{quantity} on {chain['title'].split(':')[0]}")
                ru.append(f"Осмотреть оперативную точку {n + 1}/{quantity} на карте «{map_name}»")
                index += 1
        elif kind == "placeOrMark":
            for n in range(quantity):
                zone = places[(q["order"] + objective_index + n - 1) % len(places)]
                rows.append({"conditionType": "PlaceBeacon", "dynamicLocale": False, "globalQuestCounterId": "", "id": hid(f"{qid}:place:{objective_index}:{n}"), "index": index, "parentId": "", "plantTime": 10, "target": [MARKER], "value": 1, "visibilityConditions": [], "zoneId": zone})
                en.append(f"Place an MS2000 marker at objective {n + 1}/{quantity}; the marker is consumed")
                ru.append(f"Установить маркер MS2000 в точке {n + 1}/{quantity}; маркер расходуется")
                index += 1
        elif kind == "retrieveQuestItem":
            rows.append(item_condition(qid, f"recover-{objective_index}", item_tpl, "FindItem", index, True)); index += 1
            rows.append(item_condition(qid, f"recover-{objective_index}", item_tpl, "HandoverItem", index, True)); index += 1
            en.append(f"Find 1 × {names_en.get(item_tpl, item_tpl)}. Found in raid: required")
            ru.append(f"Найти 1 × {names_ru.get(item_tpl, item_tpl)}. Статус «Найдено в рейде»: требуется")
            en.append(f"Hand over 1 × {names_en.get(item_tpl, item_tpl)}. Found in raid: required")
            ru.append(f"Передать 1 × {names_ru.get(item_tpl, item_tpl)}. Статус «Найдено в рейде»: требуется")
        elif kind == "handover":
            rows.append(item_condition(qid, f"handover-{objective_index}", item_tpl, "HandoverItem", index, False)); index += 1
            en.append(f"Hand over 1 × {names_en.get(item_tpl, item_tpl)}. Found in raid: not required")
            ru.append(f"Передать 1 × {names_ru.get(item_tpl, item_tpl)}. Статус «Найдено в рейде»: не требуется")
        elif kind == "eliminate":
            suffix = f"kill-{objective_index}"
            kill = {"id": hid(f"{qid}:{suffix}:kill"), "dynamicLocale": False, "target": {"Scav": "Savage", "Rogue": "Any", "Raider": "Any"}.get(objective.get("target"), "Any"), "compareMethod": ">=", "value": 1, "weapon": [], "distance": {"value": 0, "compareMethod": ">="}, "weaponModsInclusive": [], "weaponModsExclusive": [], "enemyEquipmentInclusive": [], "enemyEquipmentExclusive": [], "weaponCaliber": [], "savageRole": ["exUsec"] if objective.get("target") == "Rogue" else (["pmcBot"] if objective.get("target") == "Raider" else []), "bodyPart": [], "daytime": {"from": 0, "to": 0}, "conditionType": "Kills", "enemyHealthEffects": [], "resetOnSessionEnd": False}
            rows.append(counter(qid, suffix, quantity, [kill, location(qid, suffix, maps)], "Elimination", index)); index += 1
            target = {"Scav": "Scavs", "Rogue": "Rogues", "Raider": "Raiders"}.get(objective.get("target"), "hostile targets")
            target_ru = {"Scav": "Диких", "Rogue": "Отступников", "Raider": "Рейдеров"}.get(objective.get("target"), "противников")
            en.append(f"Eliminate {quantity} {target} on {chain['title'].split(':')[0]}; progress carries across raids")
            ru.append(f"Устранить {quantity} {target_ru} на карте «{map_name}»; прогресс сохраняется между рейдами")
        elif kind == "surviveExtract":
            suffix = f"survive-{objective_index}"
            exit_condition = {"id": hid(f"{qid}:{suffix}:exit"), "dynamicLocale": False, "conditionType": "ExitStatus", "status": ["Survived"]}
            rows.append(counter(qid, suffix, 1, [location(qid, suffix, maps), exit_condition], "Completion", index, True)); index += 1
            en.append(f"Survive and extract from {chain['title'].split(':')[0]} in one raid")
            ru.append(f"Выжить и эвакуироваться с карты «{map_name}» в одном рейде")
    return rows, en, ru


def rewards(q: dict, map_name: str) -> list[dict]:
    result = [
        {"value": q["rewards"]["xp"], "id": hid(f"{q['id']}:xp"), "type": "Experience", "index": 0},
        {"value": q["rewards"]["standing"], "id": hid(f"{q['id']}:standing"), "type": "TraderStanding", "target": TRADER, "index": 1},
    ]
    money_id = hid(f"{q['id']}:roubles:item")
    result.append({"value": q["rewards"]["roubles"], "id": hid(f"{q['id']}:roubles"), "type": "Item", "target": money_id, "index": 2, "items": [{"_id": money_id, "_tpl": ROUBLES, "upd": {"StackObjectsCount": q["rewards"]["roubles"]}}]})
    if q["order"] in (3, 6, 10):
        tpl = REWARD_ITEMS[map_name][{3: 0, 6: 1, 10: 2}[q["order"]]]
        item_id = hid(f"{q['id']}:thematic:item")
        result.append({"value": 1, "id": hid(f"{q['id']}:thematic"), "type": "Item", "target": item_id, "index": 3, "items": [{"_id": item_id, "_tpl": tpl, "upd": {"StackObjectsCount": 1}}]})
    if q["rewards"].get("assortmentUnlock"):
        result.append({"value": 1, "id": hid(f"{q['id']}:unlock"), "type": "AssortmentUnlock", "target": hid(f"story-unlock:{map_name}"), "index": len(result)})
    return result


def locale_set(q: dict, chain: dict, en_name: str, objective_en: list[str], objective_ru: list[str], specialist: bool) -> tuple[dict, dict]:
    qid = q["id"]
    speaker_en = "Natalya has isolated a lead inside Admiral's network." if specialist else "Admiral has assigned the next operation."
    speaker_ru = "Наталья выделила новую зацепку внутри сети Адмирала." if specialist else "Адмирал назначил следующую операцию."
    reward_en = f"Rewards:\n- {q['rewards']['xp']} XP\n- ₽{q['rewards']['roubles']}\n- +{q['rewards']['standing']:.3f} Admiral standing"
    reward_ru = f"Награды:\n- {q['rewards']['xp']} XP\n- ₽{q['rewards']['roubles']}\n- +{q['rewards']['standing']:.3f} репутации Адмирала"
    en_body = f"{speaker_en}\n\nSituation:\n{en_name}. Complete the listed field operation and return the result to Admiral. Operation takes place on {chain['title'].split(':')[0]}.\n\nRequirements:\n" + "\n".join(f"- {x}" for x in objective_en) + f"\n\n{reward_en}"
    ru_body = f"{speaker_ru}\n\nОбстановка:\n{q['brief']} Операция проводится на карте «{chain['map']}».\n\nТребования:\n" + "\n".join(f"- {x}" for x in objective_ru) + f"\n\n{reward_ru}"
    done_en = "The operation is complete. The recovered result has been logged."
    done_ru = "Операция завершена. Полученный результат принят и внесён в журнал."
    def make(name: str, body: str, done: str, reward: str, objective_lines: list[str]) -> dict:
        return {qid + " name": name, qid + " description": body, qid + " note": "", qid + " startedMessageText": body, qid + " successMessageText": done + "\n\n" + reward, qid + " failMessageText": "", qid + " acceptPlayerMessage": body, qid + " declinePlayerMessage": "", qid + " completePlayerMessage": done, qid + " changeQuestMessageText": "", **{row["id"]: objective_lines[i] for i, row in enumerate(q["runtimeFinish"]) if i < len(objective_lines)}}
    return make(en_name, en_body, done_en, reward_en, objective_en), make(q["name"], ru_body, done_ru, reward_ru, objective_ru)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--spt-root", type=Path, required=True)
    args = parser.parse_args()
    authored = json.loads((ROOT / "manifests/campaign-authored-100-review.json").read_text(encoding="utf-8"))
    items = json.loads((args.spt_root / "SPT_Data/database/templates/items.json").read_text(encoding="utf-8-sig"))
    global_en = json.loads((args.spt_root / "SPT_Data/database/locales/global/en.json").read_text(encoding="utf-8-sig"))
    global_ru = json.loads((args.spt_root / "SPT_Data/database/locales/global/ru.json").read_text(encoding="utf-8-sig"))
    names_en = {tpl: global_en.get(tpl + " Name", tpl) for tpl in items}
    names_ru = {tpl: global_ru.get(tpl + " Name", names_en[tpl]) for tpl in items}
    required_tpls = {MARKER, ROUBLES} | {tpl for rows in RECOVERY_ITEMS.values() for tpl in rows} | {tpl for rows in REWARD_ITEMS.values() for tpl in rows}
    missing = sorted(required_tpls - set(items))
    if missing:
        raise SystemExit(f"SPT 4.1.5 item IDs missing: {missing}")

    chains = authored["chains"]
    final_ids = {chain["chain"]: chain["quests"][-1]["id"] for chain in chains}
    runtime_rows, en, ru = [], {}, {}
    for chain in chains:
        for q in chain["quests"]:
            start = [level_condition(q["id"], q["level"])]
            prereqs = ([q["prerequisite"]] if q["prerequisite"] else []) + [final_ids[row["chain"]] for row in q["crossChainPrerequisites"]]
            start.extend(prerequisite(q["id"], target, index + 1) for index, target in enumerate(prereqs))
            finish, objective_en, objective_ru = build_finish(q, chain, names_en, names_ru)
            q["runtimeFinish"] = finish
            specialist = (chain["chain"], q["order"]) in NATALYA_QUESTS
            template = {"QuestName": q["name"], "_id": q["id"], "canShowNotificationsInGame": True, "conditions": {"AvailableForFinish": finish, "AvailableForStart": start, "Fail": []}, "description": q["id"] + " description", "failMessageText": q["id"] + " failMessageText", "name": q["id"] + " name", "note": q["id"] + " note", "traderId": TRADER, "location": "any", "image": "/files/quest/icon/5a27cafa86f77424e20615d6.jpg", "type": "PickUp" if any(o["kind"] == "retrieveQuestItem" for o in q["objectives"]) else ("Elimination" if any(o["kind"] == "eliminate" for o in q["objectives"]) else "Exploration"), "isKey": False, "restartable": False, "instantComplete": False, "secretQuest": False, "startedMessageText": q["id"] + " startedMessageText", "successMessageText": q["id"] + " successMessageText", "acceptPlayerMessage": q["id"] + " acceptPlayerMessage", "acceptanceAndFinishingSource": "eft", "declinePlayerMessage": q["id"] + " declinePlayerMessage", "completePlayerMessage": q["id"] + " completePlayerMessage", "changeQuestMessageText": q["id"] + " changeQuestMessageText", "rewards": {"Started": [], "Success": rewards(q, chain["map"]), "Fail": []}, "side": "Pmc", "status": 0, "progressSource": "eft", "gameModes": [], "rankingModes": [], "arenaLocations": []}
            runtime_rows.append((chain, q, template, specialist))
            en_name = f"{CHAIN_EN[chain['chain'] - 1]} {q['order']}: {CODENAMES_EN[chain['chain'] - 1][q['order'] - 1]}"
            en_set, ru_set = locale_set(q, chain, en_name, objective_en, objective_ru, specialist)
            en.update(en_set); ru.update(ru_set)

    quest_dir = ROOT / "db/quests"
    for path in quest_dir.glob("60-*.json"):
        path.unlink()
    for chain, q, template, _ in runtime_rows:
        path = quest_dir / f"60-{chain['chain']:02d}-{q['order']:02d}-{q['id']}.json"
        path.write_text(json.dumps(template, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")
    (ROOT / "db/locales/story-en.json").write_text(json.dumps(en, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (ROOT / "db/locales/story-ru.json").write_text(json.dumps(ru, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    unlock_items, unlock_barter, unlock_loyalty, unlock_rows = [], {}, {}, []
    questassort_path = ROOT / "db/questassort.json"
    questassort = json.loads(questassort_path.read_text(encoding="utf-8"))
    old_manifest_path = ROOT / "manifests/story-campaign-runtime.json"
    old = json.loads(old_manifest_path.read_text(encoding="utf-8")) if old_manifest_path.exists() else {}
    for row in old.get("assortmentUnlocks", []):
        questassort["success"].pop(row["offerId"], None)
    for chain in authored["chains"]:
        final = chain["quests"][-1]
        tpl, price, stock, buy = STORY_UNLOCKS[chain["map"]]
        offer_id = hid(f"story-unlock:{chain['map']}")
        level = min(4, 1 + (chain["chain"] - 1) // 3)
        unlock_items.append({"_id": offer_id, "_tpl": tpl, "parentId": "hideout", "slotId": "hideout", "upd": {"UnlimitedCount": False, "StackObjectsCount": stock, "BuyRestrictionMax": buy, "BuyRestrictionCurrent": 0}})
        unlock_barter[offer_id] = [[{"count": price, "_tpl": ROUBLES}]]
        unlock_loyalty[offer_id] = level
        questassort["success"][offer_id] = final["id"]
        unlock_rows.append({"chain": chain["chain"], "map": chain["map"], "questId": final["id"], "offerId": offer_id, "tpl": tpl, "priceRub": price, "stockPerReset": stock, "buyRestriction": buy, "loyaltyLevel": level})
    assort_path = ROOT / "db/assort.json"
    assort = json.loads(assort_path.read_text(encoding="utf-8"))
    old_offer_ids = {row["offerId"] for row in old.get("assortmentUnlocks", [])}
    assort["items"] = [row for row in assort["items"] if row["_id"] not in old_offer_ids]
    for offer_id in old_offer_ids:
        assort["barter_scheme"].pop(offer_id, None)
        assort["loyal_level_items"].pop(offer_id, None)
    assort["items"].extend(unlock_items)
    assort["barter_scheme"].update(unlock_barter)
    assort["loyal_level_items"].update(unlock_loyalty)
    assort_path.write_text(json.dumps(assort, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    questassort_path.write_text(json.dumps(questassort, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    runtime_manifest = {"schemaVersion": 1, "status": "runtime-materialized", "storyQuestCount": 100, "totalQuestCount": 172, "natalyaIntegratedQuestCount": len(NATALYA_QUESTS), "natalyaMode": "specialist-inside-admiral-no-second-trader", "nativeZoneReuse": True, "customZoneDependency": False, "customItemDependency": False, "typicalEliminationMaximum": 12, "assortmentUnlockCount": len(unlock_rows), "totalFiniteOfferCount": 51, "assortmentUnlocks": unlock_rows, "quests": [{"id": q["id"], "chain": chain["chain"], "order": q["order"], "map": chain["map"], "natalya": specialist} for chain, q, _, specialist in runtime_rows]}
    (ROOT / "manifests/story-campaign-runtime.json").write_text(json.dumps(runtime_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"generated": len(runtime_rows), "total": 172, "natalya": len(NATALYA_QUESTS), "storyUnlocks": len(unlock_rows), "finiteOffers": 51}))


if __name__ == "__main__":
    main()
