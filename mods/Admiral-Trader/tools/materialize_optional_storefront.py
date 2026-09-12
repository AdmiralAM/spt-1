#!/usr/bin/env python3
"""Build bounded Admiral offers from verified optional item packs."""
import argparse, hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUB = "5449016a4bdc2d6f028b456f"

WEAPON_FILES = ["Weapon92fs.json","WeaponAEK.json","WeaponAK5C.json","WeaponAN94.json","WeaponAuto5.json","WeaponCarmel.json","WeaponCZ75.json","WeaponF2000.json","WeaponHK417.json","WeaponM1894.json","WeaponM249.json","WeaponMK23.json","WeaponMSBS.json","WeaponPMM12.json","WeaponProdigy.json","WeaponStaccatoXC.json","WeaponSV98M.json","WeaponUMP9.json","WeaponUSC.json","WeaponX95.json"]
SUPPORT_FILES = ["Ammo.json","Attachment_Foregrips.json","Attachment_IronSights.json","Attachment_Magazines.json","Attachment_Muzzles.json","Attachment_PistolGrips.json","Attachment_Scopes.json","Attachment_Suppressors.json"]
GEAR_FILES = ["Headphones_config.json","Headphones_config_2.json","Headwear_config.json","Headwear_config_2.json","SimpleContainer_config_2.json","Vest_config.json","Vest_config_2.json","vest_config.json"]

def oid(seed): return hashlib.sha256(("admiral-optional-store:" + seed).encode()).hexdigest()[:24]
def empty(): return {"items": [], "barter_scheme": {}, "loyal_level_items": {}}
def add_offer(out, source_items, key, tpl, level, price, stock=3, restriction=1):
    offer = oid(key)
    remap = {row["_id"]: (offer if i == 0 else oid(f"{key}:part:{i}")) for i, row in enumerate(source_items)}
    for i, source in enumerate(source_items):
        row = dict(source); row["_id"] = remap[source["_id"]]
        if source.get("parentId") in remap: row["parentId"] = remap[source["parentId"]]
        if i == 0:
            row.update({"parentId": "hideout", "slotId": "hideout"})
            upd = dict(row.get("upd", {})); upd.update({"UnlimitedCount": False, "StackObjectsCount": stock, "BuyRestrictionMax": restriction, "BuyRestrictionCurrent": 0}); row["upd"] = upd
        out["items"].append(row)
    pay = oid(key + ":payment")
    out["barter_scheme"][offer] = [[{"count": price, "_tpl": RUB}]]
    out["loyal_level_items"][offer] = level
    return offer

def main():
    ap=argparse.ArgumentParser(); ap.add_argument("--armory", type=Path, required=True); ap.add_argument("--backport", type=Path, required=True); a=ap.parse_args()
    target=ROOT/"db/optional/storefront"; target.mkdir(parents=True, exist_ok=True)
    arm=empty(); back=empty(); manifest={"schemaVersion":1,"status":"runtime-materialized","requiredDependencies":False,"validatedAgainst":"SPT 4.1.5","sources":[{"name":"WTT Armory","guid":"com.wtt.armory","observedVersion":"3.0.0","runtimeMetadataVersion":"2.0.5"},{"name":"WTT Content Backport","guid":"com.wtt.contentbackport","observedVersion":"2.0.1","runtimeMetadataVersion":"2.0.1"}],"selectionRule":"Only complete weapon presets and standalone equipment not already assigned to the source mod's traders; bounded stock, one purchase per reset.","offers":[]}
    armory_items=a.armory/"db/CustomItems"
    for filename in WEAPON_FILES:
        data=json.loads((armory_items/filename).read_text(encoding="utf-8-sig"))
        candidates=[(tpl,row) for tpl,row in data.items() if row.get("addWeaponPreset") and row.get("weaponPresets")]
        if not candidates: raise ValueError(f"No preset in {filename}")
        tpl,row=sorted(candidates)[0]; name=row.get("masterySections",[{}])[0].get("Name",tpl); base=int(row["handbookPriceRoubles"])
        level=1 if base<12000 else 2 if base<50000 else 3 if base<100000 else 4; price=max(16000,int(base*1.55))
        offer=add_offer(arm,row["weaponPresets"][0]["_items"],"wtt:"+tpl,tpl,level,price)
        manifest["offers"].append({"offerId":offer,"tpl":tpl,"name":name,"category":"complete weapon","source":"WTT Armory","loyaltyLevel":level,"priceRub":price,"completePreset":True})
    support_seen=set()
    for filename in SUPPORT_FILES:
        data=json.loads((armory_items/filename).read_text(encoding="utf-8-sig"))
        selected=[]
        for tpl,row in sorted(data.items()):
            price=int(row.get("handbookPriceRoubles",0)); props=row.get("overrideProperties",{})
            if tpl in support_seen or row.get("addtoTraders") or price<500 or price>120000 or props.get("QuestItem"): continue
            selected.append((tpl,row,price))
        for tpl,row,base in selected[:4]:
            support_seen.add(tpl)
            ammo=filename=="Ammo.json"; level=1 if base<8000 else 2 if base<25000 else 3
            price=max(800 if ammo else 1500,int(base*1.35)); stock,restriction=((120,60) if ammo else (8,2))
            offer=add_offer(arm,[{"_id":oid("source:"+tpl),"_tpl":tpl}],"support:"+tpl,tpl,level,price,stock,restriction)
            manifest["offers"].append({"offerId":offer,"tpl":tpl,"name":row.get("overrideProperties",{}).get("Name",tpl),"category":"ammunition" if ammo else "weapon support","source":"WTT Armory","loyaltyLevel":level,"priceRub":price,"completePreset":False})
    backport_items=a.backport/"db/CustomItems"
    gear_seen=set()
    for filename in GEAR_FILES:
        paths=sorted(backport_items.rglob(filename))
        if not paths: continue
        data=json.loads(paths[0].read_text(encoding="utf-8-sig")); selected=[]
        for tpl,row in sorted(data.items()):
            price=int(row.get("handbookPriceRoubles",0)); props=row.get("overrideProperties",{})
            required=any(slot.get("_required") or slot.get("_props",{}).get("_required") for slot in props.get("Slots",[]) if isinstance(slot,dict))
            if tpl in gear_seen or row.get("addtoTraders") or price<400 or price>120000 or props.get("QuestItem") or required: continue
            selected.append((tpl,row,price))
        for tpl,row,base in selected[:3]:
            gear_seen.add(tpl)
            level=1 if base<30000 else 2 if base<70000 else 3; price=max(3000,int(base*1.3))
            offer=add_offer(back,[{"_id":oid("source:"+tpl),"_tpl":tpl}],"backport:"+tpl,tpl,level,price,5,1)
            manifest["offers"].append({"offerId":offer,"tpl":tpl,"name":row.get("overrideProperties",{}).get("Name",tpl),"category":"equipment","source":"WTT Content Backport","loyaltyLevel":level,"priceRub":price,"completePreset":False})
    for name,payload in (("wtt-armory-assort.json",arm),("content-backport-assort.json",back)):
        (target/name).write_text(json.dumps(payload,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    manifest["offerCount"]=len(manifest["offers"]); manifest["totalAdmiralOffersWhenPresent"]=51+manifest["offerCount"]; manifest["absenceBehavior"]="No optional offer is published; all core quests and offers remain available."
    (ROOT/"manifests/optional-storefront-runtime.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")

if __name__ == "__main__": main()
