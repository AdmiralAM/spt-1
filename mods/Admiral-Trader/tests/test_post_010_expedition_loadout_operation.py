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
    authority = spec["proofAuthority"]

    assert spec["schemaVersion"] == 3
    assert spec["source"]["bundles"] == ["Deep Pockets", "Tarkov Mule"]
    assert spec["source"]["legacyQuestCount"] == 150
    assert spec["source"]["directPortAllowed"] is False
    assert op["key"] == "expedition-loadout"
    assert op["title"]["en"] and op["title"]["ru"]
    assert op["description"]["en"] and op["description"]["ru"]
    assert op["started"]["en"] and op["started"]["ru"]
    assert op["success"]["en"] and op["success"]["ru"]

    assert authority["equipmentCondition"] == "post-010-player-equipment-proof.json"
    assert authority["survivedExtraction"] == "post-010-pmc-location-extraction-proof.json"
    assert "explicit validated item TPL" in authority["equipmentBoundary"]
    assert "same-raid" in authority["extractionBoundary"]

    selected = op["selectedEquipment"]
    assert selected["selectionState"] == "selected-product-reviewed"
    assert selected["requiredPair"] == [
        {
            "slot": "TacticalVest",
            "tpl": "572b7adb24597762ae139821",
            "name": "Scav Vest",
            "rationale": "Unarmored, low-tier chest rig; the objective tests carrying capacity and preparation rather than armor class or purchasing power.",
        },
        {
            "slot": "Backpack",
            "tpl": "56e33680d2720be2748b4576",
            "name": "Scav backpack",
            "rationale": "Ordinary low-tier backpack with enough field utility to represent expedition preparation without turning the objective into a premium-gear tax.",
        },
    ]
    assert selected["exactPairRequired"] is True
    assert selected["alternativesAllowedAtMaterialization"] is False
    assert selected["categoryInferenceAllowed"] is False
    assert selected["armorOrDurabilityPredicateAllowed"] is False
    assert selected["handoverRequired"] is False
    assert selected["consumedOnCompletion"] is False

    assert op["campaignPlacement"] == {
        "playerLevel": 15,
        "phase": "field-readiness",
        "prerequisites": [],
    }
    assert op["reward"] == {
        "band": "standard",
        "xp": 6500,
        "rub": 38000,
        "standing": 0.009,
        "itemReward": None,
        "economyReview": "approved-no-escalation",
        "rationale": "The selected gear is not surrendered or consumed and no item/storefront reward is granted. The existing early-campaign standard reward therefore needs no acquisition-cost reimbursement or gear-faucet escalation.",
    }

    assert bounds["maximumDistinctEquipmentRequirements"] == 2
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
    assert gates["requiresExactSpt413EquipmentPossessionOrWearProof"] is False
    assert gates["requiresExactSpt413SurvivedExtractionProof"] is False
    assert gates["requiresMultiGroupEquipmentBooleanProof"] is True
    assert gates["requiresSameRaidEquipmentAndExtractionCouplingProof"] is True
    assert gates["requiresFinalEquipmentTplSelection"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["requiresFrozenCampaignOverlapReview"] is True
    assert gates["requiresEconomyAdmiralReview"] is False

    assert spec["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }


def test_expedition_loadout_matches_campaign_and_reward_authority():
    spec = load_json(ROOT / "manifests" / "post-010-expedition-loadout-operation.json")
    campaign = load_json(ROOT / "manifests" / "post-010-campaign-progression.json")
    rewards = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")

    key = spec["operation"]["key"]
    assert spec["operation"]["campaignPlacement"]["playerLevel"] == campaign["operationLevelPlacement"][key]
    assert spec["operation"]["campaignPlacement"]["prerequisites"] == campaign["prerequisites"][key]
    assert spec["operation"]["reward"]["band"] == rewards["operationBands"][key]
    for field in ("xp", "rub", "standing", "itemReward"):
        assert spec["operation"]["reward"][field] == rewards["operationRewards"][key][field]


def test_expedition_loadout_proof_authorities_exist_and_remain_fail_closed():
    spec = load_json(ROOT / "manifests" / "post-010-expedition-loadout-operation.json")
    authority = spec["proofAuthority"]

    equipment_proof = load_json(ROOT / "manifests" / authority["equipmentCondition"])
    extraction_proof = load_json(ROOT / "manifests" / authority["survivedExtraction"])

    assert equipment_proof["proven"]["playerEquipmentConditionFamilyExists"]["conditionType"] == "Equipment"
    assert equipment_proof["proven"]["equippedOnlyMode"]["value"] is False
    assert equipment_proof["materializationRules"]["multiGroupSemanticsMustRemainFailClosed"] is True
    assert equipment_proof["materializationRules"]["sameRaidEquipmentThenExtractMustRemainFailClosed"] is True
    assert extraction_proof["materializationRules"]["sameRaidCouplingMustRemainFailClosed"] is True


def test_expedition_loadout_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))

    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
