#!/usr/bin/env python3
"""Build bounded Admiral offers from verified optional item packs."""
import argparse, hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUB = "5449016a4bdc2d6f028b456f"

WEAPONS = [
    ("WeaponCZ75.json", "6661012d16fbd2fb75408f87", "CZ 75B", 1, 18000),
    ("WeaponPMM12.json", "68d40fb07130ef271f60991f", "PMM-12", 1, 16000),
    ("WeaponUMP9.json", "67b05e25d83f07b7b587c0b5", "UMP9", 2, 52000),
    ("WeaponCarmel.json", "66ba249b102a9dd6040a6e7e", "IWI Carmel", 3, 105000),
]
GEAR = [
    ("68a6d95addf0111c2f04c9c3", "Construction helmet, orange", 1, 4500),
    ("693bb59250fafa102607aeb7", "M32 headset, white", 1, 32000),
    ("69e2441a18cb3157560855ec", "Spiritus Icebreaker rig", 2, 36000),
    ("69413241b1ce1e5fbb09ed0a", "CENS ProFlex DX5", 3, 98000),
]

def oid(seed): return hashlib.sha256(("admiral-optional-store:" + seed).encode()).hexdigest()[:24]
def empty(): return {"items": [], "barter_scheme": {}, "loyal_level_items": {}}
def add_offer(out, source_items, key, tpl, level, price):
    offer = oid(key)
    remap = {row["_id"]: (offer if i == 0 else oid(f"{key}:part:{i}")) for i, row in enumerate(source_items)}
    for i, source in enumerate(source_items):
        row = dict(source); row["_id"] = remap[source["_id"]]
        if source.get("parentId") in remap: row["parentId"] = remap[source["parentId"]]
        if i == 0:
            row.update({"parentId": "hideout", "slotId": "hideout"})
            upd = dict(row.get("upd", {})); upd.update({"UnlimitedCount": False, "StackObjectsCount": 3, "BuyRestrictionMax": 1, "BuyRestrictionCurrent": 0}); row["upd"] = upd
        out["items"].append(row)
    pay = oid(key + ":payment")
    out["barter_scheme"][offer] = [[{"count": price, "_tpl": RUB}]]
    out["loyal_level_items"][offer] = level
    return offer

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("--armory", type=Path, required=True); ap.add_argument("--backport", type=Path, required=True); a=ap.parse_args()
    target=ROOT/"db/optional/storefront"; target.mkdir(parents=True, exist_ok=True)
    arm=empty(); back=empty(); manifest={"schemaVersion":1,"status":"runtime-materialized","requiredDependencies":False,"validatedAgainst":"SPT 4.1.5","sources":[{"name":"WTT Armory","guid":"com.wtt.armory","observedVersion":"3.0.0","runtimeMetadataVersion":"2.0.5"},{"name":"WTT Content Backport","guid":"com.wtt.contentbackport","observedVersion":"2.0.1","runtimeMetadataVersion":"2.0.1"}],"selectionRule":"Only complete weapon presets and standalone equipment not already assigned to the source mod's traders; bounded stock, one purchase per reset.","offers":[]}
    for filename,tpl,name,level,price in WEAPONS:
        data=json.loads((a.armory/"db/CustomItems"/filename).read_text(encoding="utf-8-sig"))[tpl]
        preset=data["weaponPresets"][0]["_items"]
        offer=add_offer(arm,preset,"wtt:"+tpl,tpl,level,price)
        manifest["offers"].append({"offerId":offer,"tpl":tpl,"name":name,"source":"WTT Armory","loyaltyLevel":level,"priceRub":price,"completePreset":True})
    for tpl,name,level,price in GEAR:
        offer=add_offer(back,[{"_id":oid("source:"+tpl),"_tpl":tpl}],"backport:"+tpl,tpl,level,price)
        manifest["offers"].append({"offerId":offer,"tpl":tpl,"name":name,"source":"WTT Content Backport","loyaltyLevel":level,"priceRub":price,"completePreset":False})
    for name,payload in (("wtt-armory-assort.json",arm),("content-backport-assort.json",back)):
        (target/name).write_text(json.dumps(payload,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    manifest["offerCount"]=len(manifest["offers"]); manifest["absenceBehavior"]="No optional offer is published; all core quests and offers remain available."
    (ROOT/"manifests/optional-storefront-runtime.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")

if __name__ == "__main__": main()
