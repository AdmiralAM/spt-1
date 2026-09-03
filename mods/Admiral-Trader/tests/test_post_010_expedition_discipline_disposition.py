import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]; MANIFESTS=ROOT/"manifests"
def load(name:str): return json.loads((MANIFESTS/name).read_text(encoding="utf-8"))

def test_expedition_discipline_is_rejected_and_synchronization_is_complete():
    disposition=load("post-010-expedition-discipline-disposition.json"); decision=disposition["decision"]; gate=disposition["runtimeGate"]; sync=disposition["synchronization"]
    assert disposition["schemaVersion"]==1 and disposition["operationKey"]=="expedition-discipline"
    assert disposition["status"]=="rejected-as-standalone-absorbed-into-expedition-loadout"
    assert decision["standaloneOperationAccepted"] is False and decision["materializeAsQuest"] is False and decision["absorbedByOperation"]=="expedition-loadout"
    assert decision["unsupportedWeightProxyAllowed"] is False and decision["genericSurviveRaidFallbackAllowed"] is False and decision["backpackHandoverFallbackAllowed"] is False and decision["equipmentChecklistFallbackAllowed"] is False
    assert sync=={"completed":True,"removedFromAuthoredOperations":True,"removedFromCampaignProgression":True,"enduranceCircuitPrerequisiteRewiredToExpeditionLoadout":True,"removedFromRewardEnvelope":True,"authoredWaveCount":20,"completedAtomically":True}
    assert gate["implementationAllowed"] is False and gate["runtimeMaterialize"] is False and gate["physicalTestRequired"] is False and gate["deferredLifecycleRewardMilestoneUnaffected"] is True

def test_rejected_key_is_absent_from_all_active_wave_authorities():
    authored=load("post-010-authored-operations.json"); graph=load("post-010-campaign-progression.json"); rewards=load("post-010-operation-reward-envelope.json"); loadout=load("post-010-expedition-loadout-operation.json")
    authored_keys={operation["key"] for operation in authored["operations"]}
    assert "expedition-discipline" not in authored_keys and "expedition-discipline" not in graph["prerequisites"] and "expedition-discipline" not in rewards["operationBands"] and "expedition-discipline" not in rewards["operationRewards"]
    assert graph["prerequisites"]["endurance-circuit"]==["expedition-loadout","route-security"]
    assert loadout["operation"]["key"]=="expedition-loadout" and loadout["gates"]["runtimeMaterialize"] is False

def test_frozen_runtime_boundary_is_untouched():
    disposition=load("post-010-expedition-discipline-disposition.json"); assort=load("../db/assort.json"); quests=list((ROOT/"db"/"quests").glob("*.json"))
    assert disposition["frozenBoundary"]=={"questCount":31,"rootOfferCount":11,"relationshipRuntimeOffers":0}; assert len(quests)==31; assert len(assort["loyal_level_items"])==11
