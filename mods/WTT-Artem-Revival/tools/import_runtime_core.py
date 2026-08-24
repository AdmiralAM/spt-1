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
RUSSIAN_ITEM_DESCRIPTION_PARTS = tuple(LOCALIZATION_ROOT.glob("ru-item-descriptions-*.json"))

LABEL_REPLACEMENTS = (
    ("Night Vision Goggles", "ПНВ"),
    ("Night Vision goggles", "ПНВ"),
    ("NVG Battery Pack", "батарейный блок ПНВ"),
    ("Ballistic Face Mask", "баллистическая маска"),
    ("Ballsitic Face Mask", "баллистическая маска"),
    ("Tactical Bump Helmet", "тактический защитный шлем"),
    ("Tactical Helmet", "тактический шлем"),
    ("High Cut Helmet", "высокообрезанный шлем"),
    ("Ballistic Helmet", "баллистический шлем"),
    ("Plate Carrier", "бронежилет"),
    ("PLATE CARRIER", "БРОНЕЖИЛЕТ"),
    ("plate carrier", "бронежилет"),
    ("Body Armor", "бронежилет"),
    ("body armor", "бронежилет"),
    ("raid backpack", "рейдовый рюкзак"),
    ("Raid Backpack", "рейдовый рюкзак"),
    ("Helmet Cover", "чехол на шлем"),
    ("Battery Pack", "батарейный блок"),
    ("Scrim Net", "маскировочная сетка"),
    ("Gas Mask", "противогаз"),
    ("Face Mask", "маска"),
    ("Skull Mask", "маска с черепом"),
    ("Half-Mask", "полумаска"),
    ("Balaclava", "балаклава"),
    ("Headset", "гарнитура"),
    ("Medical Box", "медицинский ящик"),
    ("Item Case", "контейнер для предметов"),
    ("Figure", "фигурка"),
    ("Patch", "нашивка"),
    ("COMBAT SHIRT", "БОЕВАЯ РУБАШКА"),
    ("Combat Shirt", "боевая рубашка"),
    ("T-Shirt", "футболка"),
    ("T-shirt", "футболка"),
    ("Hoodie", "худи"),
    ("Flannel Shirt", "фланелевая рубашка"),
    ("Urban Shirt", "городская рубашка"),
    ("Rugby Shirt", "рубашка регби"),
    ("Shirt", "рубашка"),
    ("Ghillie Jacket", "маскировочная куртка"),
    ("Bomber Jacket", "куртка-бомбер"),
    ("Combat Pants", "боевые брюки"),
    ("COMBAT PANTS", "БОЕВЫЕ БРЮКИ"),
    ("TACTICAL PANTS", "ТАКТИЧЕСКИЕ БРЮКИ"),
    ("Field Pants", "полевые брюки"),
    ("Suit Pants", "брюки от костюма"),
    ("Trousers", "брюки"),
    ("Pants", "брюки"),
    ("Jeans", "джинсы"),
    ("Tracksuit", "спортивный костюм"),
    ("Notch Lapel Suit", "костюм с лацканами"),
    ("Business Casual", "деловой повседневный костюм"),
)

COLOR_REPLACEMENTS = (
    ("Multicam Black", "Multicam Black"),
    ("Multicam-Black", "Multicam Black"),
    ("Multicam", "Multicam"),
    ("Ranger Green", "Ranger Green"),
    ("Digital Urban", "Digital Urban"),
    ("M98 Woodland", "M98 Woodland"),
    ("WOODLAND", "Woodland"),
    ("Woodland", "Woodland"),
    ("ALPINE", "Alpine"),
    ("Alpine", "Alpine"),
    ("Navy Blue", "тёмно-синий"),
    ("Dark Blue", "тёмно-синий"),
    ("Coyote", "койот"),
    ("Olive", "оливковый"),
    ("Grey", "серый"),
    ("Gray", "серый"),
    ("Black", "чёрный"),
    ("White", "белый"),
    ("Yellow", "жёлтый"),
    ("Green", "зелёный"),
    ("Brown", "коричневый"),
    ("Cyan", "голубой"),
    ("Red", "красный"),
    ("Tanned", "песочный"),
    ("Tan", "песочный"),
    ("Dark", "тёмный"),
)


def load(path: Path):
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def save(path: Path, value):
    with path.open("w", encoding="utf-8", newline="\n") as handle:
        json.dump(value, handle, indent=4, ensure_ascii=False)
        handle.write("\n")


def load_fragment_map(paths: tuple[Path, ...], label: str) -> dict[str, str]:
    if not paths:
        raise RuntimeError(f"no {label} fragments found in {LOCALIZATION_ROOT}")

    merged: dict[str, str] = {}
    for path in sorted(paths):
        payload = load(path)
        if not isinstance(payload, dict):
            raise RuntimeError(f"{label} fragment must be an object: {path}")
        overlap = merged.keys() & payload.keys()
        if overlap:
            raise RuntimeError(
                f"duplicate {label} keys in {path.name}: {', '.join(sorted(overlap)[:10])}"
            )
        merged.update(payload)
    return merged


def translate_label(value: str) -> str:
    """Translate stable gear/clothing vocabulary while preserving brands/models."""
    translated = value
    for source, target in LABEL_REPLACEMENTS:
        translated = translated.replace(source, target)
    for source, target in COLOR_REPLACEMENTS:
        translated = translated.replace(source, target)
    return translated


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
    """Ensure every authored Success AssortmentUnlock is represented in QuestAssort."""
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


def normalize_quest_locales(resources: Path) -> tuple[int, int]:
    """Normalize legacy English locale name and write complete Russian quest locale."""
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

    russian = load_fragment_map(RUSSIAN_QUEST_PARTS, "Russian quest locale")
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


def localize_custom_items(resources: Path) -> int:
    descriptions = load_fragment_map(
        RUSSIAN_ITEM_DESCRIPTION_PARTS, "Russian item description"
    )
    item_dir = resources / "db/CustomItems"
    changed = 0
    uncovered_descriptions: set[str] = set()

    for path in sorted(item_dir.glob("*.json")):
        payload = load(path)
        file_changed = False
        for config in payload.values():
            locales = config.setdefault("locales", {})
            english = locales.get("en")
            if not isinstance(english, dict):
                continue

            description = english.get("description", "") or ""
            if description and description not in descriptions:
                uncovered_descriptions.add(description)
                continue

            locales["ru"] = {
                "name": translate_label(english.get("name", "") or ""),
                "shortName": translate_label(english.get("shortName", "") or ""),
                "description": descriptions.get(description, ""),
            }
            changed += 1
            file_changed = True

        if file_changed:
            save(path, payload)

    if uncovered_descriptions:
        examples = sorted(uncovered_descriptions)[:3]
        raise RuntimeError(
            f"{len(uncovered_descriptions)} custom item descriptions lack Russian translation: {examples}"
        )
    return changed


def localize_clothing(resources: Path) -> int:
    clothing_dir = resources / "db/CustomClothing"
    changed = 0

    for path in sorted(clothing_dir.glob("*.json")):
        payload = load(path)
        file_changed = False
        for config in payload:
            locales = config.setdefault("locales", {})
            english = locales.get("en")
            if not isinstance(english, dict):
                continue

            english_name = english.get("name", "") or ""
            english_description = english.get("description", "") or ""
            locales["ru"] = {
                "name": translate_label(english_name),
                "description": translate_label(english_description),
            }
            changed += 1
            file_changed = True

        if file_changed:
            save(path, payload)
    return changed


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("archive", type=Path, help="Path to authoritative 'artem main 1.zip'")
    parser.add_argument(
        "--output",
        type=Path,
        default=MODULE_ROOT / "server/Resources",
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
    localized_items = localize_custom_items(output)
    localized_clothing = localize_clothing(output)

    print(f"Imported Artem core from: {archive}")
    print(f"Output: {output}")
    print("Legacy DLL excluded: yes")
    print(f"Quest image repairs: {image_fixes}")
    print(f"Sweden Patch offer restored: {'yes' if sweden_added else 'already present'}")
    print(f"QuestAssort success mappings repaired: {len(quest_assort_repairs)}")
    if quest_assort_repairs:
        print("QuestAssort repaired offers: " + ", ".join(quest_assort_repairs))
    print(f"Quest locale keys: en={en_keys}, ru={ru_keys}")
    print(f"Custom items with Russian locale: {localized_items}")
    print(f"Clothing entries with Russian locale: {localized_clothing}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
