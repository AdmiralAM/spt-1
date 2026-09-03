import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_command_window_selects_one_exact_glukhar_target_on_reserve():
    spec = load_json(ROOT / "manifests" / "post-010-high-value-target-operation.json")
    role_proof = load_json(ROOT / "manifests" / spec["conditionAuthority"]["roleProof"])
    location_proof = load_json(ROOT / "manifests" / spec["conditionAuthority"]["locationProof"])
    target = spec["selectedTarget"]
    gates = spec["gates"]

    assert spec["schemaVersion"] == 1
    assert spec["operationKey"] == "high-value-target-window"
    assert target["friendlyName"] == "Glukhar"
    assert target["target"] == "Savage"
    assert target["savageRole"] == ["bossGluhar"]
    assert target["locationTarget"] == "RezervBase"
    assert target["maximumTargets"] == 1
    assert target["maximumSuccessfulRaids"] == 1
    assert target["repeatable"] is False

    boss = next(row for row in role_proof["proven"] if row["gameplayRole"] == "specific-boss")
    assert "bossGluhar" in boss["savageRoleExamples"]
    assert "high-value-target-window" in boss["admiralUse"]
    assert location_proof["source"]["friendlyName"] == "Reserve"
    assert location_proof["source"]["targetName"] == "RezervBase"

    assert gates["requiresFinalBossSelection"] is False
    assert gates["requiresExactBossRoleProof"] is False
    assert gates["requiresExactReserveLocationSelection"] is False
    assert gates["requiresEconomyAdmiralReview"] is False
    assert gates["requiresFrozenCampaignOverlapReview"] is True
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False


def test_command_window_reward_matches_campaign_and_critical_envelope():
    spec = load_json(ROOT / "manifests" / "post-010-high-value-target-operation.json")
    campaign = load_json(ROOT / "manifests" / "post-010-campaign-progression.json")
    envelope = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    review = spec["economyReview"]

    assert review["campaignPlayerLevel"] == campaign["operationLevelPlacement"]["high-value-target-window"] == 35
    assert review["riskBand"] == envelope["operationBands"]["high-value-target-window"] == "critical"
    assert review["authoredReward"] == envelope["operationRewards"]["high-value-target-window"]
    reward = review["authoredReward"]
    band = envelope["bands"]["critical"]
    assert band["xpMin"] <= reward["xp"] <= band["xpMax"]
    assert band["rubMin"] <= reward["rub"] <= band["rubMax"]
    assert reward["standing"] <= band["standingMax"]
    assert reward["itemReward"] is None


def test_command_window_does_not_change_frozen_runtime_counts():
    spec = load_json(ROOT / "manifests" / "post-010-high-value-target-operation.json")
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))

    assert spec["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
