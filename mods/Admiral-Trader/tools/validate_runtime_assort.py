#!/usr/bin/env python3
import json
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSORT_PATH = ROOT / "db" / "assort.json"
QUESTASSORT_PATH = ROOT / "db" / "questassort.json"
QUEST_DIR = ROOT / "db" / "quests"
RUNTIME_MANIFEST_PATH = ROOT / "manifests" / "runtime-manifest.json"
CSPROJ_PATH = ROOT / "server" / "AdmiralTrader.Server.csproj"

EXPECTED_RUNTIME_TARGET = "4.1.3"
EXPECTED_PUBLISHED_API_BASELINE = "4.1.2"
EXPECTED_OFFER_ID = "ad1000000000000000000001"
EXPECTED_ITEM_TPL = "5c94bbff86f7747ee735c08f"
EXPECTED_CLEARANCE_QUEST = "68a6527a3c73b2e85977d7a1"
RUB_TPL = "5449016a4bdc2d6f028b456f"


def fail(message: str) -> None:
    raise SystemExit(message)


def validate_runtime_target() -> None:
    runtime = json.loads(RUNTIME_MANIFEST_PATH.read_text(encoding="utf-8"))
    if runtime.get("targetSptVersion") != EXPECTED_RUNTIME_TARGET:
        fail(f"runtime target drift: {runtime.get('targetSptVersion')} != {EXPECTED_RUNTIME_TARGET}")
    if runtime.get("publishedApiCompileBaseline") != EXPECTED_PUBLISHED_API_BASELINE:
        fail("published API compile baseline drift")

    root = ET.parse(CSPROJ_PATH).getroot()
    props = {child.tag: (child.text or "").strip() for group in root.findall("PropertyGroup") for child in group}
    if props.get("SptRuntimeTarget") != EXPECTED_RUNTIME_TARGET:
        fail("csproj SptRuntimeTarget drift")
    if props.get("SptPublishedApiBaseline") != EXPECTED_PUBLISHED_API_BASELINE:
        fail("csproj SptPublishedApiBaseline drift")

    package_versions = []
    for group in root.findall("ItemGroup"):
        for package in group.findall("PackageReference"):
            if package.attrib.get("Include", "").startswith("SPTarkov."):
                package_versions.append(package.attrib.get("Version"))
    if not package_versions or any(version != "$(SptPublishedApiBaseline)" for version in package_versions):
        fail(f"SPT package references must use the published API baseline property: {package_versions}")


def main() -> None:
    validate_runtime_target()
    assort = json.loads(ASSORT_PATH.read_text(encoding="utf-8"))
    questassort = json.loads(QUESTASSORT_PATH.read_text(encoding="utf-8"))

    items = assort.get("items")
    barter = assort.get("barter_scheme")
    loyalty = assort.get("loyal_level_items")
    if not isinstance(items, list) or not isinstance(barter, dict) or not isinstance(loyalty, dict):
        fail("assort native collections have invalid types")

    root_ids = [item.get("_id") for item in items if item.get("parentId") == "hideout"]
    if len(root_ids) != len(set(root_ids)):
        fail("assort contains duplicate root ids")
    if root_ids != [EXPECTED_OFFER_ID]:
        fail(f"unexpected first assort roots: {root_ids}")

    item = items[0]
    if item.get("_tpl") != EXPECTED_ITEM_TPL:
        fail(f"first offer template drift: {item.get('_tpl')}")
    upd = item.get("upd", {})
    if upd.get("UnlimitedCount") is not False or upd.get("StackObjectsCount") != 1:
        fail("first offer must be finite with stock count 1")
    if upd.get("BuyRestrictionMax") != 1:
        fail("first offer must have per-reset buy restriction 1")

    scheme = barter.get(EXPECTED_OFFER_ID)
    if not isinstance(scheme, list) or len(scheme) != 1 or not isinstance(scheme[0], list) or len(scheme[0]) != 1:
        fail("first offer must have exactly one single-currency barter scheme")
    price = scheme[0][0]
    if price.get("_tpl") != RUB_TPL or price.get("count") != 166000:
        fail(f"first offer price drift: {price}")
    if loyalty.get(EXPECTED_OFFER_ID) != 4:
        fail("first offer must require Admiral LL4")

    success = questassort.get("Success")
    if not isinstance(success, dict) or success.get(EXPECTED_OFFER_ID) != EXPECTED_CLEARANCE_QUEST:
        fail("first offer is not gated by Access Protocol: Clearance success")
    for state in ("Started", "Fail"):
        mapping = questassort.get(state)
        if not isinstance(mapping, dict) or EXPECTED_OFFER_ID in mapping:
            fail(f"first offer must not be bound in questassort.{state}")

    quest_ids = set()
    for path in QUEST_DIR.glob("*.json"):
        quest = json.loads(path.read_text(encoding="utf-8"))
        quest_ids.add(str(quest.get("_id")))
    if EXPECTED_CLEARANCE_QUEST not in quest_ids:
        fail("questassort references a missing Clearance quest template")

    print("Admiral Trader SPT 4.1.3 target + runtime assort contract OK")


if __name__ == "__main__":
    main()
