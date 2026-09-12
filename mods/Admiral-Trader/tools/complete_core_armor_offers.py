#!/usr/bin/env python3
"""Attach native default-preset children to Admiral armor offers that require them."""
import argparse, hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
REQUIRED = {
    "5d5e9c74a4b9364855191c40",  # MSA ACH TC-2002
    "5b44cad286f77402a54ae7e5",  # 5.11 TacTec
    "5d5d87f786f77427997cfaef",  # Ars Arma A18 Skanda
}

def oid(offer, index): return hashlib.sha256(f"admiral-core-armor:{offer}:{index}".encode()).hexdigest()[:24]

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("globals", type=Path)
    args = parser.parse_args()
    path = ROOT / "db/assort.json"; assort = json.loads(path.read_text(encoding="utf-8"))
    presets = json.loads(args.globals.read_text(encoding="utf-8-sig"))["ItemPresets"]
    roots = {row["_tpl"]: row for row in assort["items"] if row.get("parentId") == "hideout" and row["_tpl"] in REQUIRED}
    if set(roots) != REQUIRED: raise ValueError(f"core armor root drift: {set(roots)}")
    owned = {root["_id"] for root in roots.values()}
    descendants = set()
    changed = True
    while changed:
        changed = False
        for row in assort["items"]:
            if row.get("parentId") in owned | descendants and row["_id"] not in descendants:
                descendants.add(row["_id"]); changed = True
    assort["items"] = [row for row in assort["items"] if row["_id"] not in descendants]
    for tpl, offer_root in roots.items():
        matches = []
        for preset in presets.values():
            items = preset.get("_items", [])
            preset_roots = [row for row in items if row.get("parentId") is None and row.get("_tpl") == tpl]
            if preset_roots: matches.append((preset_roots[0], items))
        if len(matches) != 1: raise ValueError(f"{tpl}: expected one native default preset, got {len(matches)}")
        source_root, source_items = matches[0]
        descendants = [row for row in source_items if row["_id"] != source_root["_id"]]
        remap = {source_root["_id"]: offer_root["_id"]}
        remap.update({row["_id"]: oid(offer_root["_id"], index) for index, row in enumerate(descendants, 1)})
        for source in descendants:
            row = dict(source); row["_id"] = remap[source["_id"]]
            if source.get("parentId") in remap: row["parentId"] = remap[source["parentId"]]
            assort["items"].append(row)
    path.write_text(json.dumps(assort, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

if __name__ == "__main__": main()
