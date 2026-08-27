#!/usr/bin/env python3
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

TRADER_ID = "d5c27bb3169f8dfbc13f6b69"
INTRO_QUEST_ID = "5d404ebd654de4efecef71d2"
REQUIRED_LOCALE_FIELDS = (
    "name", "description", "note", "startedMessageText", "successMessageText",
    "failMessageText", "acceptPlayerMessage", "declinePlayerMessage",
    "completePlayerMessage", "changeQuestMessageText",
)


def load_json(path: Path) -> Any:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def load_committed_quests(directory: Path, expected_ids: set[str] | None = None) -> dict[str, dict[str, Any]]:
    files = sorted(directory.glob("*.json"))
    quests: dict[str, dict[str, Any]] = {}
    for path in files:
        quest = load_json(path)
        if not isinstance(quest, dict):
            raise ValueError(f"quest file is not an object: {path}")
        qid = str(quest.get("_id") or "")
        if len(qid) != 24:
            raise ValueError(f"quest file has malformed _id: {path}: {qid}")
        if expected_ids is not None and qid not in expected_ids:
            continue
        if qid in quests:
            raise ValueError(f"duplicate committed quest id: {qid}")
        quests[qid] = quest
    return quests


def validate_runtime(committed: dict[str, dict[str, Any]], generated: dict[str, Any], english: dict[str, str], russian: dict[str, str]) -> None:
    generated_templates = generated.get("templates") or {}
    if set(committed) != set(generated_templates):
        missing = sorted(set(generated_templates) - set(committed))
        extra = sorted(set(committed) - set(generated_templates))
        raise ValueError(f"committed/generated quest id drift; missing={missing}; extra={extra}")

    for qid in sorted(committed):
        quest = committed[qid]
        if quest != generated_templates[qid]:
            raise ValueError(f"committed quest differs from compiler output: {qid}")
        if quest.get("traderId") != TRADER_ID:
            raise ValueError(f"quest {qid} trader id drift")

        finish = (quest.get("conditions") or {}).get("AvailableForFinish") or []
        expected_type = "HandoverItem" if qid == INTRO_QUEST_ID else "FindItem"
        if len(finish) != 1 or finish[0].get("conditionType") != expected_type:
            raise ValueError(f"quest {qid} must contain exactly one {expected_type} finish condition")
        if finish[0].get("onlyFoundInRaid") is not False:
            raise ValueError(f"quest {qid} unexpectedly requires FIR")

        reward_types = [reward.get("type") for reward in (quest.get("rewards") or {}).get("Success") or []]
        if "AssortmentUnlock" in reward_types:
            raise ValueError(f"quest {qid} leaked deferred assortment unlock")

        for field in REQUIRED_LOCALE_FIELDS:
            key = f"{qid} {field}"
            if key not in english:
                raise ValueError(f"English locale missing {key}")
            if key not in russian:
                raise ValueError(f"Russian locale missing {key}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Validate committed Access Protocol runtime against regenerated Access templates.")
    parser.add_argument("quest_dir", type=Path)
    parser.add_argument("generated_runtime", type=Path)
    parser.add_argument("english_locale", type=Path)
    parser.add_argument("russian_locale", type=Path)
    args = parser.parse_args()

    generated = load_json(args.generated_runtime)
    generated_templates = generated.get("templates") or {}
    expected_ids = {str(qid) for qid in generated_templates}
    committed = load_committed_quests(args.quest_dir, expected_ids)
    english = load_json(args.english_locale)
    russian = load_json(args.russian_locale)
    validate_runtime(committed, generated, english, russian)

    if len(committed) != 10:
        raise ValueError(f"expected 10 committed Access Protocol quests, got {len(committed)}")
    print(f"validated {len(committed)} committed Access Protocol quest templates")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
