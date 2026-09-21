import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def quest_path(quest_id: str) -> Path:
    matches = list((ROOT / "db/quests").glob(f"*-{quest_id}.json"))
    if len(matches) != 1:
        raise RuntimeError(f"Expected one quest file for {quest_id}, found {len(matches)}")
    return matches[0]


def load_quest(quest_id: str) -> tuple[Path, dict]:
    path = quest_path(quest_id)
    return path, json.loads(path.read_text(encoding="utf-8-sig"))


def save(path: Path, data: dict) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def counter(quest: dict) -> dict:
    counters = [condition for condition in quest["conditions"]["AvailableForFinish"] if "counter" in condition]
    if len(counters) != 1:
        raise RuntimeError(f"Expected one counter in {quest['_id']}, found {len(counters)}")
    return counters[0]


def add_condition(conditions: list[dict], condition: dict) -> None:
    if not any(existing.get("id") == condition["id"] for existing in conditions):
        conditions.append(condition)


# Low Profile keeps its persistent ID and early equipment reward, but now tests
# an actual quiet route through Interchange rather than entry and extraction alone.
path, quest = load_quest("208db81b5ce195bf0c176852")
conditions = counter(quest)["counter"]["conditions"]
add_condition(conditions, {
    "id": "d9f2f21d778e4be2bc6a6811",
    "dynamicLocale": False,
    "conditionType": "VisitPlace",
    "target": "place_SALE_03_KOSTIN",
    "value": 1,
})
add_condition(conditions, {
    "id": "709b87c0acd942d0ae664bde",
    "dynamicLocale": False,
    "conditionType": "VisitPlace",
    "target": "place_WARBLOOD_04_1",
    "value": 1,
})
save(path, quest)

# Open Corridor stays viable with starter weapons; extraction is a separate objective.
path, quest = load_quest("3c6e085fc02f0597efdb5d5a")
quest_counter = counter(quest)
quest_counter["oneSessionOnly"] = True
quest_counter["value"] = 2
conditions = quest_counter["counter"]["conditions"]
kills = next(condition for condition in conditions if condition["conditionType"] == "Kills")
kills["distance"] = {"value": 30, "compareMethod": ">="}
save(path, quest)

# Exit Discipline now enforces the extraction promised by its title and brief.
path, quest = load_quest("e520cec55b83621928e9e4ec")
quest_counter = counter(quest)
quest_counter["oneSessionOnly"] = True
conditions = quest_counter["counter"]["conditions"]
add_condition(conditions, {
    "id": "56406f2f32c942489625c9b1",
    "dynamicLocale": False,
    "conditionType": "ExitStatus",
    "status": ["Survived"],
})
save(path, quest)

locale_updates = {
    "db/locales/m3-ru.json": {
        "208db81b5ce195bf0c176852 description": "На Развязке нужен маршрут, который не привлекает внимания дорогим снаряжением. Надень Жилет Дикого и Сумку-трансформер, проверь служебную зону KOSTIN и первый складской сектор, затем выйди живым в том же комплекте. Здесь оценивается не стоимость экипировки, а способность провести разведку без лишнего шума.",
        "96d629538203984d6a1ee835": "Развязка, один рейд: Жилет Дикого + Сумка-трансформер; KOSTIN; первый складской сектор; выжить и эвакуироваться в том же комплекте."
    },
    "db/locales/m3-en.json": {
        "208db81b5ce195bf0c176852 description": "Run a low-profile Interchange route in a Scav Vest and Transformer Bag. Check the KOSTIN service area and the first warehouse sector, then survive and extract in the same equipment.",
        "96d629538203984d6a1ee835": "Interchange, one raid: Scav Vest + Transformer Bag; KOSTIN; first warehouse sector; survive and extract in the same equipment."
    },
    "db/locales/m8-ru.json": {
        "3c6e085fc02f0597efdb5d5a description": "Проверь путь через Эпицентр. За один рейд устрани двух Диких с дистанции не менее 30 метров. Модель оружия не ограничена: используй своё оружие, станковый пулемёт или гранатомёт. Затем выживи и эвакуируйся с Эпицентра. Боевая цель и выход учитываются отдельно.",
        "0655c05e2745efd12e740c0d": "За один рейд устранить 2 Диких на Эпицентре с расстояния от 30 метров любым оружием, в том числе станковым пулемётом или гранатомётом.",
        "e520cec55b83621928e9e4ec description": "Заверши боевую проверку на Эпицентре за один рейд: устрани пять противников и эвакуируйся со статусом «Выжил». Результат без возвращения не засчитывается.",
        "a0d2f4ce6b38983415aff9be": "За один рейд устранить 5 любых противников на Эпицентре, затем эвакуироваться со статусом «Выжил»."
    },
    "db/locales/m8-en.json": {
        "3c6e085fc02f0597efdb5d5a description": "Check a route through Ground Zero. Eliminate two Scavs in one raid from at least 30 metres with no weapon-model requirement. Separately, survive and extract from Ground Zero.",
        "0655c05e2745efd12e740c0d": "Eliminate 2 Scavs in one Ground Zero raid from at least 30 metres with any weapon, including mounted machine guns or grenade launchers.",
        "e520cec55b83621928e9e4ec description": "Complete the Ground Zero combat check in one raid: eliminate five targets and extract with Survived status.",
        "a0d2f4ce6b38983415aff9be": "In one Ground Zero raid, eliminate 5 targets, then extract with Survived status."
    },
}

for relative, updates in locale_updates.items():
    path = ROOT / relative
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    missing = set(updates) - set(data)
    if missing:
        raise RuntimeError(f"Locale keys missing in {relative}: {sorted(missing)}")
    data.update(updates)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

quality_path = ROOT / "manifests/quest-quality-runtime.json"
quality = json.loads(quality_path.read_text(encoding="utf-8-sig"))
low_profile = next(
    row for row in quality["equipmentObjectives"]
    if row["questId"] == "208db81b5ce195bf0c176852"
)
low_profile["en"] = locale_updates["db/locales/m3-en.json"]["96d629538203984d6a1ee835"]
low_profile["ru"] = locale_updates["db/locales/m3-ru.json"]["96d629538203984d6a1ee835"]
low_profile["detailsEn"] = "In one raid, wear a Scav Vest and Transformer Bag; on Interchange; visit the KOSTIN service area; visit the first warehouse sector; survive and extract in the same equipment. Found-in-raid status does not apply."
low_profile["detailsRu"] = "За один рейд: надеть Жилет Дикого и Сумку-трансформер; на локации Развязка; посетить служебную зону KOSTIN; посетить первый складской сектор; выжить и эвакуироваться в том же комплекте. Статус «Найдено в рейде» не применяется."
quality_path.write_text(json.dumps(quality, ensure_ascii=True, indent=2) + "\n", encoding="utf-8")

print("Polished Low Profile, Open Corridor and Exit Discipline; retained Acoustic Discipline and Contested Ground as distinct roles")
