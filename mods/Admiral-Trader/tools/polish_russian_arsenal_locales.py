#!/usr/bin/env python3
"""Keep Russian Arsenal copy explicit, localized and aligned with runtime pools."""
import json, re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RU = ROOT / "db/locales"

M8_TITLES = [
    "Ротация «Арсенал» A-1: Служебные пистолеты", "Ротация «Арсенал» A-2: Компактные автоматы Калашникова",
    "Ротация «Арсенал» A-3: Ружья с ручной перезарядкой", "Ротация «Арсенал» A-4: Ранние пистолеты-пулемёты",
    "Ротация «Арсенал» A-5: Пистолеты калибра .45", "Ротация «Арсенал» A-6: Служебные пистолеты-пулемёты",
    "Ротация «Арсенал» A-7: Автоматические пистолеты", "Ротация «Арсенал» A-8: Компактное оружие самообороны",
    "Ротация «Арсенал» A-9: Самозарядные ружья", "Ротация «Арсенал» A-10: Современные пистолеты-пулемёты",
    "Ротация «Арсенал» B-1: Начальные служебные винтовки", "Ротация «Арсенал» B-2: Гражданские автоматы Калашникова",
    "Ротация «Арсенал» B-3: Компактные штурмовые винтовки", "Ротация «Арсенал» B-4: Самозарядные карабины",
    "Ротация «Арсенал» B-5: Классические АК калибра 5,45", "Ротация «Арсенал» B-6: Классические АК калибра 7,62",
    "Ротация «Арсенал» B-7: Служебные винтовки НАТО", "Ротация «Арсенал» B-8: Альтернативные винтовки НАТО",
    "Ротация «Арсенал» B-9: Боевые винтовки", "Операция: Первый контакт", "Операция: Открытый коридор",
    "Операция: Спорная территория", "Операция: Правильный выход", "Комплект: Первый выход",
    "Комплект: Акустический контроль", "Комплект: Защита головы", "Комплект: Подвижная броня",
    "Комплект: Автономность", "Комплект: Штурмовая нагрузка",
]

REPLACEMENTS = {
    "Ground Zero": "Эпицентр", "Customs": "Таможня", "Factory": "Завод", "Woods": "Лес",
    "Shoreline": "Берег", "Interchange": "Развязка", "Reserve": "Резерв", "Lighthouse": "Маяк",
    "Streets": "Улицы Таркова", "The Lab": "Лаборатория", "FIR": "статус «Найдено в рейде»",
    " pistol": " (пистолет)", " assault rifle": " (штурмовая винтовка)",
    " submachine gun": " (пистолет-пулемёт)", " shotgun": " (ружьё)", " carbine": " (карабин)",
}

ACCESS_BRIEFS = {
    "1b92e4cf212d895be4f70b2c": ("Завод прощает только один неверный поворот. Покажи, что у тебя есть простой комплект ключей для прохода через служебные помещения; ничего сдавать не нужно.", "Маршрут через Завод подтверждён. Теперь ты не зависишь от единственной открытой двери."),
    "cba3374f11375c4e7eea9efe": ("На Таможне полезный маршрут часто заканчивается запертой дверью. Собери рабочую связку из доступных ключей и сохрани её для будущих выходов.", "Покрытие Таможни принято. Связка остаётся у тебя — она ещё пригодится."),
    "80c11744c2310e1bfe160a8b": ("В Лесу мало дверей, но каждая закрытая точка меняет маршрут. Подтверди, что можешь открыть хотя бы один из практичных запасных проходов.", "Лесной маршрут подтверждён. Закрытые точки больше не должны ломать план выхода."),
    "42ff30bc152b8212dc15b20a": ("На Развязке ключ определяет, идёшь ты за припасами или возвращаешься с пустыми руками. Подготовь два подходящих варианта для торгового комплекса.", "Доступ на Развязке подтверждён. Два независимых маршрута дают нужный запас свободы."),
    "d737e3b990c371708db369f0": ("Берег разбросан между санаторием и хозяйственными зданиями. Собери два применимых ключа, чтобы операция не зависела от одной комнаты.", "Покрытие Берега принято. Теперь можно планировать работу внутри закрытого сектора."),
    "9c438fa48f645044ddc75e8d": ("На Резерве закрытые помещения стоят рядом с самыми опасными маршрутами. Подтверди два рабочих ключа и держи их при себе для следующих операций.", "Доступ на Резерве подтверждён. Подземные и наземные маршруты можно готовить заранее."),
    "59a36c5ee68210f9154d94d0": ("Маяк не прощает импровизации у закрытой двери. Подготовь два ключа из проверенного набора, чтобы работа не зависела от случайной находки в рейде.", "Покрытие Маяка принято. Следующие группы смогут работать по заранее выбранному маршруту."),
    "a8b05eb5c5f0e67ee881df52": ("До Лаборатории добираются не ради прогулки. Подтверди действующий пропуск, но не передавай его: мне нужна твоя готовность вернуться туда ещё раз.", "Пропуск в Лабораторию подтверждён и остаётся у тебя. Канал для поздних операций открыт."),
    "68a6527a3c73b2e85977d7a1": ("Финальная проверка доступа: предъяви одну действующую карту высокого допуска. Карта останется у тебя; важно подтвердить устойчивый доступ, а не оплатить его ещё раз.", "Полный протокол доступа закрыт. У тебя есть подтверждённый путь к самым защищённым объектам."),
}

M8_OPERATION_BRIEFS = {
    "02c07ee31821696597ceabef": "Первый выход задаёт привычки на всю кампанию. Проведи короткий контакт на Эпицентре с доступным оружием и вернись живым; мне важнее дисциплина, чем число тел.",
    "3c6e085fc02f0597efdb5d5a": "Проверь несколько открытых направлений и не привязывайся к одной улице. Работай подходящим оружием, сохрани темп и подтверди, что можешь выбрать безопасный выход.",
    "31ab6a69a8436df6b3834b0a": "Граница влияния проходит сразу через несколько районов. Выбери удобный маршрут, проведи контакт и покажи, что умеешь менять карту без смены всей подготовки.",
    "e520cec55b83621928e9e4ec": "Операция считается законченной только после выхода. Выполни боевую часть подходящим оружием и сохрани бойца до эвакуации — результат без возвращения мне не нужен.",
}

def load(path): return json.loads(path.read_text(encoding="utf-8"))
def save(path, value): path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
def save_quest(path, value): path.write_text(json.dumps(value, separators=(",", ":"), ensure_ascii=False) + "\n", encoding="utf-8")

def weapon_names():
    text = (ROOT / "docs/campaign-audit-43.md").read_text(encoding="utf-8")
    result = {}
    for name, tpl in re.findall(r"([^;|]+?) \(`([0-9a-f]{24})`\)", text):
        clean = name.strip().lstrip("|").strip()
        for old, new in REPLACEMENTS.items(): clean = clean.replace(old, new)
        result.setdefault(tpl, clean)
    return result

def main():
    m8 = load(RU / "m8-ru.json")
    name_keys = [key for key in m8 if key.endswith(" name")]
    if len(name_keys) != len(M8_TITLES): raise ValueError("M8 Russian title count drift")
    for key, title in zip(name_keys, M8_TITLES): m8[key] = title
    for key, value in list(m8.items()):
        if not isinstance(value, str): continue
        for old, new in REPLACEMENTS.items(): value = value.replace(old, new)
        m8[key] = value
    for qid, description in M8_OPERATION_BRIEFS.items():
        m8[qid + " description"] = description
        m8[qid + " startedMessageText"] = description
        m8[qid + " acceptPlayerMessage"] = description
    for key in [key for key in m8 if key.endswith(" name")]:
        qid = key[:-5]
        name = m8[key]
        if name.startswith("Ротация «Арсенал»"):
            category = name.split(": ", 1)[1]
            clarification = m8[qid + " description"].split("\n\nУточнение:", 1)
            description = f"Адмирал назначает следующую ступень оружейной ротации: {category.lower()}. Выбери подходящую модель из указанного пула и подтверди результат в реальных рейдах."
            if len(clarification) == 2:
                description += "\n\nУточнение:" + clarification[1]
            m8[qid + " description"] = description
            m8[qid + " startedMessageText"] = description
            m8[qid + " acceptPlayerMessage"] = description
            success = f"{name.split(': ', 1)[1]}: испытание принято. Следующая ступень ротации разблокирована."
        elif name.startswith("Операция:"):
            success = f"{name.split(': ', 1)[1]} завершена. Маршрут и результаты добавлены в оперативную сводку."
        elif name.startswith("Комплект:"):
            category = name.split(": ", 1)[1]
            clarification = m8[qid + " description"].split("\n\nУточнение:", 1)
            description = f"Проверь полевой комплект «{category}» в настоящем выходе. Снаряжение должно менять способ ведения рейда, поэтому соблюдай указанный состав и доведи задачу до эвакуации."
            if len(clarification) == 2:
                description += "\n\nУточнение:" + clarification[1]
            m8[qid + " description"] = description
            m8[qid + " startedMessageText"] = description
            m8[qid + " acceptPlayerMessage"] = description
            success = f"{name.split(': ', 1)[1]} проверен в деле. Комплект допускается к следующим операциям."
        else:
            continue
        m8[qid + " successMessageText"] = success
        m8[qid + " completePlayerMessage"] = success
    save(RU / "m8-ru.json", m8)

    access = load(RU / "ru.json")
    for qid, (description, success) in ACCESS_BRIEFS.items():
        access[qid + " description"] = description
        access[qid + " startedMessageText"] = description
        access[qid + " acceptPlayerMessage"] = description
        access[qid + " successMessageText"] = success
        access[qid + " completePlayerMessage"] = success
    save(RU / "ru.json", access)

    names = weapon_names(); arsenal = load(RU / "arsenal-ru.json")
    for quest_path in sorted((ROOT / "db/quests").glob("20-*.json")):
        quest = load(quest_path); qid = quest["_id"]
        weapons = quest["conditions"]["AvailableForFinish"][0]["counter"]["conditions"][0].get("weapon", [])
        missing = [tpl for tpl in weapons if tpl not in names]
        if missing: raise ValueError(f"{qid}: missing Russian display names for {missing}")
        clarification = "Уточнение:\nРазрешённое оружие: " + "; ".join(names[tpl] for tpl in weapons) + ". Прогресс сохраняется между рейдами; статус «Найдено в рейде» не применяется."
        base = arsenal[qid + " description"].split("\n\nУточнение:", 1)[0]
        arsenal[qid + " description"] = base + "\n\n" + clarification
        arsenal[qid + " startedMessageText"] = arsenal[qid + " description"]
        arsenal[qid + " acceptPlayerMessage"] = arsenal[qid + " description"]
    save(RU / "arsenal-ru.json", arsenal)

    # SPT normally resolves the locale key, but QuestName is also used by
    # fallback UI paths. Keep it Russian so an unavailable or late locale load
    # cannot expose the old English generator title to the player.
    localized = {}
    for filename in ("ru.json", "arsenal-ru.json", "m3-ru.json", "m8-ru.json", "story-ru.json"):
        localized.update(load(RU / filename))
    for quest_path in sorted((ROOT / "db/quests").glob("*.json")):
        quest = load(quest_path)
        title = localized[quest["_id"] + " name"]
        if not re.search(r"[А-Яа-яЁё]", title):
            raise ValueError(f"{quest['_id']}: Russian title is missing")
        quest["QuestName"] = title
        save_quest(quest_path, quest)

if __name__ == "__main__": main()
