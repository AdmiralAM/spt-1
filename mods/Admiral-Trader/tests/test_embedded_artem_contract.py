import hashlib
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
    assert bundle_inventory["verifiedOverrides"] == [
        "artemgpnvgblack.bundle",
        "artemgpnvgod.bundle",
        "artemgpnvgwhite.bundle",
    ]
    assert len(bundle_inventory["bundles"]) == 262
    assert len({entry["path"] for entry in bundle_inventory["bundles"]}) == 262
    assert all(len(entry["sha256"]) == 64 and entry["size"] > 0 for entry in bundle_inventory["bundles"])
    assert len(list((ROOT / f"db/CustomQuests/{ARTEM_ID}/Images").iterdir())) == 24
    assert len(list((ROOT / f"db/CustomQuests/{ARTEM_ID}/Locales").glob("*.json"))) == 3

    for filename in bundle_inventory["verifiedOverrides"]:
        entry = next(row for row in bundle_inventory["bundles"] if row["path"] == filename)
        path = ROOT / "tools/artem-bundle-overrides" / filename
        assert path.stat().st_size == entry["size"]
        assert hashlib.sha256(path.read_bytes()).hexdigest() == entry["sha256"]

    custom_items = {}
    for path in (ROOT / "db/CustomItems").glob("*.json"):
        custom_items.update(load(path))
    sold_custom = {
        item["_tpl"] for item in assort["items"]
        if item.get("parentId") == "hideout" and item["_tpl"] in custom_items
    }
    bundle_paths = {entry["path"] for entry in bundle_inventory["bundles"]}
    bundle_keys = {entry["key"] for entry in load(ROOT / "bundles.json")["manifest"]}
    for item_id in sold_custom:
        prefab = custom_items[item_id]["overrideProperties"]["Prefab"]["path"]
        assert prefab in bundle_paths, item_id
        assert prefab in bundle_keys, item_id


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
    assert "dotnet build $project -c Release --no-restore" in build
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


def test_snacky_and_painter_delivery_have_truthful_copy_assets_and_finite_routes() -> None:
    custom_items = {}
    for path in (ROOT / "db/CustomItems").glob("*.json"):
        custom_items.update(load(path))
    snacky = custom_items["66326bfd46817c660d015150"]
    assert snacky["locales"]["ru"] == {
        "name": "Компактный подсумок Snacky-Z",
        "shortName": "Snacky-Z",
        "description": "Компактный подсумок-контейнер из комплекта Artem. Занимает 2x1 ячейки в схроне и предоставляет внутреннюю сетку 5x5 для небольших припасов и ценностей.",
    }

    artem_assort = load(ROOT / "external/artem/db/assort.json")
    snacky_offer = "66bf757f27d0b097db0acfc9"
    assert artem_assort["barter_scheme"][snacky_offer] == [[
        {"count": 4, "_tpl": "57347c1124597737fb1379e3"},
        {"count": 4, "_tpl": "5734795124597738002c6176"},
        {"count": 1, "_tpl": "61bf83814088ec1a363d7097"},
    ]]
    snacky_root = next(row for row in artem_assort["items"] if row["_id"] == snacky_offer)
    assert snacky_root["_tpl"] == "66326bfd46817c660d015150"
    assert snacky_root["upd"]["BuyRestrictionMax"] == 1

    painter_assort = load(ROOT / "external/painter/db/assort.json")
    delivery_offer = "67fef66cfdca1b1dd0eee5a1"
    assert painter_assort["barter_scheme"][delivery_offer] == [[
        {"count": 12, "_tpl": "57347c1124597737fb1379e3"},
        {"count": 12, "_tpl": "5734795124597738002c6176"},
        {"count": 10, "_tpl": "5d235b4d86f7742e017bc88a"},
    ]]
    delivery_root = next(row for row in painter_assort["items"] if row["_id"] == delivery_offer)
    assert delivery_root["_tpl"] == "668ff5bde41a0cce3b142464"
    assert delivery_root["upd"]["BuyRestrictionMax"] == 1

    bundle_keys = {row["key"] for row in load(ROOT / "bundles.json")["manifest"]}
    painter_bundles = {row["path"] for row in load(ROOT / "manifests/painter-bundle-inventory.json")["bundles"]}
    assert painter_bundles <= bundle_keys


def test_optional_tgc_catalogue_fills_only_missing_russian_copy() -> None:
    catalogue = load(ROOT / "db/optional/tgc-ru-fallback.json")
    identities = load(ROOT / "manifests/external-content-identities.json")["tgc"]["itemTemplateIds"]
    registration = (ROOT / "server/OptionalContentRegistration.cs").read_text(encoding="utf-8")
    assert set(catalogue) == set(identities)
    assert all(
        row[field].strip()
        for row in catalogue.values()
        for field in ("name", "shortName", "description")
    )
    assert "templateTable.Items.ContainsKey(row.Key)" in registration
    assert "SetMissing(data" in registration
    assert "string.IsNullOrWhiteSpace(value)" in registration
