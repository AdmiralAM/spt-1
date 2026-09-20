import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUESTS = ROOT / "db" / "quests"


def test_kill_and_extraction_events_are_never_combined():
    for path in QUESTS.glob("*.json"):
        quest = json.loads(path.read_text(encoding="utf-8-sig"))
        for objective in quest["conditions"]["AvailableForFinish"]:
            kinds = {condition.get("conditionType") for condition in objective.get("counter", {}).get("conditions", [])}
            assert not ({"Kills", "ExitStatus"} <= kinds), (path.name, objective["id"])


def test_open_corridor_has_working_combat_and_extraction_objectives():
    quest = json.loads((QUESTS / "50-21-3c6e085fc02f0597efdb5d5a.json").read_text(encoding="utf-8"))
    finish = quest["conditions"]["AvailableForFinish"]
    assert len(finish) == 2
    kill = next(x for x in finish if any(c.get("conditionType") == "Kills" for c in x["counter"]["conditions"]))
    survive = next(x for x in finish if any(c.get("conditionType") == "ExitStatus" for c in x["counter"]["conditions"]))
    assert kill["value"] == 4 and kill["oneSessionOnly"] is True
    assert survive["value"] == 1 and survive["oneSessionOnly"] is True
    assert {"Location", "ExitStatus"} == {c["conditionType"] for c in survive["counter"]["conditions"]}
