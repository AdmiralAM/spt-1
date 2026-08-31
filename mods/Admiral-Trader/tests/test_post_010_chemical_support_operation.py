import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_chemical_support_operation_is_bounded_and_non_materialized():
    spec = load_json(ROOT / "manifests" / "post-010-chemical-support-operation.json")
    op = spec["operation"]
    bounds = op["bounds"]
    rewards = op["rewardDoctrine"]
    gates = spec["gates"]

    assert spec["schemaVersion"] == 2
    assert spec["source"]["bundle"] == "Stims Proficiency"
    assert spec["source"]["legacyQuestCount"] == 16
    assert spec["source"]["directPortAllowed"] is False
    assert op["key"] == "controlled-chemical-support"
    assert op["title"]["en"] and op["title"]["ru"]
    assert op["description"]["en"] and op["description"]["ru"]
    assert op["started"]["en"] and op["started"]["ru"]
    assert op["success"]["en"] and op["success"]["ru"]

    assert bounds["maximumRequiredStimulantUses"] == 1
    assert bounds["maximumSuccessfulRaids"] == 1
    assert bounds["repeatable"] is False
    assert bounds["perStimCopiesAllowed"] is False
    assert bounds["consumptionLadderAllowed"] is False
    assert bounds["handoverAllowed"] is False
    assert bounds["firCollectionAllowed"] is False

    assert rewards["renewableStimulantSupplyAllowed"] is False
    assert rewards["permanentStimulantStorefrontUnlockAllowed"] is False
    assert rewards["highValueConsumableFaucetAllowed"] is False

    extraction = spec["conditionReadiness"]["survivedExtraction"]
    stimulant_use = spec["conditionReadiness"]["stimulantUse"]
    assert extraction["status"] == "proven-shape-only"
    assert extraction["authorityManifest"] == "post-010-pmc-location-extraction-proof.json"
    assert extraction["conditionShape"] == ["Location", "ExitStatus(Survived)"]
    assert extraction["sameRaidStimulantUseCouplingProven"] is False
    assert "does not prove" in extraction["boundary"].lower()
    assert stimulant_use["status"] == "unproven-fail-closed"
    assert stimulant_use["mayInferFromGenericItemUse"] is False

    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
    assert gates["requiresExactSpt413StimulantUseConditionProof"] is True
    assert gates["requiresExactSpt413SurvivedExtractionProof"] is False
    assert gates["requiresSameRaidStimulantUseToExtractionProof"] is True
    assert gates["requiresFinalApprovedStimTplSelection"] is True
    assert gates["requiresDistinctFieldConditionSelection"] is True
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["requiresFrozenCampaignOverlapReview"] is True
    assert gates["requiresEconomyAdmiralReview"] is True

    assert spec["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }


def test_chemical_support_extraction_readiness_matches_authoritative_proof():
    spec = load_json(ROOT / "manifests" / "post-010-chemical-support-operation.json")
    proof = load_json(ROOT / "manifests" / spec["conditionReadiness"]["survivedExtraction"]["authorityManifest"])
    extraction = proof["proven"]["survivedExtraction"]

    assert "controlled-chemical-support" in extraction["affectedOperations"]
    assert extraction["conditionTypes"] == ["Location", "ExitStatus"]
    assert extraction["status"] == ["Survived"]
    assert proof["notProvenByThisSlice"]["killThenExtractSameRaidCoupling"] is True


def test_chemical_support_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))

    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
