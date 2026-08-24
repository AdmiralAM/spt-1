import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOCALIZATION = ROOT / "localization"
IMPORTER = ROOT / "tools" / "import_runtime_core.py"


def load_fragments(pattern: str):
    paths = sorted(LOCALIZATION.glob(pattern))
    assert paths, f"no localization fragments matched {pattern}"

    merged = {}
    for path in paths:
        payload = json.loads(path.read_text(encoding="utf-8-sig"))
        assert isinstance(payload, dict), f"{path.name} must contain an object"
        overlap = merged.keys() & payload.keys()
        assert not overlap, f"duplicate keys across {pattern}: {sorted(overlap)[:5]}"
        merged.update(payload)
    return paths, merged


def load_importer():
    spec = importlib.util.spec_from_file_location("artem_importer", IMPORTER)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    quest_parts, quests = load_fragments("ru-quests-*.json")
    assert len(quest_parts) == 4
    assert len(quests) == 204, f"expected 204 Russian quest locale keys, got {len(quests)}"
    assert all(isinstance(v, str) and v.strip() for v in quests.values())
    assert sum(any("а" <= ch.lower() <= "я" or ch.lower() == "ё" for ch in v) for v in quests.values()) >= 190

    description_parts, descriptions = load_fragments("ru-item-descriptions-*.json")
    assert len(description_parts) == 2
    assert len(descriptions) == 48, f"expected 48 Russian item descriptions, got {len(descriptions)}"
    assert all(isinstance(k, str) and k for k in descriptions)
    assert all(isinstance(v, str) and v for v in descriptions.values())

    importer = load_importer()
    assert importer.translate_label("Helmet Cover Black") == "чехол на шлем чёрный"
    assert importer.translate_label("Ranger Green Hoodie") == "Ranger Green худи"
    assert importer.translate_label("CRYE Precision GEN.4 Combat Pants (Navy Blue)") == "CRYE Precision GEN.4 боевые брюки (тёмно-синий)"
    assert importer.translate_label("Sweden Patch") == "Sweden нашивка"

    print(
        f"OK: {len(quests)} Russian quest keys, {len(descriptions)} item descriptions, "
        f"{len(quest_parts) + len(description_parts)} localization source fragments"
    )


if __name__ == "__main__":
    main()
