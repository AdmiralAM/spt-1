import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"


def load(name: str):
    return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))


def test_post_010_campaign_progression_is_complete_reachable_and_acyclic():
    graph = load("post-010-campaign-progression.json")
    rewards = load("post-010-operation-reward-envelope.json")

    assert graph["implementationAllowed"] is False
    assert graph["runtimeMaterialize"] is False
    assert graph["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }

    phase_ops = [op for phase in graph["phases"] for op in phase["operations"]]
    expected = set(rewards["operationBands"])
    assert len(phase_ops) == 21
    assert len(set(phase_ops)) == 21
    assert set(phase_ops) == expected
    assert set(graph["prerequisites"]) == expected

    phase_index = {
        op: index
        for index, phase in enumerate(graph["phases"])
        for op in phase["operations"]
    }
    for operation, prerequisites in graph["prerequisites"].items():
        assert len(prerequisites) <= graph["designRules"]["maximumDirectPrerequisitesPerOperation"]
        assert operation not in prerequisites
        for prerequisite in prerequisites:
            assert prerequisite in expected
            assert phase_index[prerequisite] <= phase_index[operation]

    roots = {op for op, prerequisites in graph["prerequisites"].items() if not prerequisites}
    assert roots

    visiting = set()
    visited = set()

    def visit(operation: str):
        if operation in visiting:
            raise AssertionError(f"cycle detected at {operation}")
        if operation in visited:
            return
        visiting.add(operation)
        for prerequisite in graph["prerequisites"][operation]:
            visit(prerequisite)
        visiting.remove(operation)
        visited.add(operation)

    for operation in expected:
        visit(operation)
    assert visited == expected

    def reaches_root(operation: str, seen=None):
        seen = set() if seen is None else seen
        if operation in roots:
            return True
        if operation in seen:
            return False
        seen.add(operation)
        return any(reaches_root(parent, seen.copy()) for parent in graph["prerequisites"][operation])

    assert all(reaches_root(operation) for operation in expected)

    critical = {"high-value-target-window", "labs-security-disruption"}
    assert set(graph["phases"][-1]["operations"]) == critical
    for operation in critical:
        prerequisites = graph["prerequisites"][operation]
        assert len(prerequisites) == 2
        parent_branches = {
            branch
            for branch, members in graph["branchSemantics"].items()
            if any(parent in members for parent in prerequisites)
        }
        assert len(parent_branches) >= 2


def test_post_010_progression_does_not_turn_loyalty_or_sales_into_quest_gates():
    graph = load("post-010-campaign-progression.json")
    rules = graph["designRules"]
    assert rules["salesVolumeGateAllowed"] is False
    assert rules["repeatableGateAllowed"] is False
    assert rules["legacyQuestCountGateAllowed"] is False
    assert rules["mandatoryAllBranchesBeforeNextPhaseAllowed"] is False
    assert rules["storefrontUnlockRequiredForProgression"] is False
    assert rules["standingMaySupportLoyaltyButMustNotReplaceAuthoredPrerequisites"] is True

    gate = graph["materializationGate"]
    assert gate["requiresExactSpt413ConditionProofPerOperation"] is True
    assert gate["requiresOverlapAuditPerOperation"] is True
    assert gate["requiresEconomyAdmiralRewardReview"] is True
    assert gate["requiresPhysicalMilestoneOnlyAfterAWholeMaterializationWaveIsReady"] is True
