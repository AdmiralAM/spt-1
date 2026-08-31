import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_single_raid_kill_reset_semantics_are_narrow_and_fail_closed():
    proof = load_json(ROOT / "manifests" / "post-010-session-reset-proof.json")

    assert proof["runtimeMaterialize"] is False
    assert proof["frozen010Base"] == "053a62ff5f1cb545f13bc89a96bba3acd319a823"

    single_raid = proof["proven"]["singleRaidKillQuota"]
    assert single_raid["conditionType"] == "CounterCreator -> Kills"
    assert single_raid["field"] == "resetOnSessionEnd"
    assert single_raid["value"] is True

    unproven = proof["explicitlyNotProven"]
    assert unproven["oneSessionOnly"]["supported"] is False
    assert unproven["killThenExtractSameRaidCoupling"]["supported"] is False
    assert unproven["genericActionReset"]["supported"] is False

    rules = proof["materializationRules"]
    assert rules["resetOnSessionEndTrueAllowedOnlyForPureKillQuota"] is True
    assert rules["oneSessionOnlyMustRemainFalse"] is True
    assert rules["sameRaidKillThenExtractMustRemainFailClosed"] is True
    assert rules["noNewFrozen010QuestJson"] is True


def test_session_reset_proof_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))

    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
