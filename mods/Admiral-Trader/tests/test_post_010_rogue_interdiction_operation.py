import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_rogue_interdiction_role_location_and_bounds_are_exact():
    spec = load_json(ROOT / "manifests" / "post-010-rogue-interdiction-operation.json")
    authority = spec["conditionAuthority"]
    gates = spec["gates"]

    assert spec["schemaVersion"] == 1
    assert spec["operationKey"] == "rogue-interdiction"
    assert spec["runtimeMaterialize"] is False
    assert authority["role"] == {"target": "Savage", "savageRole": ["exUsec"], "status": "proven-pinned-spt413"}
    assert authority["locationSource"]["repository"] == "sp-tarkov/server-csharp"
    assert authority["locationSource"]["commit"] == "fe74b07c6361e2c6d1532dc21ba1c3b981d88d93"
    assert authority["locationSource"]["map"] == "Lighthouse"
    assert authority["locationSource"]["locationTarget"] == "Lighthouse"
    assert authority["subLocationOrWaterTreatmentZoneRequired"] is False

    role_proof = load_json(ROOT / "manifests" / authority["roleProof"])
    rogue = next(row for row in role_proof["proven"] if row["gameplayRole"] == "rogue")
    assert rogue["target"] == authority["role"]["target"]
    assert rogue["savageRole"] == authority["role"]["savageRole"]
    assert "rogue-interdiction" in rogue["admiralUse"]

    assert spec["bounds"]["maximumRogueTargets"] <= 6
    assert spec["bounds"]["maximumSuccessfulRaids"] == 1
    assert spec["bounds"]["repeatable"] is False
    assert spec["bounds"]["locationFreeFallbackAllowed"] is False
    assert gates["requiresExactRogueRoleProof"] is False
    assert gates["requiresExactLighthouseLocationSelection"] is False
    assert gates["requiresSurvivedExtractionShapeProof"] is False
    assert gates["requiresSubLocationOrZoneProof"] is False
    assert gates["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False


def test_rogue_interdiction_reward_is_inside_high_risk_envelope():
    spec = load_json(ROOT / "manifests" / "post-010-rogue-interdiction-operation.json")
    envelope = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    campaign = load_json(ROOT / "manifests" / "post-010-campaign-progression.json")
    policy = load_json(ROOT / "manifests" / "reward-policy.json")
    review = spec["economyReview"]

    assert review["status"] == "approved-static"
    assert review["campaignPlayerLevel"] == campaign["operationLevelPlacement"]["rogue-interdiction"] == 31
    assert review["riskBand"] == envelope["operationBands"]["rogue-interdiction"] == "high-risk"
    assert review["authoredReward"] == envelope["operationRewards"]["rogue-interdiction"]

    reward = review["authoredReward"]
    band = envelope["bands"]["high-risk"]
    assert band["xpMin"] <= reward["xp"] <= band["xpMax"]
    assert band["rubMin"] <= reward["rub"] <= band["rubMax"]
    assert reward["standing"] <= band["standingMax"]
    assert reward["itemReward"] is None

    elimination = policy["observedReference"]["questTypeMedians"]["Elimination"]
    overall = policy["observedReference"]["overall"]
    assert review["vanillaComparatorMedian"] == {"xp": elimination["xp"], "rub": elimination["rub"]}
    assert reward["xp"] < elimination["xp"]
    assert reward["rub"] > elimination["rub"]
    assert reward["rub"] < overall["rub"]["p75"]
    assert review["overallVanillaStandingMedian"] == overall["standing"]["median"]
    assert reward["standing"] < overall["standing"]["median"]
    assert spec["gates"]["requiresEconomyAdmiralReview"] is False


def test_rogue_interdiction_does_not_change_frozen_runtime_counts():
    spec = load_json(ROOT / "manifests" / "post-010-rogue-interdiction-operation.json")
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))

    assert spec["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
