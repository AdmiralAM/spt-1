import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_expedition_loadout_is_bounded_and_non_materialized():
    spec = load_json(ROOT / "manifests" / "post-010-expedition-loadout-operation.json")
    op = spec["operation"]
    bounds = op["bounds"]
    rewards = op["rewardDoctrine"]
    gates = spec["gates"]

    assert spec["source"]["bundles"] == ["Deep Pockets", "Tarkov Mule"]
    assert spec["source"]["legacyQuestCount"] == 150
    assert spec["source"]["directPortAllowed"] is False
    assert op["key"] == "expedition-loadout"
    assert op["title"]["en"] and op["title"]["ru"]
    assert op["description"]["en"] and op["description"]["ru"]
    assert op["started"]["en"] and op["started"]["ru"]
    assert op["success"]["en"] and op["success"]["ru"]

    assert bounds["maximumDistinctEquipmentRequirements"] <= 3
    assert bounds["maximumSuccessfulRaids"] == 1
    assert bounds["repeatable"] is False
    assert bounds["handoverAllowed"] is False
    assert bounds["firCollectionAllowed"] is False
    assert bounds["perItemQuestCopiesAllowed"] is False
    assert bounds["equipmentClassLadderAllowed"] is False

    assert rewards["durableEquipmentRewardAllowed"] is False
    assert rewards["containerRewardAllowed"] is False
    assert rewards["permanentEquipmentStorefrontUnlockAllowed"] is False

    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
    assert gates["requiresExactSpt413EquipmentPossessionOrWearProof"] is True
    assert gates["requiresExactSpt413SurvivedExtractionProof"] is True
    assert gates["requiresFinalEquipmentTplSelection"] is True
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["requiresFrozenCampaignOverlapReview"] is True
    assert gates["requiresEconomyAdmiralReview"] is True

    assert spec["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }


def test_expedition_loadout_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))

    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
