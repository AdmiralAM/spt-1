import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ARTEM_ID = "66bf757f27d0b097db0acea5"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def test_complete_artem_runtime_is_owned_by_admiral() -> None:
    assort = load(ROOT / "external/artem/db/assort.json")
    quests = load(next((ROOT / f"db/CustomQuests/{ARTEM_ID}/Quests").glob("*.json")))
    quest_assort = load(next((ROOT / f"db/CustomQuests/{ARTEM_ID}/QuestAssort").glob("*.json")))
    suits = load(next((ROOT / "db/CustomClothing").glob("*.json")))
    zones = load(next((ROOT / "db/CustomQuestZones").glob("*.json")))

    assert len(assort["items"]) == 703
    assert sum(item.get("parentId") == "hideout" for item in assort["items"]) == 281
    assert len(quests) == 23
    assert sum(len(records) for records in quest_assort.values()) == 41
    assert len(suits) == 64
    assert len(zones) == 21
    assert len(list((ROOT / "db/CustomItems").glob("*.json"))) == 6
    bundle_inventory = load(ROOT / "manifests/artem-bundle-inventory.json")
    assert bundle_inventory["bundleCount"] == 262
    assert len(bundle_inventory["bundles"]) == 262
    assert len({entry["path"] for entry in bundle_inventory["bundles"]}) == 262
    assert all(len(entry["sha256"]) == 64 and entry["size"] > 0 for entry in bundle_inventory["bundles"])
    assert len(list((ROOT / f"db/CustomQuests/{ARTEM_ID}/Images").iterdir())) == 24
    assert len(list((ROOT / f"db/CustomQuests/{ARTEM_ID}/Locales").glob("*.json"))) == 3


def test_painter_bundle_layer_is_reproducible_and_coexists_with_artem() -> None:
    inventory = load(ROOT / "manifests/painter-bundle-inventory.json")
    build = (ROOT / "tools/Build-Spt415Rc.ps1").read_text(encoding="utf-8")
    registration = (ROOT / "server/ArtemContentRegistration.cs").read_text(encoding="utf-8")

    assert inventory["bundleCount"] == 5
    assert len(inventory["bundles"]) == 5
    assert len({entry["path"] for entry in inventory["bundles"]}) == 5
    assert all(len(entry["sha256"]) == 64 and entry["size"] > 0 for entry in inventory["bundles"])
    assert "Hydrate-PainterBundles.ps1" in build
    assert "262 Artem and 5 Painter" in build
    assert "requiredBundles.Any" in registration
    for hydrator_name in ("Hydrate-ArtemBundles.ps1", "Hydrate-PainterBundles.ps1"):
        hydrator = (ROOT / "tools" / hydrator_name).read_text(encoding="utf-8")
        assert "$sevenZipCommand = Get-Command 7z.exe -ErrorAction SilentlyContinue" in hydrator
        assert "if ($null -ne $sevenZipCommand)" in hydrator
        assert "$tarCommand = Get-Command tar.exe -ErrorAction SilentlyContinue" in hydrator
        assert "-xf $archive -C $extract" in hydrator


def test_persistent_artem_id_sets_match_inventory() -> None:
    identities = load(ROOT / "manifests/external-content-identities.json")["artem"]
    assort = load(ROOT / "external/artem/db/assort.json")
    quests = load(next((ROOT / f"db/CustomQuests/{ARTEM_ID}/Quests").glob("*.json")))
    quest_assort = load(next((ROOT / f"db/CustomQuests/{ARTEM_ID}/QuestAssort").glob("*.json")))
    suits = load(next((ROOT / "db/CustomClothing").glob("*.json")))

    roots = {item["_id"] for item in assort["items"] if item.get("parentId") == "hideout"}
    unlocks = {offer for records in quest_assort.values() for offer in records}
    assert set(identities["questIds"]) == set(quests)
    assert set(identities["offerIds"]) == roots
    assert set(identities["questUnlockOfferIds"]) == unlocks
    assert set(identities["suitIds"]) == {record["suiteId"] for record in suits}


def test_no_standalone_artem_runtime_or_empty_painter_color() -> None:
    project = (ROOT / "server/AdmiralTrader.Server.csproj").read_text(encoding="utf-8")
    registration = (ROOT / "server/ArtemContentRegistration.cs").read_text(encoding="utf-8")
    painter = (ROOT / "server/PainterContentRegistration.cs").read_text(encoding="utf-8")
    assert "Admiral-Artyom-Revival" not in project
    assert "WTT-Artem.dll" not in project
    assert "Admiral Artem Content Provider.dll" not in project
    assert "standalone Artem runtime is not required" in registration
    assert 'BackgroundColor = item.Width is null ? "default" : "blue"' in painter
    assert 'BackgroundColor = item.Width is null ? string.Empty' not in painter


def test_embedded_external_content_has_complete_russian_copy() -> None:
    artem_locale_root = ROOT / f"db/CustomQuests/{ARTEM_ID}/Locales"
    artem_en = load(artem_locale_root / "en.json")
    artem_ru = load(artem_locale_root / "ru.json")
    painter_locale_root = ROOT / "external/painter/db/CustomQuests/668aaff35fd574b6dcc4a686/Locales"
    painter_en = load(painter_locale_root / "en.json")
    painter_ru = load(painter_locale_root / "ru.json")

    assert set(artem_ru) == set(artem_en)
    assert set(painter_ru) == set(painter_en)
    assert all(artem_ru[key].strip() for key in artem_en if artem_en[key].strip())
    assert all(painter_ru[key].strip() for key in painter_en if painter_en[key].strip())

    custom_items = {}
    for path in (ROOT / "db/CustomItems").glob("*.json"):
        custom_items.update(load(path))
    assert len(custom_items) == 131
    for item_id, record in custom_items.items():
        locales = record.get("locales", {})
        assert "en" in locales and "ru" in locales, item_id
        for field in ("name", "shortName", "description"):
            assert locales["ru"].get(field, "").strip(), f"{item_id} missing Russian {field}"

    expected_names = {
        "66326bfd46817c660d015125": "Шлем DLP Tactical Extreme",
        "66bf757f27d0b097db0ace46": "Инфракрасная камера MOHOC®",
        "67548853f1ba8ca2b34fa4d0": "Прибор ночного видения PVS-31",
        "66326bfd46817c660d015141": "Маска Никто",
        "669819683571cb050b0b6391": "Волчий капюшон",
        "66bf757f27d0b097db0ace69": "Резной трофей «Хедкраб»",
        "66326bfd46817c660d01513b": "Бронежилет MSV-GEN II",
    }
    for item_id, name in expected_names.items():
        assert custom_items[item_id]["locales"]["ru"]["name"] == name

    clothing = load(ROOT / "db/CustomClothing/Artem Clothes.json")
    assert len(clothing) == 64
    for row in clothing:
        assert row["locales"]["ru"]["name"].strip(), row["suiteId"]
        assert row["locales"]["ru"]["description"].strip(), row["suiteId"]
    russian_clothing_names = {row["locales"]["ru"]["name"] for row in clothing}
    assert "Куртка Softshell (Alpine MC)" in russian_clothing_names
    assert "Устранитель проблем" in russian_clothing_names
    assert "Брюки Woodland Infiltrator (Alpine)" in russian_clothing_names
