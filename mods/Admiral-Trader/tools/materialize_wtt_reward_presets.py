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


def hid(value: str) -> str:
    return hashlib.sha256(value.encode()).hexdigest()[:24]


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def roots_from_assorts(mod: Path, source: str):
    custom = {}
    for path in (mod / "db/CustomItems").rglob("*.json"):
        for tpl, row in load(path).items():
            if isinstance(row, dict) and row.get("parentId") in WEAPON_PARENTS:
                custom[tpl] = row.get("parentId")
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
                output.append((source, root["_tpl"], tree))
    return output


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--runtime", required=True, type=Path)
    args = parser.parse_args()
    runtime = args.runtime / "user/mods"
    entries = roots_from_assorts(runtime / "WTT-Armory", "wtt-armory")
    entries += roots_from_assorts(runtime / "WTT-ContentBackport", "wtt-content-backport")
    unique = {}
    for source, template, tree in entries:
        unique.setdefault((source, template), tree)
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
        rows.append({"presetId": preset_id, "source": source, "rootTemplate": template, "items": items})
    target = ROOT / "db/optional/wtt-preset-catalog.json"
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(rows, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"wrote {len(rows)} optional WTT complete weapon presets")


if __name__ == "__main__":
    main()
