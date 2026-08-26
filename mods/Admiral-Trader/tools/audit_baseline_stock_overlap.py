#!/usr/bin/env python3
"""Audit proposed Admiral baseline stock against other trader assort sources.

The tool intentionally does not choose prices or mutate assort data. It answers one
question only: where is a proposed root item already sold, and is that overlap
explicitly acknowledged by the authored proposal?
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8"))


def root_tpls_from_assort(data: Any) -> set[str]:
    if not isinstance(data, dict):
        raise ValueError("assort must be a JSON object")
    items = data.get("items")
    if not isinstance(items, list):
        raise ValueError("assort.items must be an array")
    result: set[str] = set()
    for item in items:
        if not isinstance(item, dict):
            continue
        if item.get("parentId") == "hideout" and isinstance(item.get("_tpl"), str):
            result.add(item["_tpl"])
    return result


def parse_provider(value: str) -> tuple[str, Path]:
    if "=" not in value:
        raise argparse.ArgumentTypeError("provider must be NAME=PATH")
    name, path = value.split("=", 1)
    name = name.strip()
    if not name:
        raise argparse.ArgumentTypeError("provider name cannot be empty")
    return name, Path(path)


def audit(proposal: dict[str, Any], providers: dict[str, set[str]]) -> dict[str, Any]:
    if proposal.get("schemaVersion") != 1:
        raise ValueError("baseline proposal schemaVersion must be 1")
    if proposal.get("stockClass") != "Baseline":
        raise ValueError("baseline proposal stockClass must be Baseline")
    offers = proposal.get("offers")
    if not isinstance(offers, list) or not offers:
        raise ValueError("baseline proposal must contain at least one offer")

    seen_tpls: set[str] = set()
    rows: list[dict[str, Any]] = []
    for offer in offers:
        if not isinstance(offer, dict):
            raise ValueError("baseline offer must be an object")
        tpl = offer.get("tpl")
        role = offer.get("role")
        rationale = offer.get("uniquenessRationale")
        overlap_policy = offer.get("overlapPolicy", "Documented")
        if not isinstance(tpl, str) or len(tpl) != 24:
            raise ValueError(f"invalid offer tpl: {tpl!r}")
        if tpl in seen_tpls:
            raise ValueError(f"duplicate baseline tpl: {tpl}")
        seen_tpls.add(tpl)
        if not isinstance(role, str) or not role.strip():
            raise ValueError(f"{tpl}: role is required")
        if overlap_policy not in {"Unique", "Documented"}:
            raise ValueError(f"{tpl}: overlapPolicy must be Unique or Documented")

        overlaps = sorted(name for name, tpls in providers.items() if tpl in tpls)
        if overlap_policy == "Unique" and overlaps:
            raise ValueError(f"{tpl}: declared Unique but overlaps providers {overlaps}")
        if overlaps and (not isinstance(rationale, str) or not rationale.strip()):
            raise ValueError(f"{tpl}: overlap requires uniquenessRationale")

        rows.append({
            "tpl": tpl,
            "role": role,
            "overlapPolicy": overlap_policy,
            "overlapProviders": overlaps,
            "directOverlapProviderCount": len(overlaps),
            "uniquenessRationale": rationale or "",
        })

    return {
        "schemaVersion": 1,
        "stockClass": "Baseline",
        "providerCount": len(providers),
        "providers": sorted(providers),
        "offerCount": len(rows),
        "offers": sorted(rows, key=lambda row: row["tpl"]),
        "summary": {
            "offersWithNoDirectOverlap": sum(not row["overlapProviders"] for row in rows),
            "offersWithDocumentedOverlap": sum(bool(row["overlapProviders"]) for row in rows),
            "maximumDirectOverlapProviderCount": max(row["directOverlapProviderCount"] for row in rows),
        },
    }


def main() -> int:
    parser = argparse.ArgumentParser(description="Audit Admiral baseline stock overlap against trader assorts")
    parser.add_argument("proposal", type=Path)
    parser.add_argument("--provider", action="append", default=[], type=parse_provider, metavar="NAME=PATH")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()

    provider_sets: dict[str, set[str]] = {}
    for name, path in args.provider:
        if name in provider_sets:
            raise SystemExit(f"duplicate provider name: {name}")
        provider_sets[name] = root_tpls_from_assort(load_json(path))
    if not provider_sets:
        raise SystemExit("at least one --provider NAME=PATH is required")

    result = audit(load_json(args.proposal), provider_sets)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps(result["summary"], sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
