import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_hostile_operator_intercept_rejects_streets_and_fails_closed_on_reserve_replacement():
    spec = load_json(ROOT / "manifests" / "post-010-hostile-operator-intercept-operation.json")
    authority = spec["conditionAuthority"]
    gates = spec["gates"]
    decision = spec["productDecision"]

    assert spec["schemaVersion"] == 2
    assert spec["operationKey"] == "hostile-operator-intercept"
    assert authority["target"] == "AnyPmc"
    assert authority["factionSplitRequired"] is False
    assert decision["rejectedVariant"]["locationTarget"] == "TarkovStreets"
    assert "Trouble in the Big City" in decision["rejectedVariant"]["reason"]
    assert decision["replacementVariant"]["map"] == "Reserve"
    assert decision["replacementVariant"]["locationTargetCandidate"] == "RezervBase"
    assert authority["locationTargetCandidate"] == "RezervBase"
    assert authority["locationSelectionStatus"] == "fail-closed-pending-pinned-spt413-proof"
    assert spec["bounds"]["maximumPmcTargets"] <= 4
    assert spec["bounds"]["maximumSuccessfulRaids"] == 1
    assert spec["bounds"]["repeatable"] is False
    assert spec["bounds"]["perMapCopiesAllowed"] is False
    assert spec["bounds"]["factionKillLadderAllowed"] is False
    assert gates["requiresAnyPmcTargetProof"] is False
    assert gates["requiresExactReserveLocationSelection"] is True
    assert gates["requiresSurvivedExtractionShapeProof"] is False
    assert gates["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert gates["requiresFrozenCampaignOverlapReview"] is True
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False


def test_hostile_operator_intercept_reward_envelope_remains_conservative():
    spec = load_json(ROOT / "manifests" / "post-010-hostile-operator-intercept-operation.json")
    campaign = load_json(ROOT / "manifests" / "post-010-campaign-progression.json")
    envelope = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    policy = load_json(ROOT / "manifests" / "reward-policy.json")
    review = spec["economyReview"]

    assert review["campaignPlayerLevel"] == campaign["operationLevelPlacement"]["hostile-operator-intercept"] == 23
    assert review["riskBand"] == envelope["operationBands"]["hostile-operator-intercept"] == "high-risk"
    assert review["authoredReward"] == envelope["operationRewards"]["hostile-operator-intercept"]
    reward = review["authoredReward"]
    band = envelope["bands"]["high-risk"]
    assert band["xpMin"] <= reward["xp"] <= band["xpMax"]
    assert band["rubMin"] <= reward["rub"] <= band["rubMax"]
    assert reward["standing"] <= band["standingMax"]
    assert reward["itemReward"] is None
    elimination = policy["observedReference"]["questTypeMedians"]["Elimination"]
    assert reward["xp"] < elimination["xp"]
    assert reward["rub"] < elimination["rub"]
    assert reward["standing"] < policy["observedReference"]["overall"]["standing"]["median"]
    assert spec["gates"]["requiresEconomyAdmiralReview"] is False


def test_hostile_operator_intercept_preserves_frozen_counts():
    spec = load_json(ROOT / "manifests" / "post-010-hostile-operator-intercept-operation.json")
    assert spec["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
    assert len(list((ROOT / "db" / "quests").glob("*.json"))) == 31
