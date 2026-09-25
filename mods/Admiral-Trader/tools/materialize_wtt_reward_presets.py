#!/usr/bin/env python3
"""Materialise complete, optional WTT weapon trees as an Admiral preset catalog.

Run only against an installed WTT runtime. The output is committed data, not a
runtime dependency: registration admits a preset only when every referenced
template exists and leaves native reward behavior unchanged when WTT is absent.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
WEAPON_PARENTS = {"5447b5f14bdc2d61278b4567", "5447b5cf4bdc2d65278b4567", "5447b6254bdc2dc3278b4568", "5447b6094bdc2dc3278b4568", "5447b5e04bdc2d62278b4567", "5447b6194bdc2d67278b4567", "5447b5fc4bdc2d87278b4567"}
CURATED_REWARDS = {
    "7ec59fd018a4332dc9cc598e": "68c23b960f579b9b5fe021e7",
    "96ef708ef07d2b7bd8214653": "68cf56067ff6ceab0c2fd49e",
    "9a2efe4c869cd41beb667e29": "67b05e25d83f07b7b587c0b5",
    "5c7e60f203900e75aba3edc0": "68b7f4060a4536984f82cf4b",
    "9620e5a3c17b30599df67730": "6761b213607f9a6f79017cd8",
    "de92651782ad58fce6457af9": "68433b58a8f9a618b11082d4",
    "8722d67f966ff1605393222a": "67f425638b8cbfdc0cd1b5f2",
    "fc14500bbc2900a04647083d": "66839591f4d0cba7b041b2af",
    "3def8dacb60f04f15fc2271d": "67c6de3ce39861860909e8e5",
    "a006fa3e96a9c967eda0f716": "687afda52dc9fd6c0e14c602",
    "7564e60e4c1c2f1b67a594a4": "6871284e9a353bb50606f3ed",
    "b016df9d2bea4269cc59d531": "68bf41bbb786f6e9315a5cab",
    "2568ee0bfe2ee12f24d78f45": "66e88596febdcf9daade16a8",
    "43d9544a09d068476a1a18df": "6761b213607f9a6f79017c7e",
    "33810921ad5c893b866b3951": "6932abeb5403890d0c09c926",
    "a0d05e28971f1ba57639b97d": "69236a0b2d1260dbca41ef92",
    "8cba3e2ec639a4aa2c26c4da": "677c9a47baecf3c4b2453365",
    "153839f368b80b6fbc36d29e": "684e32eaec9f5eb3cacc7ca7",
    "cd2641c70bede98dac3945d0": "68a3836826dffa87b5767c04",
    "f6e51dc4e50e47ee9af50a4d": "6920b28eabc4f9d229cb7e49",
    "4ada822d634041a721b346d5": "68aee763130c00663d08aea8",
    "570d250679328757614dcbcb": "695ab544a219218449041700",
    "ffb63228a333c8b0755741ea": "6962f22fddc6698c6309b620",
    "ad9233f54a7132d905d6f29d": "68fd4feab87d77a5aaf6bf64",
}


def hid(value: str) -> str:
    return hashlib.sha256(value.encode()).hexdigest()[:24]


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def roots_from_assorts(mod: Path, source: str):
    custom = {}
    for path in (mod / "db/CustomItems").rglob("*.json"):
        for tpl, row in load(path).items():
            if isinstance(row, dict) and row.get("parentId") in WEAPON_PARENTS:
                custom[tpl] = row
    locales = {}
    for language in ("en", "ru"):
        path = mod / f"db/CustomLocales/{language}.json"
        locales[language] = load(path) if path.exists() else {}
    output = []
    for path in (mod / "db/CustomAssortSchemes").rglob("*.json"):
        for scheme in load(path).values():
            items = scheme.get("items", []) if isinstance(scheme, dict) else []
            by_parent = {}
            for item in items:
                by_parent.setdefault(item.get("parentId"), []).append(item)
            for root in by_parent.get("hideout", []):
                if root.get("_tpl") not in custom:
                    continue
                tree, pending = [], [root]
                while pending:
                    item = pending.pop(0)
                    tree.append(item)
                    pending.extend(by_parent.get(item.get("_id"), []))
                definition = custom[root["_tpl"]]
                output.append((source, root["_tpl"], tree, {
                    "nameEn": locales["en"].get(f"{root['_tpl']} Name", definition.get("locales", {}).get("en", {}).get("name", root["_tpl"])),
                    "nameRu": locales["ru"].get(f"{root['_tpl']} Name", locales["en"].get(f"{root['_tpl']} Name", root["_tpl"])),
                    "valueRub": int(definition.get("fleaPriceRoubles") or definition.get("handbookPriceRoubles") or 1),
                }))
    return output


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime", required=True, type=Path)
    args = parser.parse_args()
    runtime = args.runtime / "user/mods"
    entries = roots_from_assorts(runtime / "WTT-Armory", "wtt-armory")
    entries += roots_from_assorts(runtime / "WTT-ContentBackport", "wtt-content-backport")
    unique = {}
    metadata = {}
    for source, template, tree, details in entries:
        unique.setdefault((source, template), tree)
        metadata[(source, template)] = details
    rows = []
    for index, ((source, template), tree) in enumerate(sorted(unique.items())):
        preset_id = hid(f"{source}:{template}:{index}")
        ids = {item["_id"]: hid(f"{preset_id}:{item['_id']}") for item in tree}
        items = []
        for item in tree:
            copy = {key: value for key, value in item.items() if key not in {"_id", "parentId"}}
            copy["_id"] = ids[item["_id"]]
            if item.get("parentId") != "hideout":
                copy["parentId"] = ids[item["parentId"]]
            copy.setdefault("slotId", "hideout")
            items.append(copy)
        rows.append({"presetId": preset_id, "source": source, "rootTemplate": template, **metadata[(source, template)], "items": items})
    target = ROOT / "db/optional/wtt-preset-catalog.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(rows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    by_template = {row["rootTemplate"]: row for row in rows}
    quests = {load(path)["_id"]: load(path) for path in (ROOT / "db/quests").glob("*.json")}
    trades = {}
    for quest_id, template in CURATED_REWARDS.items():
        preset = by_template[template]
        quest = quests[quest_id]
        cash = next(r["value"] for r in quest["rewards"]["Success"] if r.get("items", [{}])[0].get("_tpl") == "5449016a4bdc2d6f028b456f")
        # Preserve the authored minimum cash award. Some late mastery quests already
        # pay exactly 10,000 roubles, so their curated weapon is an additive reward.
        reduction = max(0, min(int(preset["valueRub"] * 0.15), int(cash) - 10000, 35000))
        trades[quest_id] = {
            "cashReductionRub": reduction,
            "minimumCashRub": 10000,
            "name": preset["nameRu"],
            "reward": {"value": 1, "id": hid(f"{quest_id}:wtt-reward"), "type": "Item", "target": preset["items"][0]["_id"], "items": preset["items"]},
        }
    (ROOT / "db/optional/wtt-reward-trades.json").write_text(json.dumps(trades, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {len(rows)} optional WTT complete weapon presets and {len(trades)} curated rewards")


if __name__ == "__main__":
    main()
