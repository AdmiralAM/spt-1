#!/usr/bin/env python3
"""Apply the complete two-lane weapon rotation without changing quest IDs."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUB = "5449016a4bdc2d6f028b456f"

LOCATION_IDS = {
    "Ground Zero": ["Sandbox", "Sandbox_high"], "Customs": ["bigmap"],
    "Factory": ["factory4_day", "factory4_night"], "Woods": ["Woods"],
    "Shoreline": ["Shoreline"], "Interchange": ["Interchange"],
    "Reserve": ["RezervBase"], "Lighthouse": ["Lighthouse"],
    "Streets": ["TarkovStreets"], "The Lab": ["laboratory"],
}

NAMES = {
    "service-pistols": ("Service Pistols", "Служебные пистолеты"),
    "compact-ak": ("Compact Kalashnikov Rifles", "Укороченные автоматы Калашникова"),
    "manual-shotguns": ("Manual Shotguns", "Помповые и магазинные дробовики"),
    "early-smg": ("Early Submachine Guns", "Ранние пистолеты-пулемёты"),
    "forty-five-pistols": (".45 ACP Pistols", "Пистолеты .45 ACP"),
    "service-smg": ("Service Submachine Guns", "Служебные пистолеты-пулемёты"),
    "automatic-pistols": ("Automatic Pistols", "Автоматические пистолеты"),
    "compact-pdw": ("Compact PDWs", "Компактное оружие самообороны"),
    "autoloading-shotguns": ("Self-loading Shotguns", "Самозарядные дробовики"),
    "modern-smg": ("Modern Submachine Guns", "Современные пистолеты-пулемёты"),
    "high-velocity-pistols": ("High-velocity Pistols", "Высокоскоростные пистолеты"),
    "special-pdw": ("Special-purpose PDWs", "Специальное оружие самообороны"),
    "unusual-shotguns": ("Specialized Shotguns", "Специализированные дробовики"),
    "magnum-sidearms": ("Magnum Sidearms", "Крупнокалиберное короткоствольное оружие"),
    "suppressed-sidearms": ("Suppressed Sidearms", "Бесшумное короткоствольное оружие"),
    "light-machine-guns": ("Light Machine Guns", "Ручные пулемёты"),
    "general-machine-guns": ("General-purpose Machine Guns", "Единые пулемёты"),
    "grenade-launchers": ("Grenade Launchers", "Гранатомёты"),
    "close-mastery": ("Close-combat Mastery", "Мастерство ближнего боя"),
    "support-mastery": ("Support-weapon Mastery", "Мастерство оружия поддержки"),
    "starter-service-rifles": ("Starter Service Rifles", "Начальные служебные автоматы"),
    "civilian-ak": ("Civilian Kalashnikov Carbines", "Гражданские карабины Калашникова"),
    "compact-rifles": ("Compact Assault Rifles", "Компактные штурмовые винтовки"),
    "sks-hunter": ("Self-loading Carbines", "Самозарядные карабины"),
    "classic-ak-545": ("Classic 5.45 Kalashnikov Rifles", "Классические автоматы Калашникова 5,45"),
    "classic-ak-762": ("Classic 7.62 Kalashnikov Rifles", "Классические автоматы Калашникова 7,62"),
    "nato-service-rifles": ("NATO Service Rifles", "Служебные винтовки НАТО"),
    "nato-alternatives": ("Alternative NATO Rifles", "Альтернативные винтовки НАТО"),
    "battle-rifles": ("Battle Rifles", "Боевые винтовки"),
    "nine-by-thirty-nine": ("9x39 Special-purpose Weapons", "Специальные системы 9×39"),
    "entry-dmr": ("Entry Marksman Rifles", "Начальные марксманские винтовки"),
    "service-dmr": ("Service Marksman Rifles", "Служебные марксманские винтовки"),
    "entry-bolt-action": ("Entry Bolt-action Rifles", "Начальные винтовки с продольно-скользящим затвором"),
    "nato-bolt-action": ("NATO Bolt-action Rifles", "Винтовки НАТО с продольно-скользящим затвором"),
    "magnum-precision": ("Magnum Precision Rifles", "Крупнокалиберные высокоточные винтовки"),
    "advanced-intermediate": ("Advanced Intermediate-caliber Rifles", "Современные автоматы промежуточного калибра"),
    "heavy-special-rifles": ("Heavy Special-purpose Rifles", "Тяжёлые специальные винтовки"),
    "modern-ak": ("Modern Kalashnikov Rifles", "Современные автоматы Калашникова"),
    "western-bullpup-modular": ("Modular and Bullpup Rifles", "Модульные винтовки и булл-папы"),
    "precision-mastery": ("Precision Mastery", "Мастерство точной стрельбы"),
}

# The first 19 records already have stable generated IDs. The remaining 21
# reuse the original Arsenal IDs, repartitioned into balanced 20-step lanes.
STABLE_IDS = {
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

SUPPORT_REWARDS = {
    1: ("GSSH-01 active headset", "5b432b965acfc47a8774094e", 1),
    2: ("IFAK field medical kit", "590c678286f77426c9660122", 1),
    3: ("Salewa first aid kit", "544fb45d4bdc2dee738b4568", 1),
    5: ("Aimpoint Micro H-2 sight", "61657230d92c473c770213d7", 1),
    6: ("EOTech 553 holographic sight", "570fd6c2d2720bc6458b457f", 1),
    7: ("PSO-1 optical sight", "5c82342f2e221644f31c060e", 1),
    9: ("SIG Sauer SRD9 suppressor", "5c6165902e22160010261b28", 1),
    10: ("Aimpoint CompM4 sight", "5c7d55de2e221644f31bff68", 1),
    11: ("SIG BRAVO4 optical sight", "57adff4f24597737f373b6e6", 1),
    13: ("MSA Sordin active headset", "5aa2ba71e5b5b000137b758f", 1),
    14: ("EOTech HHS-1 hybrid sight", "5c07dd120db834001c39092d", 1),
    15: ("Grizzly medical kit (3x3)", "590c657e86f77412b013051d", 1),
    17: ("Grizzly medical kit", "590c657e86f77412b013051d", 1),
    18: ("Surv12 field surgical kit", "5d02797c86f774203f38e30a", 1),
    19: ("Camelbak Tri-Zip backpack", "545cdae64bdc2d39198b4568", 1),
}

CASH_REDUCTION = {
    1: 1800, 2: 3500, 3: 5000, 4: 10000, 5: 7000,
    6: 9000, 7: 11000, 8: 16000, 9: 13000, 10: 15000,
    11: 10000, 12: 18000, 13: 16000, 14: 18000, 15: 20000,
    16: 24000, 17: 24000, 18: 28000, 19: 30000, 20: 32000,
}


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def save(path: Path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def hid(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:24]


def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


def clone_reward(source, quest_id: str, order: int):
    cloned = json.loads(json.dumps(source))
    id_map = {item["_id"]: hid(f"{quest_id}:rotation-reward:{order}:{index}") for index, item in enumerate(cloned["items"])}
    for item in cloned["items"]:
        old = item["_id"]
        item["_id"] = id_map[old]
        if item.get("parentId") in id_map:
            item["parentId"] = id_map[item["parentId"]]
    cloned["id"] = hid(f"{quest_id}:rotation-reward-row:{order}")
    cloned["target"] = id_map[cloned["target"]]
    cloned["index"] = 20
    return cloned


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--spt-root", type=Path)
    args = parser.parse_args()
    plan = load(ROOT / "manifests/weapon-rotation-expansion-plan.json")
    optional = load(ROOT / "manifests/optional-weapon-runtime.json")
    optional_by_pool = {}
    for row in optional["acceptedWeapons"]:
        optional_by_pool.setdefault(row["pool"], []).append(row)
    paths = {load(path)["_id"]: path for path in (ROOT / "db/quests").glob("*.json")}
    m8 = {lang: load(ROOT / f"db/locales/m8-{lang}.json") for lang in ("en", "ru")}
    arsenal = {lang: load(ROOT / f"db/locales/arsenal-{lang}.json") for lang in ("en", "ru")}
    prior_reward_path = ROOT / "manifests/weapon-rotation-rewards.json"
    prior_rewards = load(prior_reward_path) if prior_reward_path.exists() else {"rewards": []}
    prior_by_quest = {row["questId"]: row for row in prior_rewards.get("rewards", [])}
    signature = list(load(ROOT / "db/rewards/natalya-signature-replacements.json").values())
    belt_trades = load(ROOT / "db/rewards/belt-container-reward-trades.json")
    preset_slots = [(lane, order) for order in (4, 8, 12, 16, 20) for lane in ("A", "B")]
    preset_by_slot = dict(zip(preset_slots, signature))
    item_names = {"en": {}, "ru": {}}
    if args.spt_root:
        for lang in item_names:
            source = args.spt_root / "SPT_Runtime/SPT_Data/database/locales/global" / f"{lang}.json"
            raw = json.loads(source.read_text(encoding="utf-8-sig"))
            item_names[lang] = {key[:-5]: value for key, value in raw.items() if key.endswith(" Name")}

    assignments = []
    reward_rows = []
    for lane, lane_key in (("A", "A-close-support"), ("B", "B-rifle-precision")):
        rows = plan["lanes"][lane_key]
        ids = STABLE_IDS[lane]
        if len(rows) != len(ids):
            raise ValueError(f"lane {lane}: {len(rows)} rows but {len(ids)} stable IDs")
        previous = None
        for row, quest_id in zip(rows, ids):
            order, pool, level_band, locations, semantics = row
            path = paths[quest_id]
            quest = load(path)
            prior = prior_by_quest.get(quest_id)
            if prior:
                quest["rewards"]["Success"] = [reward for reward in quest["rewards"]["Success"] if reward["id"] != prior["rewardId"]]
                cash = next(reward for reward in quest["rewards"]["Success"] if reward.get("items", [{}])[0].get("_tpl") == RUB)
                cash["value"] += prior["cashReductionRub"]
                cash["items"][0]["upd"]["StackObjectsCount"] = cash["value"]
            native = plan["pools"][pool]
            additions = optional_by_pool.get(pool, [])
            weapons = native + [entry["tpl"] for entry in additions]
            kill_rows = [node for node in walk(quest["conditions"]["AvailableForFinish"]) if node.get("conditionType") == "Kills"]
            if not kill_rows:
                raise ValueError(f"{quest_id}: no kill condition")
            for kill_row in kill_rows:
                kill_row["weapon"] = weapons
                location_rows = [node for node in walk(quest["conditions"]["AvailableForFinish"]) if node.get("conditionType") == "Location"]
                targets = [runtime for label in locations for runtime in LOCATION_IDS[label]]
                if location_rows:
                    for location_row in location_rows:
                        location_row["target"] = targets
                else:
                    parent = next(node for node in walk(quest["conditions"]["AvailableForFinish"]) if node.get("counter", {}).get("conditions") is not None)
                    parent["counter"]["conditions"].append({"id": hid(quest_id + ":rotation-location"), "dynamicLocale": False, "conditionType": "Location", "target": targets})
            start = quest["conditions"]["AvailableForStart"]
            level = int(level_band.split("-")[0]) if "-" in level_band else 40
            level_row = next(node for node in start if node.get("conditionType") == "Level")
            level_row["value"] = level
            start[:] = [node for node in start if node.get("conditionType") != "Quest"]
            if previous:
                start.append({"id": hid(quest_id + ":rotation-prerequisite"), "index": 1, "dynamicLocale": False, "globalQuestCounterId": "", "visibilityConditions": [], "parentId": "", "target": previous, "status": [4], "availableAfter": 0, "dispersion": 0, "conditionType": "Quest"})
            previous = quest_id
            en_name, ru_name = NAMES[pool]
            quest["QuestName"] = f"Ротация «Арсенал» {lane}-{order}: {ru_name}"
            cash = next(reward for reward in quest["rewards"]["Success"] if reward.get("items", [{}])[0].get("_tpl") == RUB)
            requested_reduction = CASH_REDUCTION[order]
            optional_reduction = int(belt_trades.get(quest_id, {}).get("cashReductionRub", 0))
            reduction = min(requested_reduction, max(0, int(cash["value"]) - optional_reduction - 10000))
            cash["value"] -= reduction
            cash["items"][0]["upd"]["StackObjectsCount"] = cash["value"]
            if (lane, order) in preset_by_slot:
                item_reward = clone_reward(preset_by_slot[(lane, order)], quest_id, order)
                reward_name = "complete configured weapon"
                reward_kind = "complete-weapon"
            else:
                reward_name, tpl, quantity = SUPPORT_REWARDS[order]
                item_id = hid(f"{quest_id}:rotation-reward-item:{order}")
                item_reward = {"value": quantity, "id": hid(f"{quest_id}:rotation-reward-row:{order}"), "type": "Item", "target": item_id, "index": 20, "items": [{"_id": item_id, "_tpl": tpl, "upd": {"StackObjectsCount": quantity}}]}
                reward_kind = "field-support"
            quest["rewards"]["Success"].append(item_reward)
            reward_rows.append({"questId": quest_id, "lane": lane, "order": order, "kind": reward_kind, "name": reward_name, "cashReductionRub": reduction, "rewardId": item_reward["id"], "rootTemplate": item_reward["items"][0]["_tpl"], "itemTreeSize": len(item_reward["items"])})
            save(path, quest)
            locale_set = m8 if path.name.startswith("40-") else arsenal
            optional_en = ", ".join(entry["name"] for entry in additions)
            optional_ru = ", ".join(entry["nameRu"] for entry in additions)
            native_en = ", ".join(item_names["en"].get(tpl, tpl) for tpl in native)
            native_ru = ", ".join(item_names["ru"].get(tpl, tpl) for tpl in native)
            detail_en = f"Operational detail:\nEligible weapons:\n- {native_en}."
            detail_ru = f"Уточнение:\nРазрешённое оружие:\n- {native_ru}."
            if optional_en:
                detail_en += f"\nOptional WTT models:\n- {optional_en}."
                detail_ru += f"\nДополнительные модели WTT:\n- {optional_ru}."
            for lang, name, detail in (("en", f"Arsenal Rotation {lane}-{order}: {en_name}", detail_en), ("ru", quest["QuestName"], detail_ru)):
                loc = locale_set[lang]
                loc[f"{quest_id} name"] = name
                loc[f"{quest_id} description"] = detail
                loc[f"{quest_id} startedMessageText"] = detail
                loc[f"{quest_id} acceptPlayerMessage"] = detail
            assignments.append({"id": quest_id, "lane": lane, "order": order, "pool": pool, "levelBand": level_band, "locations": locations, "semantics": semantics, "nativeWeaponCount": len(native), "optionalWeaponCount": len(additions)})

    for lang in ("en", "ru"):
        (ROOT / f"db/locales/m8-{lang}.json").write_text(json.dumps(m8[lang], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        (ROOT / f"db/locales/arsenal-{lang}.json").write_text(json.dumps(arsenal[lang], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    output = {"schemaVersion": 1, "status": "runtime-materialized", "weaponQuestCount": 40, "laneCounts": {"A": 20, "B": 20}, "maximumConcurrentWeaponAssignments": 2, "stableQuestIdsRetained": True, "assignments": assignments}
    (ROOT / "manifests/weapon-rotation-runtime.json").write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    reward_output = {"schemaVersion": 1, "status": "runtime-materialized", "policy": "Every weapon assignment trades part of its rouble reward for one useful staged item; every fourth step awards a complete configured weapon.", "completeWeaponRewards": 10, "fieldSupportRewards": 30, "minimumRemainingRoubles": 10000, "rewards": reward_rows}
    prior_reward_path.write_text(json.dumps(reward_output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
