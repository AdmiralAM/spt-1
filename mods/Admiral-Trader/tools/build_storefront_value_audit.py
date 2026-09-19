#!/usr/bin/env python3
"""Build an offer-by-offer value, stock and progression audit.

The report is evidence only: it never rewrites assort data. Optional/runtime
sources are included when explicitly supplied and remain safe to omit.
"""
from __future__ import annotations

import argparse
import csv
import json
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RUB = "5449016a4bdc2d6f028b456f"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def merge_locales(paths: list[Path]) -> dict[str, str]:
    result: dict[str, str] = {}
    for folder in paths:
        if not folder.exists():
            continue
        candidates = [folder] if folder.is_file() else list(folder.rglob("*.json"))
        candidates.sort(key=lambda path: (0 if path.name == "ru.json" or path.stem.endswith("-ru") else 1, str(path)))
        for path in candidates:
            try:
                data = load(path)
            except (json.JSONDecodeError, UnicodeDecodeError):
                continue
            if isinstance(data, dict):
                for key, value in data.items():
                    if isinstance(value, str):
                        result.setdefault(str(key), value)
    return result


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--database", required=True, type=Path)
    parser.add_argument("--tgc-assort", type=Path)
    parser.add_argument("--tgc-root", type=Path)
    parser.add_argument("--output", type=Path, default=ROOT / "docs/storefront-value-audit.csv")
    args = parser.parse_args()

    handbook = load(args.database / "templates/handbook.json")
    prices = {str(k): int(v) for k, v in load(args.database / "templates/prices.json").items()}
    handbook_prices = {str(row["Id"]): int(row["Price"]) for row in handbook["Items"]}
    requirement_price = {**handbook_prices, **prices, RUB: 1}
    product_price = {**handbook_prices, RUB: 1}
    locales = merge_locales([
        args.database / "locales/global/ru.json",
        args.database / "locales/global/en.json",
        ROOT / "db/locales",
        ROOT / "external/artem",
        ROOT / "external/painter",
        args.tgc_root or Path("__missing__"),
    ])

    sources = [
        ("Admiral core", ROOT / "db/assort.json", False),
        ("Natalya", ROOT / "db/natalya-signature-assort.json", False),
        ("Artem", ROOT / "external/artem/db/assort.json", False),
        ("Painter", ROOT / "external/painter/db/assort.json", False),
        ("WTT Armory optional", ROOT / "db/optional/storefront/wtt-armory-assort.json", True),
        ("WTT Backport optional", ROOT / "db/optional/storefront/content-backport-assort.json", True),
    ]
    if args.tgc_assort:
        sources.append(("TGC runtime", args.tgc_assort, False))

    quest_gates = dict(load(ROOT / "db/questassort.json").get("success", {}))
    quest_gates.update(load(ROOT / "db/CustomQuests/66bf757f27d0b097db0acea5/QuestAssort/Artem_QuestAssort.json").get("success", {}))
    quest_gates.update({
        "672e2804a0529208b4e10e18": "668aad3c3ff8f5b258e3a65b",
        "672e284a363b798192b802af": "668c18eb12542b3c3ff6e20f",
        "672e289bb4096716fcb918a7": "668c18eb12542b3c3ff6e20f",
    })
    rows: list[dict[str, object]] = []
    for source, path, optional in sources:
        assort = load(path)
        items = {str(row["_id"]): row for row in assort.get("items", [])}
        children: dict[str, list[dict]] = {}
        for item in items.values():
            children.setdefault(str(item.get("parentId", "")), []).append(item)

        def tree_value(item_id: str) -> tuple[int, int, int]:
            root_id = item_id
            stack = [items[item_id]]
            total = known = 0
            while stack:
                item = stack.pop()
                tpl = str(item["_tpl"])
                # A root StackObjectsCount is trader stock, not bundle quantity.
                count = 1 if str(item["_id"]) == root_id else int((item.get("upd") or {}).get("StackObjectsCount") or 1)
                if tpl in product_price:
                    total += product_price[tpl] * count
                    known += 1
                stack.extend(children.get(str(item["_id"]), []))
            return total, known, sum(1 for _ in walk(item_id, children))

        for offer in (row for row in items.values() if row.get("parentId") == "hideout"):
            offer_id = str(offer["_id"])
            schemes = assort.get("barter_scheme", {}).get(offer_id) or []
            requirements = schemes[0] if schemes else []
            cost = sum(requirement_price.get(str(req.get("_tpl")), 0) * int(req.get("count") or 0) for req in requirements)
            priced_reqs = sum(str(req.get("_tpl")) in requirement_price for req in requirements)
            value, priced_nodes, total_nodes = tree_value(offer_id)
            ratio = (cost / value) if value and priced_nodes == total_nodes and priced_reqs == len(requirements) else None
            upd = offer.get("upd") or {}
            route = "cash" if len(requirements) == 1 and str(requirements[0].get("_tpl")) == RUB else "barter"
            if offer_id in quest_gates:
                route += "+quest"
            flags = []
            if not requirements:
                flags.append("missing-payment")
            if priced_nodes < total_nodes:
                flags.append("partial-product-value")
            if priced_reqs < len(requirements):
                flags.append("unpriced-requirement")
            if ratio is not None and ratio < 0.35:
                flags.append("very-cheap")
            if ratio is not None and ratio > 3.0:
                flags.append("very-expensive")
            if upd.get("UnlimitedCount") is True and int(assort.get("loyal_level_items", {}).get(offer_id, 1)) < 4:
                flags.append("early-unlimited")
            tpl = str(offer["_tpl"])
            rows.append({
                "source": source,
                "optional": str(optional).lower(),
                "offerId": offer_id,
                "templateId": tpl,
                "name": locales.get(f"{tpl} Name") or locales.get(f"{tpl} ShortName") or tpl,
                "loyalty": int(assort.get("loyal_level_items", {}).get(offer_id, 1)),
                "stock": int(upd.get("StackObjectsCount") or 0),
                "buyLimit": "unlimited" if upd.get("UnlimitedCount") is True else int(upd.get("BuyRestrictionMax") or 0),
                "route": route,
                "questGate": str(quest_gates.get(offer_id) or ""),
                "costRub": cost if priced_reqs == len(requirements) else "",
                "productValueRub": value if priced_nodes == total_nodes else "",
                "costValueRatio": f"{ratio:.3f}" if ratio is not None else "",
                "pricedNodes": f"{priced_nodes}/{total_nodes}",
                "flags": ";".join(flags),
            })

    args.output.parent.mkdir(parents=True, exist_ok=True)
    fields = list(rows[0])
    with args.output.open("w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=fields)
        writer.writeheader()
        writer.writerows(sorted(rows, key=lambda row: (str(row["source"]), int(row["loyalty"]), str(row["offerId"]))))

    summary = Counter(str(row["source"]) for row in rows)
    flagged = Counter(flag for row in rows for flag in str(row["flags"]).split(";") if flag)
    print(json.dumps({"offers": len(rows), "sources": summary, "flags": flagged}, ensure_ascii=False, indent=2))


def walk(item_id: str, children: dict[str, list[dict]]):
    yield item_id
    for child in children.get(item_id, []):
        yield from walk(str(child["_id"]), children)


if __name__ == "__main__":
    main()
