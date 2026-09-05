"""Apply the PR #327 onboarding correction to the staged frozen Trader only."""
import json
from pathlib import Path
import sys

QUEST_ID = "5d404ebd654de4efecef71d2"
TRADER_ID = "d5c27bb3169f8dfbc13f6b69"
TEST_HANDOVER_ROUBLES = 1000


def prepare(stage: Path):
    paths = list((stage / "db" / "quests").glob(f"*-{QUEST_ID}.json"))
    if len(paths) != 1:
        raise ValueError("Expected exactly one staged Fundamentals quest")
    path = paths[0]
    quest = json.loads(path.read_text(encoding="utf-8-sig"))
    conditions = quest["conditions"]["AvailableForStart"]
    if (quest["_id"] != QUEST_ID or quest["traderId"] != TRADER_ID
            or quest["secretQuest"] is not False or quest["side"] != "Pmc"
            or len(conditions) != 1 or conditions[0]["conditionType"] != "Level"
            or conditions[0]["compareMethod"] != ">=" or conditions[0]["value"] not in (1, 5)
            or len(quest["conditions"]["AvailableForFinish"]) != 1
            or quest["conditions"]["AvailableForFinish"][0]["conditionType"] not in ("FindItem", "HandoverItem")):
        raise ValueError("Fundamentals onboarding contract drifted; refusing broad gate changes")
    conditions[0]["value"] = 1
    finish = quest["conditions"]["AvailableForFinish"][0]
    finish["target"] = ["5449016a4bdc2d6f028b456f"]
    finish["value"] = TEST_HANDOVER_ROUBLES
    finish["onlyFoundInRaid"] = False
    path.write_text(json.dumps(quest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    prepare(Path(sys.argv[1]))
