import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def load_json(path: Path): return json.loads(path.read_text(encoding="utf-8"))

def test_command_window_is_deferred_after_vanilla_collision_without_retarget_filler():
    spec = load_json(ROOT / "manifests" / "post-010-high-value-target-operation.json")
    assert spec["schemaVersion"] == 3
    assert spec["operationKey"] == "high-value-target-window"
    assert spec["status"] == "post-0.1.0-deferred-out-of-current-wave"
    assert spec["activeAuthoredWave"] is False
    assert spec["replacementRequired"] is False
    target = spec["selectedTarget"]
    assert target["friendlyName"] == "Glukhar"
    assert target["savageRole"] == ["bossGluhar"]
    assert target["locationTarget"] == "RezervBase"
    assert target["selectionStatus"] == "rejected-vanilla-semantic-collision"
    review = spec["externalOverlapReview"]
    assert review["vanilla"]["quest"] == "Payback"
    assert review["vanilla"]["classification"] == "semantic-superset-of-rejected-admiral-shape"
    assert review["scorpion"]["status"] == "not-required-for-deferred-operation"
    assert review["artem"]["status"] == "not-required-for-deferred-operation"
    gates = spec["gates"]
    assert gates["requiresDistinctCommandDisruptionMechanicBeforeReadmission"] is True
    assert gates["requiresExactSpt413ConditionProofAfterRedesign"] is True
    assert gates["requiresOverlapAuditAfterRedesign"] is True
    assert gates["requiresEconomyAdmiralReviewAfterRedesign"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False

def test_command_window_is_absent_from_active_campaign_and_reward_authorities():
    campaign = load_json(ROOT / "manifests" / "post-010-campaign-progression.json")
    envelope = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    authored = load_json(ROOT / "manifests" / "post-010-authored-operations.json")
    key = "high-value-target-window"
    assert key not in campaign["operationLevelPlacement"]
    assert key not in campaign["prerequisites"]
    assert all(key not in phase["operations"] for phase in campaign["phases"])
    assert key not in envelope["operationBands"]
    assert key not in envelope["operationRewards"]
    assert key not in {op["key"] for op in authored["operations"]}
    assert campaign["progressionContracts"]["operationCount"] == 17
    assert envelope["campaignCaps"]["operationCount"] == 17
    assert envelope["campaignCaps"]["maximumAuthoredStandingAllocation"] == 0.232

def test_command_window_disposition_does_not_change_frozen_runtime_counts():
    spec = load_json(ROOT / "manifests" / "post-010-high-value-target-operation.json")
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))
    assert spec["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
