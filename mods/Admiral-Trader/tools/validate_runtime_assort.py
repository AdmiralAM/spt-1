#!/usr/bin/env python3
import json
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSORT_PATH = ROOT / "db" / "assort.json"
QUESTASSORT_PATH = ROOT / "db" / "questassort.json"
QUEST_DIR = ROOT / "db" / "quests"
BASE_PATH = ROOT / "db" / "base.json"
RUNTIME_MANIFEST_PATH = ROOT / "manifests" / "runtime-manifest.json"
BASELINE_STOCK_PATH = ROOT / "manifests" / "baseline-stock.json"
AMMO_POLICY_PATH = ROOT / "manifests" / "ammo-offer-policy.json"
CSPROJ_PATH = ROOT / "server" / "AdmiralTrader.Server.csproj"

EXPECTED_RUNTIME_TARGET = "4.1.5"
EXPECTED_PUBLISHED_API_BASELINE = "4.1.5"
LABS_OFFER_ID = "ad1000000000000000000001"
LABS_ITEM_TPL = "5c94bbff86f7747ee735c08f"
LABS_CLEARANCE_QUEST = "68a6527a3c73b2e85977d7a1"
RUB_TPL = "5449016a4bdc2d6f028b456f"
BASELINE_OFFER_IDS = {
    "ad2000000000000000000001",
    "ad2000000000000000000002",
    "ad2000000000000000000003",
    "ad2000000000000000000004",
}
AMMO_OFFER_IDS = {
    "handguns": "6cf0fc22a55417075c5af23e",
    "smg-pdw": "67d5501fb925a7836b99f112",
    "shotguns": "ece0342652b331ac065d2e5a",
    "assault-rifles": "b71182859e5958fd12c02e89",
    "marksman-battle": "07efd6dee267ec18ed830dd6",
    "precision": "731e65964d324bc545a1b839",
}
NATIVE_QUESTASSORT_KEYS = {"started", "success", "fail"}
LEGACY_CAPITALIZED_QUESTASSORT_KEYS = {"Started", "Success", "Fail"}


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
            if package.attrib.get("Include", "").startswith(("SPTarkov.", "SPTushonka.")):
                package_versions.append(package.attrib.get("Version"))
    if not package_versions or any(version != "$(SptPublishedApiBaseline)" for version in package_versions):
        fail(f"SPT package references must use the published API baseline property: {package_versions}")


def validate_single_rub_offer(offer_id: str, item: dict, barter: dict, loyalty: dict, *, tpl: str, price: int, stock: int, buy_limit: int, loyalty_level: int = 1) -> None:
    if item.get("_tpl") != tpl or item.get("parentId") != "hideout" or item.get("slotId") != "hideout":
        fail(f"{offer_id}: native root item contract drift")
    upd = item.get("upd") or {}
    if upd.get("UnlimitedCount") is not False:
        fail(f"{offer_id}: offer must remain finite")
    if upd.get("StackObjectsCount") != stock:
        fail(f"{offer_id}: stock drift: {upd.get('StackObjectsCount')} != {stock}")
    if upd.get("BuyRestrictionMax") != buy_limit or upd.get("BuyRestrictionCurrent") != 0:
        fail(f"{offer_id}: buy restriction drift")
    scheme = barter.get(offer_id)
    if not isinstance(scheme, list) or len(scheme) != 1 or not isinstance(scheme[0], list) or len(scheme[0]) != 1:
        fail(f"{offer_id}: expected exactly one single-currency barter scheme")
    currency = scheme[0][0]
    if currency.get("_tpl") != RUB_TPL or currency.get("count") != price:
        fail(f"{offer_id}: RUB price drift: {currency}")
    if loyalty.get(offer_id) != loyalty_level:
        fail(f"{offer_id}: loyalty level drift: {loyalty.get(offer_id)} != {loyalty_level}")


def main() -> None:
    validate_runtime_target()
    assort = json.loads(ASSORT_PATH.read_text(encoding="utf-8"))
    questassort = json.loads(QUESTASSORT_PATH.read_text(encoding="utf-8"))
    baseline = json.loads(BASELINE_STOCK_PATH.read_text(encoding="utf-8"))
    ammo_policy = json.loads(AMMO_POLICY_PATH.read_text(encoding="utf-8"))
    base = json.loads(BASE_PATH.read_text(encoding="utf-8"))

    if set(questassort) != NATIVE_QUESTASSORT_KEYS:
        fail(f"questassort top-level keys must be exact native lower-case {sorted(NATIVE_QUESTASSORT_KEYS)}, got {sorted(questassort)}")
    if LEGACY_CAPITALIZED_QUESTASSORT_KEYS & set(questassort):
        fail("legacy capitalized questassort state keys are forbidden on SPT 4.1.5")

    if baseline.get("targetSptVersion") != EXPECTED_RUNTIME_TARGET:
        fail("Baseline stock target drift")
    if baseline.get("stockClass") != "Baseline" or baseline.get("status") != "FrozenPreserved":
        fail("Baseline stock authority drift")
    baseline_offers = baseline.get("offers") or []
    baseline_by_id = {str(row.get("offerId")): row for row in baseline_offers}
    if set(baseline_by_id) != BASELINE_OFFER_IDS or len(baseline_offers) != 4:
        fail(f"Baseline stock must contain exactly four preserved offers: {sorted(baseline_by_id)}")
    if any(row.get("questGate") is not None for row in baseline_offers):
        fail("Baseline offers must remain non-quest-gated")

    items = assort.get("items")
    barter = assort.get("barter_scheme")
    loyalty = assort.get("loyal_level_items")
    if not isinstance(items, list) or not isinstance(barter, dict) or not isinstance(loyalty, dict):
        fail("assort native collections have invalid types")

    milestone_ids = {LABS_OFFER_ID, *AMMO_OFFER_IDS.values()}
    expected_ids = BASELINE_OFFER_IDS | milestone_ids
    root_items = {item.get("_id"): item for item in items if item.get("parentId") == "hideout"}
    if len(root_items) != len(items) or len(root_items) != 11:
        fail(f"expected exactly eleven root-only Admiral offers, got roots={len(root_items)} items={len(items)}")
    if set(root_items) != expected_ids:
        fail(f"assort root id drift; missing={sorted(expected_ids-set(root_items))} extra={sorted(set(root_items)-expected_ids)}")
    if set(barter) != expected_ids or set(loyalty) != expected_ids:
        fail("assort root/barter/loyalty key sets must match the 4 Baseline + 7 Milestone contract")

    for offer_id, policy in baseline_by_id.items():
        validate_single_rub_offer(
            offer_id,
            root_items[offer_id],
            barter,
            loyalty,
            tpl=str(policy["tpl"]),
            price=int(policy["priceRub"]),
            stock=int(policy["stockPerReset"]),
            buy_limit=int(policy["buyRestriction"]),
            loyalty_level=int(policy["loyaltyLevel"]),
        )

    validate_single_rub_offer(
        LABS_OFFER_ID,
        root_items[LABS_OFFER_ID],
        barter,
        loyalty,
        tpl=LABS_ITEM_TPL,
        price=166000,
        stock=1,
        buy_limit=1,
    )

    offers = ammo_policy.get("offers") or {}
    if set(offers) != set(AMMO_OFFER_IDS):
        fail("ammo offer policy family set drift")
    if ammo_policy.get("targetSptVersion") != EXPECTED_RUNTIME_TARGET:
        fail("ammo offer policy lost SPT 4.1.5 target")
    if (ammo_policy.get("specialWeapons") or {}).get("permanentOffer") is not False:
        fail("Special Weapons must not receive a permanent offer")

    success = questassort.get("success")
    if not isinstance(success, dict) or set(success) != milestone_ids:
        fail("questassort.success must contain exactly the seven Milestone offers and no Baseline offers")
    if BASELINE_OFFER_IDS & set(success):
        fail("Baseline offers must never leak into questassort.success")
    if success.get(LABS_OFFER_ID) != LABS_CLEARANCE_QUEST:
        fail("Labs offer is not gated by Access Protocol: Clearance success")

    for family, policy in offers.items():
        offer_id = AMMO_OFFER_IDS[family]
        validate_single_rub_offer(
            offer_id,
            root_items[offer_id],
            barter,
            loyalty,
            tpl=str(policy["tpl"]),
            price=int(policy["priceRub"]),
            stock=int(policy["stockPerReset"]),
            buy_limit=int(policy["buyRestriction"]),
        )
        if success.get(offer_id) != str(policy["questId"]):
            fail(f"{family}: questassort success gate drift")

    for state in ("started", "fail"):
        mapping = questassort.get(state)
        if not isinstance(mapping, dict) or mapping:
            fail(f"questassort.{state} must remain empty")

    quest_ids = set()
    for path in QUEST_DIR.glob("*.json"):
        quest = json.loads(path.read_text(encoding="utf-8"))
        quest_ids.add(str(quest.get("_id")))
    missing_quests = sorted(set(success.values()) - quest_ids)
    if missing_quests:
        fail(f"questassort references missing quest templates: {missing_quests}")

    loyalty_levels = base.get("loyaltyLevels") or []
    expected_standing = [0, 0.10, 0.30, 0.55]
    if len(loyalty_levels) != 4:
        fail("Admiral must retain four loyalty levels")
    for index, (level, standing) in enumerate(zip(loyalty_levels, expected_standing), start=1):
        if level.get("minSalesSum") != 0:
            fail(f"Admiral LL{index}: sales-sum grind must remain disabled")
        if float(level.get("minStanding", -1)) != standing:
            fail(f"Admiral LL{index}: standing threshold drift")

    print("Admiral Trader SPT 4.1.5 native questassort + 4 Baseline + 7 Milestone offer contract OK")


if __name__ == "__main__":
    main()
