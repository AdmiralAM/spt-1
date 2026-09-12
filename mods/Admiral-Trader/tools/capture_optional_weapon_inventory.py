#!/usr/bin/env python3
"""Capture verified optional WTT weapons without making them dependencies."""
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WEAPON_CATEGORIES = {"AssaultCarbine", "AssaultRifle", "MarksmanRifle", "SniperRifle"}
DEFERRED_NAMES = {
    "Bofors Ak5G 5.56x45 light machine gun": "mechanical role differs from its cloned rifle template",
    "Valmet M78 7.62x51 light machine gun": "mechanical role differs from its cloned AK template",
    "Colt RO991 9x19 submachine gun": "mechanical role differs from its cloned rifle template",
    "IWI Tavor X95 9x19 submachine gun": "mechanical role differs from its cloned rifle template",
}

def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def entries(folder: Path, accepted_file) -> list[dict]:
    result=[]
    for path in sorted(folder.glob("*.json")):
        if not accepted_file(path): continue
        data=json.loads(path.read_text(encoding="utf-8-sig"))
        for tpl,value in data.items():
            if not isinstance(value,dict) or not value.get("itemTplToClone"): continue
            locales=value.get("locales",{})
            locale=locales.get("en",{})
            locale_ru=locales.get("ru",{})
            name=locale.get("name") or value.get("name") or path.stem
            result.append({"tpl":tpl,"cloneTpl":value["itemTplToClone"],"name":name,"nameRu":locale_ru.get("name") or name,"sourceFile":path.name})
    return result

def main() -> None:
    parser=argparse.ArgumentParser()
    parser.add_argument("--spt-root",type=Path,required=True)
    args=parser.parse_args()
    mods=args.spt_root/"user"/"mods"
    sources=[
        ("WTT Armory","com.wtt.armory","2.0.5",mods/"WTT-Armory",lambda p:p.name.startswith("Weapon")),
        ("WTT Content Backport","com.wtt.contentbackport","2.0.1",mods/"WTT-ContentBackport",lambda p:p.stem.split("_")[0] in WEAPON_CATEGORIES),
    ]
    plan=json.loads((ROOT/"manifests/weapon-rotation-expansion-plan.json").read_text(encoding="utf-8"))
    selected=plan["lanes"]["A-close-support"][:10]+plan["lanes"]["B-rifle-precision"][:9]
    clone_to_pool={tpl:row[1] for row in selected for tpl in plan["pools"][row[1]]}
    accepted=[];deferred=[];provenance=[]
    for name,guid,version,folder,accepted_file in sources:
        dll=next(folder.glob("*.dll"))
        provenance.append({"name":name,"guid":guid,"observedVersion":version,"dllSha256":sha256(dll)})
        for row in entries(folder/"db"/"CustomItems",accepted_file):
            if row["cloneTpl"] not in clone_to_pool: continue
            row.update({"source":name,"pool":clone_to_pool[row["cloneTpl"]]})
            if row["name"] in DEFERRED_NAMES:
                row["reason"]=DEFERRED_NAMES[row["name"]];deferred.append(row)
            else: accepted.append(row)
    output={"schemaVersion":1,"status":"verified-optional-runtime-input","requiredDependency":False,"absenceBehavior":"Every assignment retains a native SPT weapon pool and the campaign graph remains complete.","sources":provenance,"acceptedWeapons":sorted(accepted,key=lambda x:(x["pool"],x["source"],x["tpl"])),"deferredWeapons":sorted(deferred,key=lambda x:x["tpl"])}
    destination=ROOT/"manifests/optional-weapon-runtime.json"
    destination.write_text(json.dumps(output,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    print(json.dumps({"accepted":len(accepted),"deferred":len(deferred),"output":str(destination)}))

if __name__=="__main__": main()
