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

POOL_NAMES_RU = {
    "service-pistols": "Служебные пистолеты",
    "compact-ak": "Укороченные автоматы Калашникова",
    "manual-shotguns": "Помповые и магазинные дробовики",
    "early-smg": "Ранние пистолеты-пулемёты",
    "forty-five-pistols": "Пистолеты .45 ACP",
    "service-smg": "Служебные пистолеты-пулемёты",
    "automatic-pistols": "Автоматические пистолеты",
    "compact-pdw": "Компактное оружие самообороны",
    "autoloading-shotguns": "Самозарядные дробовики",
    "modern-smg": "Современные пистолеты-пулемёты",
    "starter-service-rifles": "Стартовые служебные автоматы",
    "civilian-ak": "Гражданские карабины Калашникова",
    "compact-rifles": "Компактные автоматы",
    "sks-hunter": "Самозарядные карабины",
    "classic-ak-545": "Классические автоматы 5,45",
    "classic-ak-762": "Классические автоматы 7,62",
    "nato-service-rifles": "Служебные винтовки НАТО",
    "nato-alternatives": "Альтернативные винтовки НАТО",
    "nine-by-thirty-nine": "Специальные системы 9×39",
}

LEGACY_WEAPON_LANES = {
    "A": [
        (23, "b016df9d2bea4269cc59d531"), (25, "43d9544a09d068476a1a18df"),
        (27, "8d8d81032315f4fdc5a06798"), (29, "8cba3e2ec639a4aa2c26c4da"),
        (31, "59ca4829e098dfafa03888d2"), (33, "cb8a202d7107f39d860ccb38"),
        (35, "73febe7f3f61ca0913410ffc"), (37, "f1368cb3b69c3a4917c4f206"),
        (40, "88118e994f26cab3bee1521d"), (40, "5f62a924076e4b7c2320f2e8"),
    ],
    "B": [
        (21, "7564e60e4c1c2f1b67a594a4"), (23, "2568ee0bfe2ee12f24d78f45"),
        (25, "33810921ad5c893b866b3951"), (27, "a0d05e28971f1ba57639b97d"),
        (29, "153839f368b80b6fbc36d29e"), (31, "cd2641c70bede98dac3945d0"),
        (33, "f6e51dc4e50e47ee9af50a4d"), (35, "4ada822d634041a721b346d5"),
        (37, "570d250679328757614dcbcb"), (40, "ffb63228a333c8b0755741ea"),
        (40, "ad9233f54a7132d905d6f29d"),
    ],
}

def hid(text: str) -> str:
    return hashlib.sha256(("admiral-trader:" + text).encode()).hexdigest()[:24]

def start(level: int, previous: str | None):
    rows=[{"id":hid(f"level:{level}:{previous}"),"index":0,"compareMethod":">=","dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","value":level,"conditionType":"Level"}]
    if previous:
        rows.append({"id":hid(f"previous:{previous}"),"index":1,"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","target":previous,"status":[4],"availableAfter":0,"dispersion":0,"conditionType":"Quest"})
    return rows

def replace_prerequisite(quest: dict, previous: str | None):
    rows = [row for row in quest["conditions"]["AvailableForStart"] if row["conditionType"] != "Quest"]
    if previous:
        rows.append({"id":hid(f"previous:{previous}"),"index":len(rows),"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","target":previous,"status":[4],"availableAfter":0,"dispersion":0,"conditionType":"Quest"})
    quest["conditions"]["AvailableForStart"] = rows

def counter(qid: str, value: int, conditions: list[dict], qtype="Elimination", one=False):
    for condition in conditions:
        if condition.get("conditionType") == "Kills":
            condition["resetOnSessionEnd"] = one
    return {"id":hid(qid+":finish"),"index":0,"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","value":value,"type":qtype,"oneSessionOnly":one,"isResetOnConditionFailed":False,"isNecessary":False,"doNotResetIfCounterCompleted":False,"counter":{"id":hid(qid+":counter"),"conditions":conditions},"completeInSeconds":0,"conditionType":"CounterCreator"}

def kill(qid, weapons, locations, target="Any", distance=0):
    rows=[{"id":hid(qid+":kill"),"dynamicLocale":False,"target":target,"compareMethod":">=","value":1,"weapon":weapons,"distance":{"value":distance,"compareMethod":">="},"weaponModsInclusive":[],"weaponModsExclusive":[],"enemyEquipmentInclusive":[],"enemyEquipmentExclusive":[],"weaponCaliber":[],"savageRole":[],"bodyPart":[],"daytime":{"from":0,"to":0},"conditionType":"Kills","enemyHealthEffects":[],"resetOnSessionEnd":False}]
    if locations: rows.append({"id":hid(qid+":location"),"dynamicLocale":False,"conditionType":"Location","target":locations})
    return rows

def reward(qid, level):
    xp=2500+level*450; rub=10000+level*1800; standing=round(min(.005+level*.0005,.025),3)
    item=hid(qid+":rub")
    return [{"value":xp,"id":hid(qid+":xp"),"type":"Experience","index":0},{"value":standing,"id":hid(qid+":rep"),"type":"TraderStanding","target":TRADER,"index":1},{"value":rub,"id":hid(qid+":money"),"type":"Item","target":item,"index":2,"items":[{"_id":item,"_tpl":RUB,"upd":{"StackObjectsCount":rub}}]}]

def quest(slug,name,level,previous,finish,qtype="Elimination"):
    qid=hid("quest:"+slug)
    return qid,{"QuestName":name,"_id":qid,"canShowNotificationsInGame":True,"conditions":{"AvailableForFinish":[finish(qid)],"AvailableForStart":start(level,previous),"Fail":[]},"description":qid+" description","failMessageText":qid+" failMessageText","name":qid+" name","note":qid+" note","traderId":TRADER,"location":"any","image":"/files/quest/icon/5a27cafa86f77424e20615d6.jpg","type":qtype,"isKey":False,"restartable":False,"instantComplete":False,"secretQuest":False,"startedMessageText":qid+" startedMessageText","successMessageText":qid+" successMessageText","acceptPlayerMessage":qid+" acceptPlayerMessage","acceptanceAndFinishingSource":"eft","declinePlayerMessage":qid+" declinePlayerMessage","completePlayerMessage":qid+" completePlayerMessage","changeQuestMessageText":qid+" changeQuestMessageText","rewards":{"Started":[],"Success":reward(qid,level),"Fail":[]},"side":"Pmc","status":0,"progressSource":"eft","gameModes":[],"rankingModes":[],"arenaLocations":[]}

def loc_condition(qid, locations, equipment_groups=None, kill_target=None, kill_condition_id=None):
    rows=[]
    if equipment_groups: rows.append({"id":hid(qid+":gear"),"dynamicLocale":False,"conditionType":"Equipment","equipmentInclusive":equipment_groups,"equipmentExclusive":[],"IncludeNotEquippedItems":False})
    if kill_target:
        kill_rows = kill(qid, [], [], kill_target)
        if kill_condition_id: kill_rows[0]["id"] = kill_condition_id
        rows += kill_rows
    rows += [{"id":hid(qid+":loc"),"dynamicLocale":False,"conditionType":"Location","target":locations},{"id":hid(qid+":exit"),"dynamicLocale":False,"conditionType":"ExitStatus","status":["Survived"]}]
    return rows

def locale(qid,name,detail,level,objective,ru=False):
    intro=("Адмирал формирует долгую программу полевых испытаний." if ru else "Admiral is building a long field qualification program.")
    # The native panels already show the task count, locations and rewards.
    # Keep only information that the compact objective row cannot make clear:
    # the concrete eligible equipment or weapon pool.
    details = ("Уточнение:\n" if ru else "Operational detail:\n") + detail if detail else ""
    body = intro + (("\n\n" + details) if details else "")
    done=("Задача выполнена. Результат принят." if ru else "Assignment complete. The result is accepted.")
    return {qid+" name":name,qid+" description":body,qid+" note":"",qid+" startedMessageText":body,qid+" successMessageText":done,qid+" failMessageText":"",qid+" acceptPlayerMessage":body,qid+" declinePlayerMessage":"",qid+" completePlayerMessage":done,qid+" changeQuestMessageText":"",hid(qid+":finish"):objective}

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
    expanded_by_lane={"A":[],"B":[]}
    for lane,row in [("A",x) for x in plan["lanes"]["A-close-support"][:10]]+[("B",x) for x in plan["lanes"]["B-rifle-precision"][:9]]:
        order,pool,band,locations,semantics=row; level=int(band.split('-')[0]); native_weapons=plan["pools"][pool]; optional_weapons=optional_by_pool.get(pool,[]); weapons=native_weapons+[x["tpl"] for x in optional_weapons]; runtime_locations=[x for label in locations for x in LOCATION_IDS[label]]
        # B-9 existed before the family correction. Keep its authored slug so
        # the persistent quest ID and player progress remain unchanged.
        slug = "rotation-b-09-battle-rifles" if lane == "B" and order == 9 else f"rotation-{lane.lower()}-{order:02d}-{pool}"
        name=f"Arsenal Rotation {lane}-{order}: {pool.replace('-',' ').title()}"
        target = "Savage" if pool == "sks-hunter" else "Any"
        minimum_distance = 40 if pool == "sks-hunter" else 0
        required_count = 8 if pool == "sks-hunter" else min(6+order,15)
        qid,q=quest(slug,name,level,previous[lane],lambda qid,w=weapons,l=runtime_locations,v=required_count,t=target,d=minimum_distance:counter(qid,v,kill(qid,w,l,t,d)),"Elimination")
        previous[lane]=qid;out.append((qid,q));meta.append({"id":qid,"kind":"weapon","lane":lane,"order":order,"pool":pool})
        expanded_by_lane[lane].append((level, qid, q))
        optional_names=", ".join(x["name"] for x in optional_weapons)
        optional_names_ru=", ".join(x["nameRu"] for x in optional_weapons)
        allowed_en=item_list(native_weapons,'en')+(f"; optional WTT: {optional_names}" if optional_names else "")
        allowed_ru=item_list(native_weapons,'ru')+(f"; опционально WTT: {optional_names_ru}" if optional_names_ru else "")
        en_detail=f"Allowed weapons: {item_list(native_weapons,'en')}." + (f"\nOptional WTT additions: {optional_names}." if optional_names else "")
        ru_detail=f"Разрешённое оружие:\n- {item_list(native_weapons,'ru')}." + (f"\nДополнительные модели при установленном WTT:\n- {optional_names_ru}." if optional_names_ru else "")
        objective_en = "Eliminate 8 Scavs from at least 40 metres with an allowed self-loading carbine on Customs, Woods, or Shoreline" if pool == "sks-hunter" else f"Eliminate {required_count} targets with the allowed weapon pool"
        objective_ru = "Устранить 8 Диких с дистанции от 40 метров допустимым самозарядным карабином на Таможне, Лесу или Берегу" if pool == "sks-hunter" else f"Устранить {required_count} целей разрешённым оружием"
        if pool == "sks-hunter":
            name = "Arsenal Rotation B-4: Self-loading Carbines"
            q["QuestName"] = name
            en_detail += "\nTask: eliminate 8 Scavs from at least 40 metres on Customs, Woods, or Shoreline; progress carries across raids."
            ru_detail += "\nЗадача: устранить 8 Диких с дистанции не менее 40 метров на Таможне, Лесу или Берегу; прогресс сохраняется между рейдами."
        ru_name = f"Ротация «Арсенал» {lane}-{order}: {POOL_NAMES_RU[pool]}"
        en.update(locale(qid,name,en_detail,level,objective_en));ru.update(locale(qid,ru_name,ru_detail,level,objective_ru,True))
    # Ground Zero opening chain.
    prev=None
    gz=[("ground-zero-arrival","Operation: First Contact",1,2,"Savage"),("ground-zero-corridor","Operation: Open Corridor",3,4,"Savage"),("ground-zero-pressure","Operation: Contested Ground",5,2,"AnyPmc"),("ground-zero-exit","Operation: Exit Discipline",7,5,"Any")]
    for slug,name,level,count,target in gz:
        qid,q=quest(slug,name,level,prev,lambda qid,c=count,t=target:counter(qid,c,kill(qid,[],LOCATION_IDS["Ground Zero"],t)),"Elimination");prev=qid;out.append((qid,q));meta.append({"id":qid,"kind":"operation","location":"Ground Zero"})
        q["location"]="653e6760052c01c1c805532f"
        en.update(locale(qid,name,"",level,f"Eliminate {count} targets on Ground Zero"));ru.update(locale(qid,name,"",level,f"Устранить {count} целей на Эпицентре",True))
    # A staged non-weapon equipment chain.
    gear=[
        # The opening assignment is logistics training, not a combat exam. Two
        # groups require one common rig and one common backpack simultaneously.
        ("light-rig","Loadout: First Field Kit",6,["Ground Zero","Customs","Woods"],[["572b7adb24597762ae139821","5e4abc1f86f774069619fbaa","6034d0230ca681766b6a0fb5"],["544a5cde4bdc2d39388b456b","56e33680d2720be2748b4576","56e335e4d2720b6c058b456d"]],1,None,None,"Equip one allowed rig and one allowed backpack, then survive and extract from Ground Zero, Customs, or Woods in the same raid; no kills or received damage are required","Надеть одну разрешённую разгрузку и один разрешённый рюкзак, затем выжить и эвакуироваться с Эпицентра, Таможни или Леса в том же рейде; убийства и получение урона не требуются"),
        ("field-headset","Loadout: Acoustic Cover",11,["Customs","Woods","Shoreline"],[["5b432b965acfc47a8774094e","5e4d34ca86f774264f758330"]],4,"Any","f2b78c3ab062acd976bbe35c","Wear the GSSh-01 or Walker’s Razor Digital headset and eliminate 4 targets in one raid on Customs, Woods, or Shoreline; separately survive and extract from one of those maps","Надеть гарнитуру ГСШ-01 или Walker’s Razor Digital и устранить 4 цели за один рейд на Таможне, Лесу или Берегу; отдельно выжить и эвакуироваться с одной из этих карт"),
        ("service-helmet","Loadout: Head Protection",16,["Woods","Shoreline","Interchange"],[["5c06c6a80db834001b735491","5aa7cfc0e5b5b00015693143"]],5,"Any","9d78917164400742a5e2511d","Test the helmet under fire: eliminate 5 targets and survive the same raid","Проверить защиту головы в бою: устранить 5 противников и выжить в том же рейде"),
        ("medium-armor","Loadout: Mobile Armor",21,["Shoreline","Interchange","Streets"],[["5c0e655586f774045612eeb2","5c0e625a86f7742d77340f62"]],2,"AnyPmc","3ac29a7f402bea66538246bc","Test the mobile armor: eliminate 2 PMCs and survive the same raid","Проверить подвижную броню: устранить 2 бойцов ЧВК и выжить в том же рейде"),
        ("cargo-rig","Loadout: Sustainment",26,["Reserve","Lighthouse","Streets"],[["5df8a42886f77412640e2e75","5c0e9f2c86f77432297fe0a3"]],7,"Any","2a064ed77cf937cc6d423718","Complete a sustained combat patrol: eliminate 7 targets and survive the same raid","Провести длительный боевой выход: устранить 7 противников и выжить в том же рейде"),
        ("heavy-kit","Loadout: Breach Weight",31,["Factory","Reserve","The Lab"],[["5ca2151486f774244a3b8d30","5ca21c6986f77479963115a7"]],3,"AnyPmc","81c019d78a77aa67046c0c16","Test the heavy assault load: eliminate 3 PMCs and survive the same raid","Проверить тяжёлый штурмовой комплект: устранить 3 бойцов ЧВК и выжить в том же рейде"),
    ]
    prev=None
    for slug,name,level,locations,equipment_groups,count,target,kill_condition_id,task_en,task_ru in gear:
        runtime_locations=[runtime_id for location_name in locations for runtime_id in LOCATION_IDS[location_name]]
        qtype="Elimination" if target else "Exploration"
        qid,q=quest(slug,name,level,prev,lambda qid,l=runtime_locations,g=equipment_groups,c=count,t=target,k=kill_condition_id,qt=qtype:counter(qid,c,loc_condition(qid,l,g,t,k),qt,True),qtype)
        if slug == "light-rig":
            reward_id=hid(qid+":starter-pack")
            q["rewards"]["Success"].append({"value":1,"id":reward_id,"type":"Item","target":reward_id,"index":3,"items":[{"_id":reward_id,"_tpl":"5e9dcf5986f7746c417435b3","upd":{"StackObjectsCount":1}}]})
        prev=qid;out.append((qid,q));meta.append({"id":qid,"kind":"equipment","locations":locations})
        rendered_en=[item_list(group,'en') for group in equipment_groups]
        rendered_ru=[item_list(group,'ru') for group in equipment_groups]
        en_detail=("Eligible equipment:\n" + "\n".join(f"- choose one from: {group}." for group in rendered_en) + f"\nTask: {task_en}.") if rendered_en else f"Task: {task_en}."
        ru_detail=("Допуск по снаряжению:\n" + "\n".join(f"- выбрать один предмет: {group}." for group in rendered_ru) + f"\nЗадача: {task_ru}.") if rendered_ru else f"Задача: {task_ru}."
        objective_en = "Equip one allowed rig and one allowed backpack, then survive and extract" if slug == "light-rig" else task_en
        objective_ru = "Надеть разрешённую разгрузку и рюкзак, затем выжить и эвакуироваться" if slug == "light-rig" else task_ru
        en.update(locale(qid,name,en_detail,level,objective_en));ru.update(locale(qid,name,ru_detail,level,objective_ru,True))
    qdir=ROOT/"db/quests"
    lane_lengths={}
    for lane in ("A", "B"):
        legacy=[]
        for effective_level, quest_id in LEGACY_WEAPON_LANES[lane]:
            path=next(qdir.glob(f"20-*{quest_id}.json"))
            legacy.append((effective_level, quest_id, json.loads(path.read_text(encoding="utf-8")), path))
        combined=[(level,0,index,quest_id,quest_data,None) for index,(level,quest_id,quest_data) in enumerate(expanded_by_lane[lane])]
        combined += [(level,1,index,quest_id,quest_data,path) for index,(level,quest_id,quest_data,path) in enumerate(legacy)]
        combined.sort(key=lambda row:(row[0],row[1],row[2]))
        previous_id=None
        for _,_,_,quest_id,quest_data,path in combined:
            replace_prerequisite(quest_data, previous_id)
            previous_id=quest_id
            if path:
                path.write_text(json.dumps(quest_data,separators=(",",":"),ensure_ascii=False)+"\n",encoding="utf-8")
        lane_lengths[lane]=len(combined)
    for p in qdir.glob("40-*.json"): p.unlink()
    for p in qdir.glob("50-*.json"): p.unlink()
    for i,(qid,q) in enumerate(out):
        prefix="40" if meta[i]["kind"]=="weapon" else "50"
        (qdir/f"{prefix}-{i+1:02d}-{qid}.json").write_text(json.dumps(q,separators=(",",":"),ensure_ascii=False)+"\n",encoding="utf-8")
    (ROOT/"db/locales/m8-en.json").write_text(json.dumps(en,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    (ROOT/"db/locales/m8-ru.json").write_text(json.dumps(ru,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    manifest={"schemaVersion":1,"status":"runtime-materialized","totalQuestCount":72,"newQuestCount":29,"weaponAssignments":19,"totalWeaponQuestCount":40,"weaponLaneQuestCounts":lane_lengths,"weaponGraphRoots":2,"groundZeroOperations":4,"equipmentAssignments":6,"maximumConcurrentWeaponAssignments":2,"optionalWeaponCount":len(optional["acceptedWeapons"]),"optionalWeaponsRequired":False,"icebreaker":{"reserved":False,"runtimePublished":True,"optionalQuestCount":10,"coreQuestCountWhenAbsent":172,"totalQuestCountWhenPresent":182,"manifest":"icebreaker-runtime.json"},"quests":meta}
    (ROOT/"manifests/m8-campaign-expansion-runtime.json").write_text(json.dumps(manifest,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    first=qdir/"01-5d404ebd654de4efecef71d2.json"
    base=json.loads(first.read_text(encoding="utf-8"))
    if not any(c.get("conditionType")=="Quest" and c.get("target")==out[19][0] for c in base["conditions"]["AvailableForStart"]):
        base["conditions"]["AvailableForStart"].append({"id":hid("foundation-after-ground-zero"),"index":1,"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","target":out[19][0],"status":[4],"availableAfter":0,"dispersion":0,"conditionType":"Quest"})
    first.write_text(json.dumps(base,separators=(",",":"),ensure_ascii=False)+"\n",encoding="utf-8")
    print(json.dumps({"generated":len(out),"total":72}))
if __name__=="__main__": main()
