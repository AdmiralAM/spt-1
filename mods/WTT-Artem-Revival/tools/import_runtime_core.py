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
TRADER_ID = "66bf757f27d0b097db0acea5"
MODULE_ROOT = Path(__file__).resolve().parents[1]
LOCALIZATION_ROOT = MODULE_ROOT / "localization"
RUSSIAN_QUEST_PARTS = tuple(LOCALIZATION_ROOT.glob("ru-quests-*.json"))


def load(path: Path):
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def save(path: Path, value):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, indent=4, ensure_ascii=False)
        handle.write("\n")


def patch_quest_image(resources: Path) -> int:
    quest_path = resources / f"db/CustomQuests/{TRADER_ID}/Quests/ArtemQuests.json"
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


def sync_success_quest_assort(resources: Path) -> list[str]:
    """Ensure every authored Success AssortmentUnlock is represented in QuestAssort.

    This is additive on purpose. Existing QuestAssort-only entries are preserved
    because they may be intentional hidden unlocks; only missing/mismatched reward
    targets are repaired from the explicit quest reward declarations.
    """
    quest_path = resources / f"db/CustomQuests/{TRADER_ID}/Quests/ArtemQuests.json"
    quest_assort_path = resources / f"db/CustomQuests/{TRADER_ID}/QuestAssort/Artem_QuestAssort.json"
    quests = load(quest_path)
    quest_assort = load(quest_assort_path)
    success = quest_assort.setdefault("success", {})
    repaired: list[str] = []

    for quest_id, quest in quests.items():
        for reward in quest.get("rewards", {}).get("Success", []):
            if reward.get("type") != "AssortmentUnlock":
                continue
            offer_id = reward.get("target")
            if not offer_id:
                continue
            if success.get(offer_id) != quest_id:
                success[offer_id] = quest_id
                repaired.append(offer_id)

    if repaired:
        save(quest_assort_path, quest_assort)
    return repaired


def load_russian_quest_locale() -> dict[str, str]:
    """Load durable Russian quest source fragments and reject duplicate keys."""
    if not RUSSIAN_QUEST_PARTS:
        raise RuntimeError(f"no Russian quest locale fragments found in {LOCALIZATION_ROOT}")

    merged: dict[str, str] = {}
    for path in sorted(RUSSIAN_QUEST_PARTS):
        payload = load(path)
        if not isinstance(payload, dict):
            raise RuntimeError(f"Russian locale fragment must be an object: {path}")
        overlap = merged.keys() & payload.keys()
        if overlap:
            raise RuntimeError(
                f"duplicate Russian quest locale keys in {path.name}: {', '.join(sorted(overlap))}"
            )
        merged.update(payload)
    return merged


def normalize_quest_locales(resources: Path) -> tuple[int, int]:
    """Create real CommonLib locale codes (`en.json`, `ru.json`).

    Legacy Artem named its English file `artemenglish.json`. CommonLib 3.x derives
    the locale code from the filename, so that legacy name becomes an unknown
    locale code and every real game locale falls back to English. The revival
    normalizes English to `en.json` and writes a complete Russian `ru.json`.
    """
    locale_dir = resources / f"db/CustomQuests/{TRADER_ID}/Locales"
    legacy_english = locale_dir / "artemenglish.json"
    english_path = locale_dir / "en.json"
    russian_path = locale_dir / "ru.json"

    if legacy_english.is_file():
        english = load(legacy_english)
    elif english_path.is_file():
        english = load(english_path)
    else:
        raise RuntimeError(f"Artem English quest locale not found in {locale_dir}")

    russian = load_russian_quest_locale()
    english_keys = set(english)
    russian_keys = set(russian)
    if english_keys != russian_keys:
        missing = sorted(english_keys - russian_keys)
        extra = sorted(russian_keys - english_keys)
        raise RuntimeError(
            "Russian quest locale key mismatch: "
            f"missing={missing[:10]} extra={extra[:10]} "
            f"(en={len(english_keys)}, ru={len(russian_keys)})"
        )

    save(english_path, english)
    save(russian_path, russian)
    if legacy_english.exists() and legacy_english != english_path:
        legacy_english.unlink()

    return len(english), len(russian)


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
    quest_assort_repairs = sync_success_quest_assort(output)
    en_keys, ru_keys = normalize_quest_locales(output)

    print(f"Imported Artem core from: {archive}")
    print(f"Output: {output}")
    print("Legacy DLL excluded: yes")
    print(f"Quest image repairs: {image_fixes}")
    print(f"Sweden Patch offer restored: {'yes' if sweden_added else 'already present'}")
    print(f"QuestAssort success mappings repaired: {len(quest_assort_repairs)}")
    if quest_assort_repairs:
        print("QuestAssort repaired offers: " + ", ".join(quest_assort_repairs))
    print(f"Quest locale keys: en={en_keys}, ru={ru_keys}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
