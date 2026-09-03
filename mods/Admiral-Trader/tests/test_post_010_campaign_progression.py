import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"
def load(name: str): return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))

def test_post_010_campaign_progression_is_complete_reachable_and_acyclic():
    graph = load("post-010-campaign-progression.json"); rewards = load("post-010-operation-reward-envelope.json")
    assert graph["implementationAllowed"] is False and graph["runtimeMaterialize"] is False
    assert graph["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
    phase_ops = [op for phase in graph["phases"] for op in phase["operations"]]; expected = set(rewards["operationBands"])
    assert len(phase_ops) == 16 and len(set(phase_ops)) == 16 and set(phase_ops) == expected
    assert set(graph["prerequisites"]) == expected
    for deferred in ("expedition-discipline", "field-medicine-under-pressure", "controlled-chemical-support", "high-value-target-window", "night-signal-disruption"):
        assert deferred not in expected
    assert graph["prerequisites"]["endurance-circuit"] == ["expedition-loadout", "route-security"]
    phase_index = {op: i for i, phase in enumerate(graph["phases"]) for op in phase["operations"]}
    for operation, prerequisites in graph["prerequisites"].items():
        assert len(prerequisites) <= graph["designRules"]["maximumDirectPrerequisitesPerOperation"]
        assert operation not in prerequisites
        for prerequisite in prerequisites:
            assert prerequisite in expected and phase_index[prerequisite] <= phase_index[operation]
    roots = {op for op, prerequisites in graph["prerequisites"].items() if not prerequisites}; assert roots
    visiting=set(); visited=set()
    def visit(operation):
        if operation in visiting: raise AssertionError(f"cycle detected at {operation}")
        if operation in visited: return
        visiting.add(operation)
        for prerequisite in graph["prerequisites"][operation]: visit(prerequisite)
        visiting.remove(operation); visited.add(operation)
    for operation in expected: visit(operation)
    assert visited == expected
    critical={"labs-security-disruption"}; assert set(graph["phases"][-1]["operations"]) == critical
    for operation in critical:
        prerequisites=graph["prerequisites"][operation]; assert len(prerequisites)==2
        parent_branches={branch for branch,members in graph["branchSemantics"].items() if any(parent in members for parent in prerequisites)}
        assert len(parent_branches)>=2

def test_post_010_campaign_has_concrete_non_regressing_player_level_placement():
    graph=load("post-010-campaign-progression.json"); rewards=load("post-010-operation-reward-envelope.json"); trajectory=load("relationship-standing-trajectory.json")
    levels=graph["operationLevelPlacement"]; expected=set(rewards["operationBands"])
    assert set(levels)==expected and all(isinstance(level,int) and level>=1 for level in levels.values())
    phase_minimums={phase["key"]:phase["minimumPlayerLevel"] for phase in graph["phases"]}
    assert phase_minimums=={"field-readiness":15,"operational-integration":20,"specialist-operations":28,"command-grade-operations":35}
    for phase in graph["phases"]: assert all(levels[operation]>=phase["minimumPlayerLevel"] for operation in phase["operations"])
    for operation,prerequisites in graph["prerequisites"].items():
        for prerequisite in prerequisites: assert levels[prerequisite]<=levels[operation]
    loyalty_minimums={tier["loyaltyLevel"]:tier["minimumPlayerLevel"] for tier in trajectory["tiers"]}
    assert loyalty_minimums[2]==15 and loyalty_minimums[3]==25 and loyalty_minimums[4]==35
    assert min(levels.values())==loyalty_minimums[2]; assert levels["labs-security-disruption"]>loyalty_minimums[4]
    contracts=graph["progressionContracts"]; assert contracts["operationCount"]==16
    assert contracts["questLevelsReviewedAgainstFrozenAdmiralLoyaltyThresholds"] is True
    assert contracts["everyOperationHasConcretePlayerLevel"] is True and contracts["prerequisitesNeverRequireHigherPlayerLevelThanDependentOperation"] is True
    assert graph["materializationGate"]["requiresFinalQuestLevelPlacement"] is False

def test_post_010_progression_does_not_turn_loyalty_or_sales_into_quest_gates():
    graph=load("post-010-campaign-progression.json"); rules=graph["designRules"]
    assert rules["salesVolumeGateAllowed"] is False and rules["repeatableGateAllowed"] is False and rules["legacyQuestCountGateAllowed"] is False
    assert rules["mandatoryAllBranchesBeforeNextPhaseAllowed"] is False and rules["storefrontUnlockRequiredForProgression"] is False
    assert rules["standingMaySupportLoyaltyButMustNotReplaceAuthoredPrerequisites"] is True
    gate=graph["materializationGate"]; assert gate["requiresExactSpt413ConditionProofPerOperation"] is True and gate["requiresOverlapAuditPerOperation"] is True
    assert gate["requiresEconomyAdmiralRewardReview"] is True and gate["requiresPhysicalMilestoneOnlyAfterAWholeMaterializationWaveIsReady"] is True
