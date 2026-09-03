import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"

def load(name):
    return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))

def test_relationship_standing_static_readiness_closes_every_nonphysical_gate_without_enabling_runtime():
    readiness = load("relationship-standing-static-readiness-proof.json")
    uplift = load("relationship-standing-stock-uplift.json")
    visibility = load("relationship-standing-profile-visibility-proof.json")
    relationship = load("relationship-stock.json")

    assert readiness["frozen010Base"] == "053a62ff5f1cb545f13bc89a96bba3acd319a823"
    resolved = readiness["resolvedStaticGates"]
    assert all(resolved.values())
    assert visibility["proofResult"]["profileScopedCurrentStandingVisibleToAssortProjection"] is True
    assert visibility["proofResult"]["serverSeamStaticallyProven"] is True
    assert uplift["materializationArchitecture"]["proofState"] == "profile-scoped-post-route-seam-proven-implemented-fail-closed-on-pinned-spt-4.1.3"
    assert uplift["materializationArchitecture"]["assortMutationContract"]["proofState"] == "implemented-and-regression-locked"
    assert uplift["economyReview"]["decision"] == "approved-with-bounds"
    assert relationship["standingUpliftState"]["economyEnvelopeApproved"] is True
    assert relationship["standingUpliftState"]["profileScopedProjectionImplemented"] is True
    assert relationship["standingUpliftState"]["profileLevelAndStandingResolverImplemented"] is True

    remaining = readiness["remainingGate"]
    assert remaining["type"] == "batched-physical-runtime-proof"
    assert remaining["requestedNow"] is False
    assert "no cross-profile" in remaining["scope"]
    assert len(remaining["diagnosticBoundaries"]) == 5

    decision = readiness["decision"]
    assert decision["additionalStaticRelationshipWorkRequiredBeforePhysicalCheckpoint"] is False
    assert decision["runtimeMaterializationEnabled"] is False
    assert decision["physicalCheckpointActivated"] is False
    assert decision["frozen010Changed"] is False
    assert relationship["standingUpliftState"]["runtimeMaterializationEnabled"] is False
    assert readiness["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
