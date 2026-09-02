import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_hostile_operator_intercept_rejects_streets_and_pins_reserve_replacement():
    spec = load_json(ROOT / "manifests" / "post-010-hostile-operator-intercept-operation.json")
    authority = spec["conditionAuthority"]
    gates = spec["gates"]
    decision = spec["productDecision"]
    location_proof = load_json(ROOT / "manifests" / "post-010-reserve-location-proof.json")
    frozen_proof = load_json(ROOT / "manifests" / "post-010-hostile-operator-intercept-frozen-overlap-proof.json")
    external_proof = load_json(ROOT / "manifests" / "post-010-hostile-operator-intercept-external-overlap-proof.json")

    assert spec["schemaVersion"] == 4
    assert spec["operationKey"] == "hostile-operator-intercept"
    assert authority["target"] == "AnyPmc"
    assert authority["factionSplitRequired"] is False
    assert decision["rejectedVariant"]["locationTarget"] == "TarkovStreets"
    assert "Trouble in the Big City" in decision["rejectedVariant"]["reason"]
    assert decision["replacementVariant"]["map"] == "Reserve"
    assert decision["replacementVariant"]["locationTarget"] == "RezervBase"
    assert authority["locationProof"] == "post-010-reserve-location-proof.json"
    assert authority["locationTarget"] == "RezervBase"
    assert authority["locationSelectionStatus"] == "resolved-pinned-upstream-authority"
    assert authority["externalOverlapProof"] == "post-010-hostile-operator-intercept-external-overlap-proof.json"
    assert location_proof["source"]["commit"] == "9b05502ac7cd3872251ee1e9b67a5f3d541e04c0"
    assert location_proof["source"]["friendlyName"] == "Reserve"
    assert location_proof["source"]["targetName"] == "RezervBase"
    assert frozen_proof["status"] == "resolved-no-frozen-reserve-pmc-semantic-overlap"
    assert frozen_proof["operationShape"]["locationTarget"] == "RezervBase"
    assert frozen_proof["semanticDuplicateFound"] is False
    assert external_proof["status"] == "resolved-no-external-semantic-duplicate"
    assert external_proof["operationShape"]["target"] == "AnyPmc"
    assert external_proof["operationShape"]["locationTarget"] == "RezervBase"
    assert external_proof["operationShape"]["maximumPmcTargets"] <= 4
    assert external_proof["decision"]["aggregateExternalOverlapResolved"] is True
    assert external_proof["decision"]["semanticDuplicateFound"] is False
    assert external_proof["sources"]["scorpion"]["commit"] == "7d1bed766f859f227680377acc1e20c8f97b09bf"
    assert external_proof["sources"]["artem"]["commit"] == "ee5b7c13a1980ff961ed0b2c1d8764574377b2bf"
    assert spec["bounds"]["maximumPmcTargets"] <= 4
    assert spec["bounds"]["maximumSuccessfulRaids"] == 1
    assert spec["bounds"]["repeatable"] is False
    assert spec["bounds"]["perMapCopiesAllowed"] is False
    assert spec["bounds"]["factionKillLadderAllowed"] is False
    assert gates["requiresAnyPmcTargetProof"] is False
    assert gates["requiresExactReserveLocationSelection"] is False
    assert gates["requiresSurvivedExtractionShapeProof"] is False
    assert gates["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert gates["requiresFrozenCampaignOverlapReview"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is False
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
