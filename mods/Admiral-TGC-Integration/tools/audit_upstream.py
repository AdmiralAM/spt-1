#!/usr/bin/env python3
"""Fail-closed audit of the official extracted TGC 3.0.0 runtime."""

from __future__ import annotations

import argparse
import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SPEC = json.loads((ROOT / "manifests/upstream-tgc-3.0.0.json").read_text(encoding="utf-8"))


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def audit(runtime: Path) -> dict:
    db = runtime / "db"
    items_path = db / "CustomItems/modTGC_items.json"
    clothes_path = db / "modTGC_clothes.json"
    assort_path = db / f"traders/{SPEC['upstreamTraderId']}/assort.json"
    suits_path = db / f"traders/{SPEC['upstreamTraderId']}/suits.json"
    bundles_path = runtime / "bundles.json"
    required = [items_path, clothes_path, assort_path, suits_path, bundles_path]
    missing = [str(path.relative_to(runtime)) for path in required if not path.is_file()]
    if missing:
        raise ValueError(f"missing required upstream files: {missing}")

    items = load(items_path)
    clothes = load(clothes_path)
    assort = load(assort_path)
    suits = load(suits_path)
    bundle_manifest = load(bundles_path)
    root_offers = [row for row in assort["items"] if row.get("parentId") == "hideout"]

    expected = SPEC["expected"]
    count_actual = {
        "customItemCount": len(items),
        "rootOfferCount": len(root_offers),
        "barterSchemeCount": len(assort["barter_scheme"]),
        "loyaltyOfferCount": len(assort["loyal_level_items"]),
        "suitCount": len(suits),
    }

    ids = set(items)
    clone_ids = {record["itemTplToClone"] for record in items.values()}
    belt_policy_ids = sorted(
        item_id
        for item_id, record in items.items()
        if record.get("putInArmband") is True or record.get("putInSecureContainer") is True
    )
    offer_ids = {row["_id"] for row in root_offers}
    if offer_ids != set(assort["barter_scheme"]) or offer_ids != set(assort["loyal_level_items"]):
        raise ValueError("root offer/barter/loyalty key sets differ")

    manifest = bundle_manifest.get("manifest", bundle_manifest)
    bundle_paths = []
    if isinstance(manifest, dict):
        bundle_paths = [value.get("path") or value.get("key") for value in manifest.values() if isinstance(value, dict)]
    elif isinstance(manifest, list):
        bundle_paths = [value.get("path") or value.get("key") for value in manifest if isinstance(value, dict)]
    bundle_paths = [path for path in bundle_paths if path]
    missing_bundles = [path for path in bundle_paths if not (runtime / "bundles" / path).is_file()]
    if missing_bundles:
        raise ValueError(f"missing declared bundles: {missing_bundles[:10]}")
    actual = {**count_actual, "declaredBundleCount": len(bundle_paths)}
    if actual != expected:
        raise ValueError(f"upstream content drift: expected={expected}, actual={actual}")

    return {
        "status": "pass",
        "counts": actual,
        "clothingDefinitionCount": len(clothes),
        "customItemIds": sorted(ids),
        "cloneTemplateIds": sorted(clone_ids),
        "beltPolicyItemIds": belt_policy_ids,
    }


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("runtime", type=Path, help="Extracted TGC-NG runtime folder")
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    result = audit(args.runtime.resolve())
    rendered = json.dumps(result, indent=2, ensure_ascii=False) + "\n"
    if args.output:
        args.output.write_text(rendered, encoding="utf-8")
    print(rendered, end="")


if __name__ == "__main__":
    main()
