#!/usr/bin/env python3
"""Give every remaining cash-only quest a useful native reward."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUB = "5449016a4bdc2d6f028b456f"

REWARD_LADDER = [
    ("Активные наушники ГСШ-01", "5b432b965acfc47a8774094e", 1),
    ("Аптечка IFAK", "590c678286f77426c9660122", 1),
    ("Аптечка Salewa", "544fb45d4bdc2dee738b4568", 1),
    ("Рюкзак LBT-8005A Day Pack", "5e9dcf5986f7746c417435b3", 1),
    ("Коллиматор Aimpoint Micro H-2", "61657230d92c473c770213d7", 1),
    ("Голографический прицел EOTech 553", "570fd6c2d2720bc6458b457f", 1),
    ("Бронежилет PACA", "5648a7494bdc2d9d488b4583", 1),
    ("Аптечка AFAK", "60098ad7c2240c0fe85c570a", 1),
    ("Хирургический набор CMS", "5d02778e86f774203e7dedbe", 1),
    ("Активные наушники MSA Sordin", "5aa2ba71e5b5b000137b758f", 1),
    ("Разгрузочный жилет BlackRock", "5648a69d4bdc2ded0b8b457b", 1),
    ("Оптический прицел SIG BRAVO4", "57adff4f24597737f373b6e6", 1),
    ("Гибридный прицел EOTech HHS-1", "5c07dd120db834001c39092d", 1),
    ("Аптечка Grizzly", "590c657e86f77412b013051d", 1),
    ("Хирургический набор Surv12", "5d02797c86f774203f38e30a", 1),
    ("Рюкзак Camelbak Tri-Zip", "545cdae64bdc2d39198b4568", 1),
    ("Стимулятор Пропитал", "5c0e530286f7747fa1419862", 1),
    ("Стимулятор Загустин", "5c0e533786f7747fa23f4d47", 1),
    ("Стимулятор SJ6", "5c0e531d86f7747fa23f4d42", 1),
    ("Стимулятор eTG-c", "5c0e534186f7747fa1419867", 1),
]

OVERLAY_FILES = [
    "natalya-signature-replacements.json", "early-weapon-reward-trades.json",
    "field-support-reward-trades.json", "tactical-reward-trades.json",
    "belt-container-reward-trades.json",
]


def hid(value: str) -> str:
    return hashlib.sha256(value.encode("utf-8")).hexdigest()[:24]


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def save(path: Path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def level(quest) -> int:
    return int(next(row["value"] for row in quest["conditions"]["AvailableForStart"] if row["conditionType"] == "Level"))


def main():
    quest_paths = {load(path)["_id"]: path for path in (ROOT / "db/quests").glob("*.json")}
    quests = {qid: load(path) for qid, path in quest_paths.items()}
    overlays = set()
    for filename in OVERLAY_FILES:
        overlays.update(load(ROOT / "db/rewards" / filename))
    rotation = load(ROOT / "manifests/weapon-rotation-rewards.json")
    overlays.update(row["questId"] for row in rotation["rewards"])
    output_path = ROOT / "manifests/campaign-polish-rewards.json"
    previous = load(output_path) if output_path.exists() else {"rewards": []}
    for row in previous.get("rewards", []):
        quest = quests[row["questId"]]
        quest["rewards"]["Success"] = [reward for reward in quest["rewards"]["Success"] if reward["id"] != row["rewardId"]]
        cash = next(reward for reward in quest["rewards"]["Success"] if reward.get("items", [{}])[0].get("_tpl") == RUB)
        cash["value"] += row["cashReductionRub"]
        cash["items"][0]["upd"]["StackObjectsCount"] = cash["value"]

    candidates = []
    for qid, quest in quests.items():
        direct_item = any(
            reward.get("type") == "Item" and reward.get("items", [{}])[0].get("_tpl") != RUB
            for reward in quest["rewards"]["Success"]
        )
        if not direct_item and qid not in overlays:
            candidates.append((level(quest), qid, quest))
    candidates.sort(key=lambda row: (row[0], row[1]))
    rows = []
    for index, (quest_level, qid, quest) in enumerate(candidates):
        tier = min(19, max(0, (quest_level - 1) // 2))
        name, tpl, quantity = REWARD_LADDER[(tier + index) % len(REWARD_LADDER)]
        cash = next(reward for reward in quest["rewards"]["Success"] if reward.get("items", [{}])[0].get("_tpl") == RUB)
        desired = 3000 + min(27000, quest_level * 700)
        reduction = min(desired, max(0, int(cash["value"]) - 10000))
        cash["value"] -= reduction
        cash["items"][0]["upd"]["StackObjectsCount"] = cash["value"]
        item_id = hid(f"{qid}:campaign-polish-item")
        reward_id = hid(f"{qid}:campaign-polish-reward")
        reward = {"value": quantity, "id": reward_id, "type": "Item", "target": item_id, "index": 21, "items": [{"_id": item_id, "_tpl": tpl, "upd": {"StackObjectsCount": quantity}}]}
        quest["rewards"]["Success"].append(reward)
        rows.append({"questId": qid, "level": quest_level, "name": name, "tpl": tpl, "quantity": quantity, "cashReductionRub": reduction, "rewardId": reward_id})

    for qid, quest in quests.items():
        save(quest_paths[qid], quest)
    output = {"schemaVersion": 1, "status": "runtime-materialized", "policy": "Every quest has XP, standing, at least 10000 remaining roubles and one useful item or unlock after native and conditional reward layers are considered.", "rewardedQuestCount": len(rows), "minimumRemainingRoubles": 10000, "rewards": rows}
    output_path.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
