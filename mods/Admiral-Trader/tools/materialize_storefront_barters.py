#!/usr/bin/env python3
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    policy = json.loads((ROOT / "manifests/storefront-barter-policy.json").read_text(encoding="utf-8"))
    path = ROOT / "db/assort.json"
    assort = json.loads(path.read_text(encoding="utf-8"))
    roots = {row["_id"] for row in assort["items"] if row.get("parentId") == "hideout"}
    for offer in policy["offers"]:
        offer_id = offer["offerId"]
        if offer_id not in roots:
            raise ValueError(f"barter policy references unknown root offer: {offer_id}")
        assort["barter_scheme"][offer_id] = [[
            {"count": int(row["count"]), "_tpl": row["tpl"]}
            for row in offer["requirements"]
        ]]
    path.write_text(json.dumps(assort, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
