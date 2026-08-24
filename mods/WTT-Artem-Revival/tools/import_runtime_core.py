#!/usr/bin/env python3
"""Import the authoritative Artem runtime core ZIP into the revival workspace.

The importer deliberately excludes the legacy WTT-Artem.dll. It preserves the
runtime data/layout, applies only proven revival repairs, and writes resources
under server/Resources so the SPT 4.1 loader can package them.
"""

from __future__ import annotations

import argparse
import json
import shutil
import zipfile
from pathlib import Path

LEGACY_DLL = "WTT-Artem.dll"
SWEDEN_OFFER = "675267324707588d57c75972"
SWEDEN_TPL = "6752641b1470fc33b675d59a"
ROUBLES_TPL = "5449016a4bdc2d6f028b456f"
BAD_IMAGE = "/files/quest/icon/ARTT_3thumbnail.jpg"
GOOD_IMAGE = "/files/quest/icon/ARTT_3thumbnail.png"


def load(path: Path):
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def save(path: Path, value):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, indent=4, ensure_ascii=False)
        handle.write("\n")


def patch_quest_image(resources: Path) -> int:
    quest_path = resources / "db/CustomQuests/66bf757f27d0b097db0acea5/Quests/ArtemQuests.json"
    quests = load(quest_path)
    changed = 0
    for quest in quests.values():
        if quest.get("image") == BAD_IMAGE:
            quest["image"] = GOOD_IMAGE
            changed += 1
    save(quest_path, quests)
    return changed


def restore_sweden_offer(resources: Path) -> bool:
    assort_path = resources / "db/assort.json"
    assort = load(assort_path)
    if any(item.get("_id") == SWEDEN_OFFER for item in assort["items"]):
        return False

    assort["items"].append(
        {
            "_id": SWEDEN_OFFER,
            "_tpl": SWEDEN_TPL,
            "parentId": "hideout",
            "slotId": "hideout",
            "upd": {
                "UnlimitedCount": False,
                "StackObjectsCount": 500,
                "BuyRestrictionMax": 3,
                "BuyRestrictionCurrent": 0,
            },
        }
    )
    assort["barter_scheme"][SWEDEN_OFFER] = [[{"count": 200, "_tpl": ROUBLES_TPL}]]
    assort["loyal_level_items"][SWEDEN_OFFER] = 1
    save(assort_path, assort)
    return True


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path, help="Path to authoritative 'artem main 1.zip'")
    parser.add_argument(
        "--output",
        type=Path,
        default=Path(__file__).resolve().parents[1] / "server/Resources",
        help="Destination Resources directory",
    )
    args = parser.parse_args()

    archive = args.archive.resolve()
    output = args.output.resolve()
    if not archive.is_file():
        raise SystemExit(f"archive does not exist: {archive}")

    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True)

    with zipfile.ZipFile(archive) as zf:
        names = set(zf.namelist())
        required = {"db/base.json", "db/assort.json", "bundles.json", LEGACY_DLL}
        missing = sorted(required - names)
        if missing:
            raise SystemExit("archive is not the expected Artem core: missing " + ", ".join(missing))

        for info in zf.infolist():
            if info.is_dir() or Path(info.filename).name == LEGACY_DLL:
                continue
            destination = (output / info.filename).resolve()
            if output not in destination.parents:
                raise SystemExit(f"unsafe archive path: {info.filename}")
            destination.parent.mkdir(parents=True, exist_ok=True)
            with zf.open(info) as src, destination.open("wb") as dst:
                shutil.copyfileobj(src, dst)

    image_fixes = patch_quest_image(output)
    sweden_added = restore_sweden_offer(output)

    print(f"Imported Artem core from: {archive}")
    print(f"Output: {output}")
    print(f"Legacy DLL excluded: yes")
    print(f"Quest image repairs: {image_fixes}")
    print(f"Sweden Patch offer restored: {'yes' if sweden_added else 'already present'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
