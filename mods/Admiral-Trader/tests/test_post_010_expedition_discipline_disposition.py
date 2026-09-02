import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"


def load(name: str):
    return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))


def test_expedition_discipline_is_rejected_as_a_standalone_operation():
    disposition = load("post-010-expedition-discipline-disposition.json")
    decision = disposition["decision"]
    gate = disposition["runtimeGate"]

    assert disposition["schemaVersion"] == 1
    assert disposition["operationKey"] == "expedition-discipline"
    assert disposition["status"] == "rejected-as-standalone-absorbed-into-expedition-loadout"
    assert decision["standaloneOperationAccepted"] is False
    assert decision["materializeAsQuest"] is False
    assert decision["absorbedByOperation"] == "expedition-loadout"
    assert decision["unsupportedWeightProxyAllowed"] is False
    assert decision["genericSurviveRaidFallbackAllowed"] is False
    assert decision["backpackHandoverFallbackAllowed"] is False
    assert decision["equipmentChecklistFallbackAllowed"] is False
    assert decision["questCountPreservationIsNotARequirement"] is True
    assert gate["implementationAllowed"] is False
    assert gate["runtimeMaterialize"] is False
    assert gate["physicalTestRequired"] is False
    assert gate["deferredLifecycleRewardMilestoneUnaffected"] is True


def test_disposition_requires_atomic_wave_synchronization_instead_of_a_proxy_quest():
    disposition = load("post-010-expedition-discipline-disposition.json")
    sync = disposition["synchronizationRequired"]
    loadout = load("post-010-expedition-loadout-operation.json")
    authored = load("post-010-authored-operations.json")
    graph = load("post-010-campaign-progression.json")
    rewards = load("post-010-operation-reward-envelope.json")

    assert loadout["operation"]["key"] == "expedition-loadout"
    assert loadout["gates"]["runtimeMaterialize"] is False

    # Until the follow-up atomic synchronization commit lands, the old plan still
    # contains the rejected key. The disposition makes that state explicit rather
    # than silently treating an unsupported weight proxy as accepted product work.
    authored_keys = {operation["key"] for operation in authored["operations"]}
    assert "expedition-discipline" in authored_keys
    assert "expedition-discipline" in graph["prerequisites"]
    assert "expedition-discipline" in rewards["operationBands"]

    assert sync == {
        "removeFromAuthoredOperations": True,
        "removeFromCampaignProgression": True,
        "rewireEnduranceCircuitPrerequisiteToExpeditionLoadout": True,
        "removeFromRewardEnvelope": True,
        "reduceAuthoredWaveCountFrom21To20": True,
        "mustBeOneAtomicProductCommit": True,
    }


def test_frozen_runtime_boundary_is_untouched():
    disposition = load("post-010-expedition-discipline-disposition.json")
    assort = load("../db/assort.json")
    quests = list((ROOT / "db" / "quests").glob("*.json"))

    assert disposition["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }
    assert len(quests) == 31
    assert len(assort["loyal_level_items"]) == 11
