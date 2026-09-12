#!/usr/bin/env python3
"""Materialize the optional ten-part Icebreaker investigation."""
import hashlib, json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRADER = "d5c27bb3169f8dfbc13f6b69"
MAP = "882b2fa04bbd616567022938"
DISCOVERY = "9d5e3f7d6320a7fd139a2772"
ROUBLES = "5449016a4bdc2d6f028b456f"
MARKER = "5991b51486f77447b112d44f"
FUEL = "5b43575a86f77424f443fe62"
INTEL = "5c12613b86f7743bbe2c3f76"

STEPS = [
 ("Cold Wake", "Холодный след", 28, "Mechanic found the hull, but coordinates alone do not make a route. Reach Boreas, check whether the approaches are usable, and return alive with a first-hand report.", "Механик нашёл корпус, но одни координаты ещё не дают маршрута. Доберись до «Борея», проверь подходы и вернись живым с докладом."),
 ("Silent Manifest", "Немой манифест", 29, "The ship's public cargo record is deliberately empty. Bring intact intelligence folders that can be cross-checked against the markings and schedules observed aboard Boreas.", "Открытый грузовой реестр судна намеренно вычищен. Принеси целые папки с разведданными: их сверят с маркировкой и расписаниями на борту «Борея»."),
 ("Outer Watch", "Внешний дозор", 30, "Looters are mapping the same approaches. Thin their screen before they turn every safe route into an ambush corridor.", "Мародёры изучают те же подходы. Прореди их заслон, пока каждый безопасный маршрут не превратился в засаду."),
 ("Frozen Repair", "Замёрзший ремонт", 31, "One service point on Woods supplied the ship before the blockade. Mark it for a technical team and leave the area alive; the site matters more than another firefight.", "До блокады судно снабжала ремонтная точка в Лесу. Отметь её для технической группы и выйди живым: сейчас место важнее ещё одной перестрелки."),
 ("Resort Relay", "Курортный ретранслятор", 32, "A relay near the resort carried short encrypted bursts toward the coast. Clear the camp inside its actual perimeter so Natalya can inspect the equipment without sending another team into occupied ground.", "У санатория работал ретранслятор, передававший короткие шифрованные пакеты к побережью. Зачисти лагерь строго в его периметре, чтобы Наталья смогла проверить аппаратуру."),
 ("Broken Transit", "Разорванный транзит", 33, "The supply route split between Woods and Lighthouse. Mark both transfer points; together they reveal who moved personnel to Boreas after the city was sealed.", "Маршрут снабжения расходился между Лесом и Маяком. Отметь обе перевалочные точки: вместе они покажут, кто перебрасывал людей на «Борей» после изоляции города."),
 ("Close Quarters", "Ближний борт", 34, "Long rifles catch on ladders and bulkheads. Run a compact boarding weapon on Boreas and prove the loadout under real resistance.", "Длинные стволы цепляются за трапы и переборки. Возьми компактное оружие и проверь абордажный комплект на «Борее» под настоящим сопротивлением."),
 ("Fuel Ledger", "Топливный реестр", 35, "Fuel-control records can date the ship's last powered movement. Secure usable conditioners for comparison, then verify Boreas is still reachable before the next team deploys.", "Топливные записи позволят определить последнее движение судна своим ходом. Добыть кондиционеры для сверки недостаточно — ещё раз подтверди, что путь к «Борею» открыт."),
 ("Command Deck", "Командная палуба", 37, "Contractors have replaced scavengers around the command section. Remove the PMC element before it destroys the bridge records or extracts whoever is giving orders.", "У командной секции диких сменили контрактники. Устрани группу ЧВК, пока она не уничтожила записи мостика и не вывезла того, кто отдаёт приказы."),
 ("Boreas Protocol", "Протокол «Борей»", 39, "The route, relay and fuel ledger point to one final transfer window. Sweep Boreas in a single deployment and survive; Admiral needs a closed operation, not another missing squad.", "Маршрут, ретранслятор и топливный реестр указывают на последнее окно переброски. Зачисти «Борей» за один выход и выживи: Адмиралу нужна завершённая операция, а не ещё одна пропавшая группа."),
]

BONUS = {2:("5d02797c86f774203f38e30a",1,"Surv12 field surgical kit","хирургический набор Surv12"),4:("617aa4dd8166f034d57de9c5",2,"M18 green smoke grenades","зелёные дымовые гранаты M18"),6:("5ed51652f6c34d2cc26336a1",1,"M.U.L.E. stimulant injector","стимулятор M.U.L.E."),8:("5c0e534186f7747fa1419867",1,"eTG-change stimulant injector","стимулятор eTG-change"),10:("5d1b376e86f774252519444e",1,"Fierce Hatchling moonshine","самогон Fierce Hatchling")}

def hid(s): return hashlib.sha256(("admiral-icebreaker:" + s).encode()).hexdigest()[:24]
def location(q, suffix, target): return {"id":hid(q+suffix+"loc"),"dynamicLocale":False,"conditionType":"Location","target":[target]}
def counter(q, suffix, value, conditions, typ="Completion", one=False):
 return {"id":hid(q+suffix),"index":0,"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","value":value,"type":typ,"oneSessionOnly":one,"isResetOnConditionFailed":False,"isNecessary":False,"doNotResetIfCounterCompleted":False,"counter":{"id":hid(q+suffix+"counter"),"conditions":conditions},"completeInSeconds":0,"conditionType":"CounterCreator"}
def survive(q, target=MAP, one=True):
 return counter(q,"survive",1,[location(q,"survive",target),{"id":hid(q+"exit"),"dynamicLocale":False,"conditionType":"ExitStatus","status":["Survived"]}],"Completion",one)
def kills(q, value, target="Savage", weapon=None, zone=None, location_target=MAP):
 c={"id":hid(q+"kills-inner"),"dynamicLocale":False,"conditionType":"Kills","target":target,"compareMethod":">=","value":1,"weapon":weapon or [],"savageRole":[],"bodyPart":[],"daytime":{"from":0,"to":0},"distance":{"compareMethod":">=","value":0}}
 conditions=[c,location(q,"kills",location_target)]
 if zone: conditions.insert(0,{"id":hid(q+"zone"),"dynamicLocale":False,"conditionType":"InZone","zoneIds":[zone]})
 return counter(q,"kills",value,conditions,"Elimination")
def item(q, tpl, count, kind):
 return {"conditionType":kind,"countInRaid":False,"dogtagLevel":0,"dynamicLocale":False,"globalQuestCounterId":"","id":hid(q+kind+tpl),"index":0,"isEncoded":False,"maxDurability":100,"minDurability":0,"onlyFoundInRaid":True,"parentId":"","target":[tpl],"value":count,"visibilityConditions":[]}
def beacon(q, zone):
 return {"conditionType":"PlaceBeacon","dynamicLocale":False,"globalQuestCounterId":"","id":hid(q+zone),"index":0,"parentId":"","plantTime":10,"target":[MARKER],"value":1,"visibilityConditions":[],"zoneId":zone}
def reward(q, order):
 xp=14500+order*1500; rub=52000+order*6000; rep=round(.012+order*.001,3); iid=hid(q+"rub-item")
 result=[{"value":xp,"id":hid(q+"xp"),"type":"Experience","index":0},{"value":rep,"id":hid(q+"rep"),"type":"TraderStanding","target":TRADER,"index":1},{"value":rub,"id":hid(q+"rub"),"type":"Item","target":iid,"index":2,"items":[{"_id":iid,"_tpl":ROUBLES,"upd":{"StackObjectsCount":rub}}]}]
 if order in BONUS:
  tpl,count,_,_=BONUS[order]; bid=hid(q+"bonus-item")
  result.append({"value":count,"id":hid(q+"bonus"),"type":"Item","target":bid,"index":3,"items":[{"_id":bid,"_tpl":tpl,"upd":{"StackObjectsCount":count}}]})
 return result,xp,rub,rep

def main():
 out=ROOT/"db/optional/icebreaker"; qdir=out/"quests"; ldir=out/"locales"; qdir.mkdir(parents=True,exist_ok=True); ldir.mkdir(parents=True,exist_ok=True)
 for p in qdir.glob("*.json"): p.unlink()
 en={}; ru={}; rows=[]; previous=DISCOVERY
 for order,(en_name,ru_name,level,en_brief,ru_brief) in enumerate(STEPS,1):
  qid=hid(f"quest-{order}")
  if order==1: finish=[survive(qid)] ; en_req=["Survive and extract from Icebreaker once"] ; ru_req=["Выжить и эвакуироваться с Icebreaker один раз"]
  elif order==2: finish=[item(qid,INTEL,2,"FindItem"),item(qid,INTEL,2,"HandoverItem"),survive(qid)] ; en_req=["Find 2 Intelligence folders in raid","Hand over 2 found-in-raid Intelligence folders","Survive and extract from Icebreaker"] ; ru_req=["Найти в рейдах 2 папки с разведданными","Передать 2 найденные в рейдах папки с разведданными","Выжить и эвакуироваться с Icebreaker"]
  elif order==3: finish=[kills(qid,7,"Savage")] ; en_req=["Eliminate 7 Scavs on Icebreaker"] ; ru_req=["Устранить 7 диких на Icebreaker"]
  elif order==4: finish=[beacon(qid,"boreas_part1_repair"),survive(qid,"Woods")] ; en_req=["Plant a marker at the Boreas repair point on Woods","Survive and extract from Woods"] ; ru_req=["Установить маркер на ремонтной точке «Борея» в Лесу","Выжить и эвакуироваться из Леса"]
  elif order==5: finish=[kills(qid,6,"Any",zone="boreas_camp_resort",location_target="Shoreline")] ; en_req=["Eliminate 6 hostiles inside the Boreas resort camp on Shoreline"] ; ru_req=["Устранить 6 противников в лагере «Борея» у санатория на Берегу"]
  elif order==6: finish=[beacon(qid,"boreas_roman_woods_transit"),beacon(qid,"boreas_roman_lighthouse_transit")] ; en_req=["Plant a marker at the Woods transit point","Plant a marker at the Lighthouse transit point"] ; ru_req=["Установить маркер на транзитной точке в Лесу","Установить маркер на транзитной точке на Маяке"]
  elif order==7: finish=[kills(qid,7,"Savage",["5926bb2186f7744b1c6c6e60","57d14d2524597714373db789","5bd70322209c4d00d7167b8f","5cc82d76e24e8d00134b4b83"])] ; en_req=["Eliminate 7 Scavs on Icebreaker with an MP7, Kedr, MP5 or P90"] ; ru_req=["Устранить 7 диких на Icebreaker из MP7, «Кедра», MP5 или P90"]
  elif order==8: finish=[item(qid,FUEL,3,"FindItem"),item(qid,FUEL,3,"HandoverItem"),survive(qid)] ; en_req=["Find 3 Fuel conditioners in raid","Hand over 3 found-in-raid Fuel conditioners","Survive and extract from Icebreaker"] ; ru_req=["Найти в рейдах 3 топливных кондиционера","Передать 3 найденных в рейдах топливных кондиционера","Выжить и эвакуироваться с Icebreaker"]
  elif order==9: finish=[kills(qid,3,"AnyPmc")] ; en_req=["Eliminate 3 PMCs on Icebreaker"] ; ru_req=["Устранить 3 ЧВК на Icebreaker"]
  else: finish=[kills(qid,8,"Any"),survive(qid)] ; finish[0]["oneSessionOnly"]=True ; en_req=["Eliminate 8 hostiles on Icebreaker in one raid","Survive and extract from that raid"] ; ru_req=["Устранить 8 противников на Icebreaker за один рейд","Выжить и эвакуироваться из этого рейда"]
  for i,x in enumerate(finish): x["index"]=i
  rewards,xp,rub,rep=reward(qid,order)
  start=[{"id":hid(qid+"level"),"index":0,"compareMethod":">=","dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","value":level,"conditionType":"Level"},{"id":hid(qid+"pre"),"index":1,"dynamicLocale":False,"globalQuestCounterId":"","visibilityConditions":[],"parentId":"","target":previous,"status":[4],"availableAfter":0,"dispersion":0,"conditionType":"Quest"}]
  quest={"QuestName":en_name,"_id":qid,"canShowNotificationsInGame":True,"conditions":{"AvailableForFinish":finish,"AvailableForStart":start,"Fail":[]},"description":qid+" description","failMessageText":qid+" failMessageText","name":qid+" name","note":qid+" note","traderId":TRADER,"location":"any","image":"/files/quest/icon/5a27cafa86f77424e20615d6.jpg","type":"PickUp" if order in (2,8) else ("Elimination" if order in (3,5,7,9,10) else "Exploration"),"isKey":False,"restartable":False,"instantComplete":False,"secretQuest":False,"startedMessageText":qid+" startedMessageText","successMessageText":qid+" successMessageText","acceptPlayerMessage":qid+" acceptPlayerMessage","acceptanceAndFinishingSource":"eft","declinePlayerMessage":qid+" declinePlayerMessage","completePlayerMessage":qid+" completePlayerMessage","changeQuestMessageText":qid+" changeQuestMessageText","rewards":{"Started":[],"Success":rewards,"Fail":[]},"side":"Pmc","status":0,"progressSource":"eft","gameModes":[],"rankingModes":[],"arenaLocations":[]}
  (qdir/f"{order:02d}-{qid}.json").write_text(json.dumps(quest,ensure_ascii=False,separators=(",",":"))+"\n",encoding="utf-8")
  reward_en=f"Rewards:\n- {xp} XP\n- ₽{rub}\n- +{rep:.3f} Admiral standing"; reward_ru=f"Награды:\n- {xp} XP\n- ₽{rub}\n- +{rep:.3f} репутации Адмирала"
  if order in BONUS:
   _,count,en_bonus,ru_bonus=BONUS[order]; reward_en+=f"\n- {count} × {en_bonus}"; reward_ru+=f"\n- {count} × {ru_bonus}"
  for loc,name,brief,reqs,reward_text,done in ((en,en_name,en_brief,en_req,reward_en,"Icebreaker operation logged."),(ru,ru_name,ru_brief,ru_req,reward_ru,"Операция на Icebreaker внесена в журнал.")):
   body=f"Situation:\n{brief}\n\nRequirements:\n"+"\n".join("- "+x for x in reqs)+"\n\n"+reward_text if loc is en else f"Обстановка:\n{brief}\n\nТребования:\n"+"\n".join("- "+x for x in reqs)+"\n\n"+reward_text
   loc.update({qid+" name":name,qid+" description":body,qid+" note":"",qid+" startedMessageText":body,qid+" successMessageText":done+"\n\n"+reward_text,qid+" failMessageText":"",qid+" acceptPlayerMessage":body,qid+" declinePlayerMessage":"",qid+" completePlayerMessage":done,qid+" changeQuestMessageText":""})
  rows.append({"order":order,"id":qid,"prerequisite":previous,"level":level,"nameEn":en_name,"nameRu":ru_name})
  previous=qid
 (ldir/"en.json").write_text(json.dumps(en,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
 (ldir/"ru.json").write_text(json.dumps(ru,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
 manifest={"schemaVersion":1,"status":"runtime-materialized-when-detected","requiredDependency":False,"coreQuestCount":172,"optionalQuestCount":10,"location":{"guid":"com.manimal.icebreaker","versionValidated":"1.1.0","key":"icebreaker","id":MAP},"detection":{"serverDll":"icebreaker-server.dll","baseIdentityRequired":True},"entryPrerequisite":{"questId":DISCOVERY,"owner":"Manimal Icebreaker","name":"Boreas - Part 3"},"profileSafety":{"customItemsRequired":False,"coreGraphDependsOnOptional":False,"optionalGraphDependsOnCore":False},"quests":rows}
 (ROOT/"manifests/icebreaker-runtime.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
 print(json.dumps({"generated":len(rows),"first":rows[0]["id"],"last":rows[-1]["id"]}))
if __name__=="__main__": main()
