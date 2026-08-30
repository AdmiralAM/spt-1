import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_route_security_operation_is_bounded_and_non_materialized():
    spec = load_json(ROOT / "manifests" / "post-010-route-security-operation.json")
    op = spec["operation"]
    bounds = op["bounds"]
    gates = spec["gates"]

    assert spec["source"]["bundle"] == "Scav Hunt"
    assert spec["source"]["legacyQuestCount"] == 60
    assert op["key"] == "route-security"
    assert op["title"]["en"] and op["title"]["ru"]
    assert op["description"]["en"] and op["description"]["ru"]
    assert op["started"]["en"] and op["started"]["ru"]
    assert op["success"]["en"] and op["success"]["ru"]

    assert bounds["maximumScavTargets"] <= 6
    assert bounds["maximumSuccessfulRaids"] == 1
    assert bounds["locationBound"] is True
    assert bounds["surviveOrExtractRequired"] is True
    assert bounds["repeatable"] is False
    assert bounds["escalatingSequelsAllowed"] is False
    assert bounds["perMapCopiesAllowed"] is False
    assert bounds["fourDigitKillCountsAllowed"] is False

    assert op["rewardDoctrine"]["permanentScavGearSupplyAllowed"] is False
    assert op["rewardDoctrine"]["capabilityUnlockAllowed"] is False
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
    assert gates["requiresExactSpt413ScavTargetConditionProof"] is True
    assert gates["requiresExactSpt413SurvivedExtractionProof"] is True
    assert gates["requiresExactLocationSelection"] is True
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["requiresFrozenCampaignOverlapReview"] is True
    assert gates["requiresEconomyAdmiralReview"] is True

    frozen = spec["frozenBoundary"]
    assert frozen == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }


def test_route_security_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_dir = ROOT / "db" / "quests"
    quest_files = list(quest_dir.glob("*.json"))

    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
