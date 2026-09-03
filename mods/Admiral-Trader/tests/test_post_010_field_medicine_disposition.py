import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "manifests" / "post-010-field-medicine-disposition.json"

def load_manifest(): return json.loads(MANIFEST.read_text(encoding="utf-8"))

def test_field_medicine_is_fail_closed_without_exact_treatment_authority():
    data = load_manifest(); decision = data["decision"]; runtime = data["runtimeGate"]
    assert data["operationKey"] == "field-medicine-under-pressure"
    assert data["status"] == "deferred-out-of-current-wave-pending-exact-treatment-authority"
    assert decision["currentWaveAccepted"] is False and decision["materializeAsQuest"] is False and decision["permanentConceptRejection"] is False
    assert decision["unsupportedHealthEffectInferenceAllowed"] is False and decision["unsupportedUseItemInferenceAllowed"] is False
    assert decision["medicalItemHandoverFallbackAllowed"] is False and decision["firMedicalCollectionFallbackAllowed"] is False
    assert decision["consumableCountFallbackAllowed"] is False and decision["genericSurviveExtractFallbackAllowed"] is False and decision["stimulantSubstitutionAllowed"] is False
    assert decision["questCountPreservationIsNotARequirement"] is True
    assert runtime["implementationAllowed"] is False and runtime["runtimeMaterialize"] is False and runtime["physicalTestRequired"] is False

def test_field_medicine_disposition_preserves_frozen_010_and_is_atomically_synchronized():
    data = load_manifest(); sync = data["synchronization"]
    assert data["frozen010Base"] == "053a62ff5f1cb545f13bc89a96bba3acd319a823"
    assert data["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
    assert sync["completed"] is True and sync["completedAtomically"] is True
    assert sync["removedFromAuthoredOperations"] is True and sync["removedFromCampaignProgression"] is True and sync["removedFromRewardEnvelope"] is True
    assert sync["controlledChemicalSupportPrerequisiteReauthored"] is True and sync["controlledChemicalSupportPrerequisite"] == []
    assert sync["authoredWaveCount"] == 19

def test_field_medicine_remains_distinct_from_chemical_support():
    data = load_manifest(); retained = data["retainedProductMeaning"]
    assert "ordinary field medicine" in retained["separationFromChemicalSupport"]
    assert "no handover/FIR/use-count ladder" in retained["futureConcept"]
