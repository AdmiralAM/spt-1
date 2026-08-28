#!/usr/bin/env python3
import argparse
import json
from pathlib import Path

EXPECTED_OFFER_COUNT = 11
EXPECTED_QUEST_COUNT = 31
EXPECTED_QUESTASSORT_STATES = {"started", "success", "fail"}
EXPECTED_MILESTONE_UNLOCKS = 7
REQUIRED_LOCALES = {
    "en.json",
    "ru.json",
    "gameplay-alpha-en.json",
    "gameplay-alpha-ru.json",
    "objectives-en.json",
    "objectives-ru.json",
}
RECOVERY_TOOL = Path("tools") / "Reset-AdmiralTraderProfile.ps1"


def fail(message: str) -> None:
    raise SystemExit(message)


def load_json(path: Path):
    if not path.is_file():
        fail(f"candidate tree missing required file: {path}")
    try:
        return json.loads(path.read_text(encoding="utf-8-sig"))
    except Exception as exc:
        fail(f"candidate tree contains invalid JSON at {path}: {exc}")


def validate(root: Path, require_enabled: bool) -> None:
    db = root / "db"
    manifests = root / "manifests"
    locales = db / "locales"
    quests = db / "quests"

    recovery = root / RECOVERY_TOOL
    if not recovery.is_file():
        fail(f"candidate tree missing backup-first profile recovery tool: {recovery}")
    recovery_text = recovery.read_text(encoding="utf-8-sig")
    for contract in (
        "[switch]$Apply",
        "Copy-Item $resolvedProfile $backupPath -Force",
        "Backup verification failed; profile was not modified.",
        "Expected exactly 31 canonical Admiral quest IDs",
    ):
        if contract not in recovery_text:
            fail(f"candidate profile recovery contract drift: missing {contract!r}")

    runtime = load_json(manifests / "runtime-manifest.json")
    assort = load_json(db / "assort.json")
    questassort = load_json(db / "questassort.json")

    if runtime.get("targetSptVersion") != "4.1.3":
        fail(f"candidate target drift: {runtime.get('targetSptVersion')} != 4.1.3")
    if require_enabled:
        if runtime.get("registrationEnabled") is not True:
            fail("staged candidate must set registrationEnabled=true")
        if runtime.get("publicationMode") != "test-candidate":
            fail(f"staged candidate publicationMode drift: {runtime.get('publicationMode')}")

    items = assort.get("items")
    barter = assort.get("barter_scheme")
    loyalty = assort.get("loyal_level_items")
    if not isinstance(items, list) or not isinstance(barter, dict) or not isinstance(loyalty, dict):
        fail("candidate assort native collections have invalid types")

    roots = [row for row in items if isinstance(row, dict) and row.get("parentId") == "hideout"]
    root_ids = [row.get("_id") for row in roots]
    if len(roots) != EXPECTED_OFFER_COUNT:
        fail(f"candidate must contain exactly {EXPECTED_OFFER_COUNT} Admiral root offers; found {len(roots)}")
    if len(set(root_ids)) != EXPECTED_OFFER_COUNT or any(not value for value in root_ids):
        fail("candidate assort root offer IDs are missing or duplicated")
    root_id_set = set(root_ids)
    if set(barter) != root_id_set or set(loyalty) != root_id_set:
        fail("candidate assort root/barter/loyalty key sets do not match exactly")

    if not isinstance(questassort, dict) or set(questassort) != EXPECTED_QUESTASSORT_STATES:
        fail(f"candidate questassort must use exact native lowercase states {sorted(EXPECTED_QUESTASSORT_STATES)}")
    for state in EXPECTED_QUESTASSORT_STATES:
        if not isinstance(questassort[state], dict):
            fail(f"candidate questassort.{state} must be an object")
    if questassort["started"] or questassort["fail"]:
        fail("candidate questassort started/fail mappings must remain empty")
    if len(questassort["success"]) != EXPECTED_MILESTONE_UNLOCKS:
        fail(f"candidate must contain exactly {EXPECTED_MILESTONE_UNLOCKS} milestone unlock mappings; found {len(questassort['success'])}")
    if not set(questassort["success"]).issubset(root_id_set):
        fail("candidate questassort.success references a non-offer ID")

    quest_files = sorted(quests.glob("*.json")) if quests.is_dir() else []
    if len(quest_files) != EXPECTED_QUEST_COUNT:
        fail(f"candidate must contain exactly {EXPECTED_QUEST_COUNT} current quest templates; found {len(quest_files)}")
    quest_ids = set()
    for path in quest_files:
        quest = load_json(path)
        quest_id = str(quest.get("_id") or "")
        if not quest_id or quest_id in quest_ids:
            fail(f"candidate quest template has missing/duplicate _id: {path}")
        quest_ids.add(quest_id)
    missing_unlock_quests = sorted(set(map(str, questassort["success"].values())) - quest_ids)
    if missing_unlock_quests:
        fail(f"candidate milestone unlocks reference missing quest templates: {missing_unlock_quests}")

    missing_locales = sorted(name for name in REQUIRED_LOCALES if not (locales / name).is_file())
    if missing_locales:
        fail(f"candidate is missing required EN/RU Gameplay Alpha/objective locale files: {missing_locales}")
    for name in REQUIRED_LOCALES:
        data = load_json(locales / name)
        if not isinstance(data, dict) or not data:
            fail(f"candidate locale is empty or not an object: {name}")

    print(
        "Admiral Trader candidate tree OK: "
        f"target=4.1.3 offers={EXPECTED_OFFER_COUNT} milestoneUnlocks={EXPECTED_MILESTONE_UNLOCKS} "
        f"quests={EXPECTED_QUEST_COUNT} locales={len(REQUIRED_LOCALES)} recovery=backup-first enabled={require_enabled}"
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Validate Admiral Trader Gameplay Alpha source/staged candidate composition.")
    parser.add_argument("root", type=Path, help="Admiral-Trader source or staged mod directory")
    parser.add_argument("--require-enabled", action="store_true", help="Require staged registrationEnabled=true/test-candidate publication mode")
    args = parser.parse_args()
    root = args.root.resolve()
    if not root.is_dir():
        fail(f"candidate tree root is not a directory: {root}")
    validate(root, args.require_enabled)


if __name__ == "__main__":
    main()
