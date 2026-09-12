#!/usr/bin/env python3
from __future__ import annotations
import argparse, hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRADER = "d5c27bb3169f8dfbc13f6b69"
RUB = "5449016a4bdc2d6f028b456f"
LOCATION_IDS = {
    "Ground Zero": ["Sandbox", "Sandbox_high"], "Customs": ["bigmap"],
    "Factory": ["factory4_day", "factory4_night"], "Woods": ["Woods"],
    "Shoreline": ["Shoreline"], "Interchange": ["Interchange"],
    "Reserve": ["RezervBase"], "Lighthouse": ["Lighthouse"],
    "Streets": ["TarkovStreets"], "The Lab": ["laboratory"],
}

def hid(text: str) -> str:
    return hashlib.sha256(("admiral-trader:" + text).encode()).hexdigest()[:24]

def start(level: int, previous: str | None):
    rows=[{"id":hid(f"level:{level}:{previous}"),"index":0,"compareMethod":">=","dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","value":level,"conditionType":"Level"}]
    if previous:
        rows.append({"id":hid(f"previous:{previous}"),"index":1,"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","target":previous,"status":[4],"availableAfter":0,"dispersion":0,"conditionType":"Quest"})
    return rows

def counter(qid: str, value: int, conditions: list[dict], qtype="Elimination", one=False):
    return {"id":hid(qid+":finish"),"index":0,"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","value":value,"type":qtype,"oneSessionOnly":one,"isResetOnConditionFailed":False,"isNecessary":False,"doNotResetIfCounterCompleted":False,"counter":{"id":hid(qid+":counter"),"conditions":conditions},"completeInSeconds":0,"conditionType":"CounterCreator"}

def kill(qid, weapons, locations, target="Any"):
    rows=[{"id":hid(qid+":kill"),"dynamicLocale":False,"target":target,"compareMethod":">=","value":1,"weapon":weapons,"distance":{"value":0,"compareMethod":">="},"weaponModsInclusive":[],"weaponModsExclusive":[],"enemyEquipmentInclusive":[],"enemyEquipmentExclusive":[],"weaponCaliber":[],"savageRole":[],"bodyPart":[],"daytime":{"from":0,"to":0},"conditionType":"Kills","enemyHealthEffects":[],"resetOnSessionEnd":False}]
    if locations: rows.append({"id":hid(qid+":location"),"dynamicLocale":False,"conditionType":"Location","target":locations})
    return rows

def reward(qid, level):
    xp=2500+level*450; rub=10000+level*1800; standing=round(min(.005+level*.0005,.025),3)
    item=hid(qid+":rub")
    return [{"value":xp,"id":hid(qid+":xp"),"type":"Experience","index":0},{"value":standing,"id":hid(qid+":rep"),"type":"TraderStanding","target":TRADER,"index":1},{"value":rub,"id":hid(qid+":money"),"type":"Item","target":item,"index":2,"items":[{"_id":item,"_tpl":RUB,"upd":{"StackObjectsCount":rub}}]}]

def quest(slug,name,level,previous,finish,qtype="Elimination"):
    qid=hid("quest:"+slug)
    return qid,{"QuestName":name,"_id":qid,"canShowNotificationsInGame":True,"conditions":{"AvailableForFinish":[finish(qid)],"AvailableForStart":start(level,previous),"Fail":[]},"description":qid+" description","failMessageText":qid+" failMessageText","name":qid+" name","note":qid+" note","traderId":TRADER,"location":"any","image":"/files/quest/icon/5a27cafa86f77424e20615d6.jpg","type":qtype,"isKey":False,"restartable":False,"instantComplete":False,"secretQuest":False,"startedMessageText":qid+" startedMessageText","successMessageText":qid+" successMessageText","acceptPlayerMessage":qid+" acceptPlayerMessage","acceptanceAndFinishingSource":"eft","declinePlayerMessage":qid+" declinePlayerMessage","completePlayerMessage":qid+" completePlayerMessage","changeQuestMessageText":qid+" changeQuestMessageText","rewards":{"Started":[],"Success":reward(qid,level),"Fail":[]},"side":"Pmc","status":0,"progressSource":"eft","gameModes":[],"rankingModes":[],"arenaLocations":[]}

def loc_condition(qid, locations, equipment=None):
    rows=[]
    if equipment: rows.append({"id":hid(qid+":gear"),"dynamicLocale":False,"conditionType":"Equipment","equipmentInclusive":[equipment],"equipmentExclusive":[],"IncludeNotEquippedItems":False})
    rows += [{"id":hid(qid+":loc"),"dynamicLocale":False,"conditionType":"Location","target":locations},{"id":hid(qid+":exit"),"dynamicLocale":False,"conditionType":"ExitStatus","status":["Survived"]}]
    return rows

def locale(qid,name,requirements,level,objective,ru=False):
    intro=("Адмирал формирует долгую программу полевых испытаний." if ru else "Admiral is building a long field qualification program.")
    req=("Требования" if ru else "Requirements")+":\n- "+requirements
    xp=2500+level*450; rub=10000+level*1800; standing=round(min(.005+level*.0005,.025),3)
    rew=(f"Награды:\n- {xp} XP, +{standing:.3f} репутации Адмирала, ₽{rub}." if ru else f"Rewards:\n- {xp} XP, +{standing:.3f} Admiral standing, ₽{rub}.")
    done=("Задача выполнена. Результат принят." if ru else "Assignment complete. The result is accepted.")
    return {qid+" name":name,qid+" description":f"{intro}\n\n{req}\n\n{rew}",qid+" note":"",qid+" startedMessageText":f"{intro}\n\n{req}",qid+" successMessageText":f"{done}\n\n{rew}",qid+" failMessageText":"",qid+" acceptPlayerMessage":f"{intro}\n\n{req}",qid+" declinePlayerMessage":"",qid+" completePlayerMessage":done,qid+" changeQuestMessageText":"",hid(qid+":finish"):objective}

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--spt-root",type=Path)
    args=parser.parse_args()
    names={"en":{},"ru":{}}
    if args.spt_root:
        for lang in names:
            path=args.spt_root/"SPT_Data/database/locales/global"/f"{lang}.json"
            raw=json.loads(path.read_text(encoding="utf-8-sig"))
            names[lang]={k[:-5]:v for k,v in raw.items() if k.endswith(" Name")}
    def item_list(ids,lang): return ", ".join(names[lang].get(x,x) for x in ids)
    plan=json.loads((ROOT/"manifests/weapon-rotation-expansion-plan.json").read_text())
    optional_path=ROOT/"manifests/optional-weapon-runtime.json"
    optional=json.loads(optional_path.read_text(encoding="utf-8")) if optional_path.exists() else {"acceptedWeapons":[]}
    optional_by_pool={}
    for row in optional["acceptedWeapons"]: optional_by_pool.setdefault(row["pool"],[]).append(row)
    out=[]; meta=[]; en={}; ru={}
    # Two independent weapon lanes: exactly two weapon assignments can be active.
    selected=plan["lanes"]["A-close-support"][:10]+plan["lanes"]["B-rifle-precision"][:9]
    previous={"A":None,"B":None}
    for lane,row in [("A",x) for x in plan["lanes"]["A-close-support"][:10]]+[("B",x) for x in plan["lanes"]["B-rifle-precision"][:9]]:
        order,pool,band,locations,semantics=row; level=int(band.split('-')[0]); native_weapons=plan["pools"][pool]; optional_weapons=optional_by_pool.get(pool,[]); weapons=native_weapons+[x["tpl"] for x in optional_weapons]; runtime_locations=[x for label in locations for x in LOCATION_IDS[label]]; slug=f"rotation-{lane.lower()}-{order:02d}-{pool}"; name=f"Arsenal Rotation {lane}-{order}: {pool.replace('-',' ').title()}"
        qid,q=quest(slug,name,level,previous[lane],lambda qid,w=weapons,l=runtime_locations,v=min(6+order,15):counter(qid,v,kill(qid,w,l)),"Elimination")
        previous[lane]=qid;out.append((qid,q));meta.append({"id":qid,"kind":"weapon","lane":lane,"order":order,"pool":pool})
        optional_names=", ".join(x["name"] for x in optional_weapons)
        optional_names_ru=", ".join(x["nameRu"] for x in optional_weapons)
        allowed_en=item_list(native_weapons,'en')+(f"; optional WTT: {optional_names}" if optional_names else "")
        allowed_ru=item_list(native_weapons,'ru')+(f"; опционально WTT: {optional_names_ru}" if optional_names_ru else "")
        req=f"Eliminate {min(6+order,15)} targets on {', '.join(locations)}. Allowed weapons: [{allowed_en}]. Progress carries across raids; FIR does not apply."
        en.update(locale(qid,name,req,level,f"Eliminate {min(6+order,15)} targets with the allowed weapon pool"));ru.update(locale(qid,name,f"Устранить {min(6+order,15)} целей на картах: {', '.join(locations)}. Разрешённое оружие: [{allowed_ru}]. Прогресс сохраняется между рейдами; FIR не применяется.",level,f"Устранить {min(6+order,15)} целей разрешённым оружием",True))
    # Ground Zero opening chain.
    prev=None
    gz=[("ground-zero-arrival","Operation: First Contact",1,2,"Savage"),("ground-zero-corridor","Operation: Open Corridor",3,4,"Savage"),("ground-zero-pressure","Operation: Contested Ground",5,2,"AnyPmc"),("ground-zero-exit","Operation: Exit Discipline",7,5,"Any")]
    for slug,name,level,count,target in gz:
        qid,q=quest(slug,name,level,prev,lambda qid,c=count,t=target:counter(qid,c,kill(qid,[],LOCATION_IDS["Ground Zero"],t)),"Elimination");prev=qid;out.append((qid,q));meta.append({"id":qid,"kind":"operation","location":"Ground Zero"})
        req=f"Eliminate {count} {target} targets on Ground Zero. Progress carries across raids."
        en.update(locale(qid,name,req,level,f"Eliminate {count} targets on Ground Zero"));ru.update(locale(qid,name,f"Устранить {count} целей типа {target} на Эпицентре. Прогресс сохраняется между рейдами.",level,f"Устранить {count} целей на Эпицентре",True))
    # A staged non-weapon equipment chain.
    gear=[("light-rig","Loadout: Low Signature",6,"Customs",["5c0e722886f7740458316a57","5e4abc1f86f774069619fbaa"]),("field-headset","Loadout: Acoustic Cover",11,"Woods",["5b432b965acfc47a8774094e","5e4d34ca86f774264f758330"]),("service-helmet","Loadout: Head Protection",16,"Shoreline",["5c06c6a80db834001b735491","5aa7cfc0e5b5b00015693143"]),("medium-armor","Loadout: Mobile Armor",21,"Interchange",["5c0e655586f774045612eeb2","5c0e625a86f7742d77340f62"]),("cargo-rig","Loadout: Sustainment",26,"Reserve",["5df8a42886f77412640e2e75","5c0e9f2c86f77432297fe0a3"]),("heavy-kit","Loadout: Breach Weight",31,"Factory",["5ca2151486f774244a3b8d30","5ca21c6986f77479963115a7"])]
    prev=None
    for slug,name,level,location,items in gear:
        qid,q=quest(slug,name,level,prev,lambda qid,l=location,i=items:counter(qid,1,loc_condition(qid,LOCATION_IDS[l],i),"Exploration",True),"Exploration");prev=qid;out.append((qid,q));meta.append({"id":qid,"kind":"equipment","location":location})
        req=f"Enter a raid on {location} wearing one item from [{item_list(items,'en')}], then survive and extract in the same raid. FIR does not apply."
        en.update(locale(qid,name,req,level,f"Use the allowed equipment and survive {location}"));ru.update(locale(qid,name,f"Выйти в рейд на {location} с одним предметом из [{item_list(items,'ru')}], выжить и эвакуироваться в том же рейде. FIR не применяется.",level,f"Использовать разрешённый комплект и выжить на {location}",True))
    qdir=ROOT/"db/quests"
    for p in qdir.glob("40-*.json"): p.unlink()
    for p in qdir.glob("50-*.json"): p.unlink()
    for i,(qid,q) in enumerate(out):
        prefix="40" if meta[i]["kind"]=="weapon" else "50"
        (qdir/f"{prefix}-{i+1:02d}-{qid}.json").write_text(json.dumps(q,separators=(",",":"),ensure_ascii=False)+"\n",encoding="utf-8")
    (ROOT/"db/locales/m8-en.json").write_text(json.dumps(en,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    (ROOT/"db/locales/m8-ru.json").write_text(json.dumps(ru,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    manifest={"schemaVersion":1,"status":"runtime-materialized","totalQuestCount":72,"newQuestCount":29,"weaponAssignments":19,"groundZeroOperations":4,"equipmentAssignments":6,"maximumConcurrentWeaponAssignments":2,"optionalWeaponCount":len(optional["acceptedWeapons"]),"optionalWeaponsRequired":False,"icebreaker":{"reserved":True,"runtimePublished":False,"reason":"map not installed; no speculative IDs"},"quests":meta}
    (ROOT/"manifests/m8-campaign-expansion-runtime.json").write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    first=qdir/"01-5d404ebd654de4efecef71d2.json"
    base=json.loads(first.read_text(encoding="utf-8"))
    if not any(c.get("conditionType")=="Quest" and c.get("target")==out[19][0] for c in base["conditions"]["AvailableForStart"]):
        base["conditions"]["AvailableForStart"].append({"id":hid("foundation-after-ground-zero"),"index":1,"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","target":out[19][0],"status":[4],"availableAfter":0,"dispersion":0,"conditionType":"Quest"})
    first.write_text(json.dumps(base,separators=(",",":"),ensure_ascii=False)+"\n",encoding="utf-8")
    print(json.dumps({"generated":len(out),"total":72}))
if __name__=="__main__": main()
