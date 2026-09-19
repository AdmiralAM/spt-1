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

# Product-reviewed rewards for the equipment and legacy operation clusters.
# These are deliberately useful at the quest's actual progression stage and
# remain native, so the reward never disappears when optional content is off.
CURATED_REWARDS = {
    "4a8f533e1ed458e83b41c01f": ("Аптечка IFAK", "590c678286f77426c9660122", 1),
    "4ab0b49478adb233ae900b33": ("Активные наушники MSA Sordin", "5aa2ba71e5b5b000137b758f", 1),
    "ca33fab8b9cc5f5f5ad322c0": ("Бронежилет HighCom Trooper", "5c0e655586f774045612eeb2", 1),
    "9c35b3ac22ede1a5a79118bc": ("Бронежилет БНТИ Жук", "5c0e625a86f7742d77340f62", 1),
    "ee813142de655daf2dedfebc": ("Кейс для патронов", "5aafbde786f774389d0cbc0f", 1),
    "47480d824cea0b80917cafa5": ("Бронежилет 6Б13 М Killa", "5c0e541586f7747fa54205c9", 1),
    "8dad0d354ac000b7bbf05b9a": ("Активные наушники ComTac IV", "628e4e576d783146b124c64d", 1),
    "56813681ae0690016376f163": ("Металлическая топливная канистра", "5d1b36a186f7742523398433", 1),
    "208db81b5ce195bf0c176852": ("Бронежилет HighCom Trooper", "5c0e655586f774045612eeb2", 1),
    "6574a072f763d0b09a553401": ("Бронежилет БНТИ Жук", "5c0e625a86f7742d77340f62", 1),
    "8b6f2b25ab2e91e0540761e3": ("Планшет для документов", "590c60fc86f77412b13fddcf", 1),
    "41a41cb262ea084c1e110513": ("Активные наушники MSA Sordin", "5aa2ba71e5b5b000137b758f", 1),
    "133aa723b4695a3d93de92f1": ("Кейс для патронов", "5aafbde786f774389d0cbc0f", 1),
    "db220288bc8d5559a45feeb1": ("Кейс для жетонов", "5c093e3486f77430cb02e593", 1),
    "4c2cc3f85d60170907642d9e": ("Оптический прицел SIG BRAVO4", "57adff4f24597737f373b6e6", 1),
    "b1b3d9e3a930a3eae47b2353": ("Бронежилет 6Б13 М Killa", "5c0e541586f7747fa54205c9", 1),
    "f62d8e1285027e336767513c": ("Полевой хирургический набор Surv12", "5d02797c86f774203f38e30a", 1),
    "4072a5e458946a243b886ad8": ("Кейс S I C C", "5d235bb686f77443f4331278", 1),
}

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
    output_path = ROOT / "manifests/campaign-polish-rewards.json"
    previous = load(output_path) if output_path.exists() else {"rewards": []}
    previous_by_quest = {row["questId"]: row for row in previous.get("rewards", [])}
    optional_reductions = {
        quest_id: int(row["cashReductionRub"])
        for quest_id, row in load(ROOT / "db/rewards/belt-container-reward-trades.json").items()
    }
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
        curated = CURATED_REWARDS.get(qid)
        has_curated = curated and any(
            reward.get("type") == "Item" and reward.get("items", [{}])[0].get("_tpl") == curated[1]
            for reward in quest["rewards"]["Success"]
        )
        if (curated and not has_curated) or not direct_item:
            candidates.append((level(quest), qid, quest))
    candidates.sort(key=lambda row: (row[0], row[1]))
    rows = []
    for index, (quest_level, qid, quest) in enumerate(candidates):
        tier = min(19, max(0, (quest_level - 1) // 2))
        name, tpl, quantity = CURATED_REWARDS.get(qid, REWARD_LADDER[(tier + index) % len(REWARD_LADDER)])
        cash = next(reward for reward in quest["rewards"]["Success"] if reward.get("items", [{}])[0].get("_tpl") == RUB)
        maximum_reduction = max(0, int(cash["value"]) - optional_reductions.get(qid, 0) - 10000)
        if qid in CURATED_REWARDS:
            reduction = 0
        elif qid in previous_by_quest:
            reduction = min(int(previous_by_quest[qid]["cashReductionRub"]), maximum_reduction)
        else:
            desired = 3000 + min(27000, quest_level * 700)
            reduction = min(desired, maximum_reduction)
        cash["value"] -= reduction
        cash["items"][0]["upd"]["StackObjectsCount"] = cash["value"]
        item_id = hid(f"{qid}:campaign-polish-item")
        reward_id = hid(f"{qid}:campaign-polish-reward")
        reward = {"value": quantity, "id": reward_id, "type": "Item", "target": item_id, "index": 21, "items": [{"_id": item_id, "_tpl": tpl, "upd": {"StackObjectsCount": quantity}}]}
        quest["rewards"]["Success"].append(reward)
        rows.append({"questId": qid, "level": quest_level, "name": name, "tpl": tpl, "quantity": quantity, "cashReductionRub": reduction, "rewardId": reward_id})

    for qid, quest in quests.items():
        save(quest_paths[qid], quest)
    output = {"schemaVersion": 1, "status": "runtime-materialized", "policy": "Every quest has a native useful item reward even when optional content is absent; curated equipment and operation rewards supplement rather than replace cash.", "rewardedQuestCount": len(rows), "minimumRemainingRoubles": 10000, "rewards": rows}
    output_path.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
