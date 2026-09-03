import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def load_json(path: Path): return json.loads(path.read_text(encoding="utf-8"))

def test_chemical_support_is_deferred_without_fake_stimulant_semantics():
    spec = load_json(ROOT / "manifests" / "post-010-chemical-support-operation.json")
    assert spec["schemaVersion"] == 3
    assert spec["status"] == "deferred-out-of-current-wave-pending-exact-stimulant-use-authority"
    assert spec["source"]["bundle"] == "Stims Proficiency" and spec["source"]["directPortAllowed"] is False
    decision=spec["decision"]
    assert decision["currentWaveAccepted"] is False and decision["materializeAsQuest"] is False and decision["permanentConceptRejection"] is False
    assert decision["genericItemUseInferenceAllowed"] is False
    assert decision["stimulantHandoverFallbackAllowed"] is False and decision["firStimulantCollectionFallbackAllowed"] is False
    assert decision["stimulantPossessionFallbackAllowed"] is False and decision["genericSurviveExtractFallbackAllowed"] is False
    assert decision["questCountPreservationIsNotARequirement"] is True
    stimulant=spec["conditionReadiness"]["stimulantUse"]
    assert stimulant["status"] == "unproven-fail-closed" and stimulant["mayInferFromGenericItemUse"] is False
    gate=spec["gates"]
    assert gate["implementationAllowed"] is False and gate["runtimeMaterialize"] is False and gate["physicalTestRequired"] is False
    assert gate["requiresExactSpt413StimulantUseConditionProof"] is True and gate["deferredLifecycleRewardMilestoneUnaffected"] is True

def test_chemical_support_is_removed_from_active_progression_and_rewards():
    spec=load_json(ROOT / "manifests" / "post-010-chemical-support-operation.json")
    graph=load_json(ROOT / "manifests" / "post-010-campaign-progression.json")
    rewards=load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    active={op for phase in graph["phases"] for op in phase["operations"]}
    assert "controlled-chemical-support" not in active
    assert "controlled-chemical-support" not in graph["prerequisites"] and "controlled-chemical-support" not in graph["operationLevelPlacement"]
    assert "controlled-chemical-support" not in rewards["operationBands"] and "controlled-chemical-support" not in rewards["operationRewards"]
    sync=spec["synchronization"]
    assert sync["removedFromActiveCampaign"] is True and sync["removedFromRewardEnvelope"] is True
    assert sync["activeAuthoredWaveCount"] == 18 and sync["replacementQuestCreated"] is False and sync["rewardEscalationCreated"] is False

def test_chemical_support_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))
    assert len(quest_files) == 31 and len(assort["loyal_level_items"]) == 11 and len(assort["barter_scheme"]) == 11
