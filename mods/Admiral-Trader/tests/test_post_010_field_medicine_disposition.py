import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "manifests" / "post-010-field-medicine-disposition.json"
CURATION = ROOT / "manifests" / "post-010-medical-capability-curation.json"

def load_manifest(): return json.loads(MANIFEST.read_text(encoding="utf-8"))
def load_curation(): return json.loads(CURATION.read_text(encoding="utf-8"))

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

def test_field_medicine_disposition_preserves_frozen_010_and_tracks_current_wave_without_rewriting_history():
    data = load_manifest(); sync = data["synchronization"]
    assert data["frozen010Base"] == "053a62ff5f1cb545f13bc89a96bba3acd319a823"
    assert data["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
    assert sync["completed"] is True and sync["completedAtomically"] is True
    assert sync["removedFromAuthoredOperations"] is True and sync["removedFromCampaignProgression"] is True and sync["removedFromRewardEnvelope"] is True
    assert sync["historicalDispositionWaveCount"] == 19
    assert sync["currentActiveAuthoredWaveCount"] == 15
    assert sync["chemicalSupportHistoricalPrerequisiteReauthoringSuperseded"] is True
    assert sync["currentChemicalSupportDisposition"] == "REJECT-VANILLA-SEMANTIC-COLLISION"

def test_field_stabilization_and_recovery_are_one_deferred_capability_not_parallel_quests():
    data = load_manifest(); consolidation = data["candidateConsolidation"]
    curation = load_curation()
    assert consolidation["decision"] == "MERGE-PARALLEL-LABELS-INTO-SINGLE-DEFERRED-CAPABILITY"
    assert consolidation["canonicalConcept"] == "field-medicine-under-pressure"
    assert set(consolidation["retiredIndependentCandidateIds"]) == {"MED-FIELD-01", "MED-RECOVERY-01"}
    assert consolidation["independentQuestCountAfterConsolidation"] == 1
    assert curation["admissionPolicy"]["currentIndependentCandidateCount"] == 1
    assert curation["admissionPolicy"]["secondOrdinaryMedicineQuestRequiresDistinctExactMechanic"] is True
    assert len(curation["authoredCandidates"]) == 1
    candidate = curation["authoredCandidates"][0]
    assert candidate["id"] == "MED-FIELD-01" and candidate["operationKey"] == "field-medicine-under-pressure"
    assert set(candidate["absorbedWorkingNames"]) == {"Field Stabilization", "Recovery Protocol"}
    merged = {item["workingName"]: item for item in curation["consolidatedCandidates"]}
    assert merged["Recovery Protocol"]["decision"] == "MERGED-NOT-INDEPENDENT"
    assert merged["Field Stabilization"]["decision"] == "MERGED-NOT-INDEPENDENT"

def test_field_medicine_does_not_reopen_rejected_chemical_support():
    data = load_manifest(); retained = data["retainedProductMeaning"]
    curation = load_curation()
    assert "permanently rejected" in retained["chemicalSupportBoundary"]
    assert "Stimulants must not substitute" in retained["chemicalSupportBoundary"]
    assert curation["crossAuthoritySynchronization"]["controlledPharmacologyMayBeResurrectedByMechanicProof"] is False
    rejected = {item["id"]: item for item in curation["rejectedCandidates"]}
    assert rejected["MED-PHARM-01"]["decision"] == "REJECT-VANILLA-SEMANTIC-COLLISION"
