#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("quest_dir", type=Path)
    parser.add_argument("generated_runtime", type=Path)
    args = parser.parse_args()

    generated = load_json(args.generated_runtime)
    templates = generated.get("templates") or {}
    if len(templates) != 21:
        raise SystemExit(f"expected 21 generated Arsenal templates, got {len(templates)}")

    existing_by_id: dict[str, Path] = {}
    for path in sorted(args.quest_dir.glob("*.json")):
        data = load_json(path)
        qid = str(data.get("_id") or "")
        if qid in templates:
            if qid in existing_by_id:
                raise SystemExit(f"duplicate committed Arsenal quest id {qid}")
            existing_by_id[qid] = path

    missing = sorted(set(templates) - set(existing_by_id))
    if missing:
        raise SystemExit(f"missing committed Arsenal quest files: {missing}")

    changed = 0
    for qid, template in templates.items():
        path = existing_by_id[qid]
        current = load_json(path)
        if current == template:
            continue
        path.write_text(json.dumps(template, separators=(",", ":"), ensure_ascii=False) + "\n", encoding="utf-8")
        changed += 1
        print(f"synced {path.name}")

    print(json.dumps({"arsenalQuestCount": len(templates), "changed": changed}, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
