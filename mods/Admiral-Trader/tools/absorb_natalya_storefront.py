#!/usr/bin/env python3
import argparse, hashlib, json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def oid(seed): return hashlib.sha256(("admiral-natalya-full:"+seed).encode()).hexdigest()[:24]
def main():
 p=argparse.ArgumentParser(); p.add_argument("source",type=Path); p.add_argument("items",type=Path); p.add_argument("globals",type=Path); a=p.parse_args()
 src=json.loads((a.source/"db/assort.json").read_text(encoding="utf-8-sig")); valid=set(json.loads(a.items.read_text(encoding="utf-8-sig")))
 presets=json.loads(a.globals.read_text(encoding="utf-8-sig"))["ItemPresets"]
 weapon_roots={x["_tpl"] for v in presets.values() for x in v.get("_items",[]) if x.get("parentId") is None}
 old=json.loads((ROOT/"db/natalya-signature-assort.json").read_text()); preserved={x["_tpl"]:x["_id"] for x in old["items"] if x.get("parentId")=="hideout"}
 children={}
 for x in src["items"]: children.setdefault(x.get("parentId"),[]).append(x)
 out={"items":[],"barter_scheme":{},"loyal_level_items":{}}
 for root in [x for x in src["items"] if x.get("parentId")=="hideout"]:
  tree=[]; stack=[root]
  while stack:
   x=stack.pop(); tree.append(x); stack.extend(children.get(x["_id"],[]))
  if root["_tpl"] not in weapon_roots or not all(x["_tpl"] in valid for x in tree): continue
  offer=preserved.get(root["_tpl"],oid(root["_id"])); remap={x["_id"]:(offer if x is root else oid(root["_id"]+":"+x["_id"])) for x in tree}
  for x in tree:
   y=dict(x); y["_id"]=remap[x["_id"]]
   if x is root: y["parentId"]="hideout"; y["slotId"]="hideout"; y["upd"]={**y.get("upd",{}),"UnlimitedCount":False,"StackObjectsCount":2,"BuyRestrictionMax":1,"BuyRestrictionCurrent":0}
   elif x.get("parentId") in remap: y["parentId"]=remap[x["parentId"]]
   out["items"].append(y)
  out["barter_scheme"][offer]=src["barter_scheme"][root["_id"]]
  out["loyal_level_items"][offer]=src["loyal_level_items"][root["_id"]]
 roots=[x for x in out["items"] if x.get("parentId")=="hideout"]
 if len(roots)!=35: raise ValueError(f"expected 35 native weapon offers, got {len(roots)}")
 (ROOT/"db/natalya-signature-assort.json").write_text(json.dumps(out,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
 manifest_path=ROOT/"manifests/m7-natalya-absorption-program.json"; manifest=json.loads(manifest_path.read_text())
 manifest["signatureOffers"]=[{"offerId":x["_id"],"tpl":x["_tpl"],"loyaltyLevel":out["loyal_level_items"][x["_id"]],"stockPerReset":2,"buyRestriction":1,"priceRub":out["barter_scheme"][x["_id"]][0][0]["count"]} for x in roots]
 manifest_path.write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
if __name__=="__main__": main()
