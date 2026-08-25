#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSORT_PATH = ROOT / "db" / "assort.json"
QUESTASSORT_PATH = ROOT / "db" / "questassort.json"
QUEST_DIR = ROOT / "db" / "quests"

EXPECTED_OFFER_ID = "ad1000000000000000000001"
EXPECTED_ITEM_TPL = "5c94bbff86f7747ee735c08f"
EXPECTED_CLEARANCE_QUEST = "68a6527a3c73b2e85977d7a1"
RUB_TPL = "5449016a4bdc2d6f028b456f"


def fail(message: str) -> None:
    raise SystemExit(message)


def main() -> None:
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

    print("Admiral Trader runtime assort contract OK: 1 Clearance-gated LL4 Labs access offer")


if __name__ == "__main__":
    main()
