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
    "Ротация «Арсенал» B-3: Компактные штурмовые винтовки", "Ротация «Арсенал» B-4: СКС и охотничьи карабины",
    "Ротация «Арсенал» B-5: Классические АК калибра 5,45", "Ротация «Арсенал» B-6: Классические АК калибра 7,62",
    "Ротация «Арсенал» B-7: Служебные винтовки НАТО", "Ротация «Арсенал» B-8: Альтернативные винтовки НАТО",
    "Ротация «Арсенал» B-9: Боевые винтовки", "Операция: Первый контакт", "Операция: Открытый коридор",
    "Операция: Спорная территория", "Операция: Правильный выход", "Комплект: Низкая заметность",
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

def load(path): return json.loads(path.read_text(encoding="utf-8"))
def save(path, value): path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")

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
    save(RU / "m8-ru.json", m8)

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

if __name__ == "__main__": main()
