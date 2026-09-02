import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_route_security_vanilla_overlap_is_pinned_and_only_partial():
    proof = load_json(ROOT / "manifests" / "post-010-route-security-vanilla-overlap-proof.json")
    assert proof["schemaVersion"] == 1
    assert proof["operationKey"] == "route-security"
    assert proof["status"] == "resolved-no-semantic-duplicate"
    assert proof["runtimeMaterialize"] is False

    source = proof["source"]
    assert source["repository"] == "TarkovTracker/tarkovdata"
    assert source["commit"] == "1b9bc2acbea0e873244d1819cba9d9fe0f14e26c"
    assert source["path"] == "quests.json"
    assert "not SPT evaluator authority" in source["role"]

    comparators = {row["quest"]: row for row in proof["observedComparatorShapes"]}
    assert comparators["Operation Aquarius - Part 2"]["targetCount"] == 15
    assert comparators["Operation Aquarius - Part 2"]["surviveOrExtractObjective"] is False
    assert comparators["Polikhim Hobo"]["targetCount"] == 25
    assert comparators["Polikhim Hobo"]["surviveOrExtractObjective"] is False
    assert comparators["The Punisher - Part 3"]["targetCount"] == 25
    assert comparators["The Punisher - Part 3"]["surviveOrExtractObjective"] is False
    assert comparators["Big customer"]["ordinaryScavEliminationObjective"] is False

    decision = proof["decision"]
    assert decision["vanillaOverlapResolved"] is True
    assert decision["semanticDuplicateFound"] is False
    assert decision["classification"] == "partial-component-overlap-only"
    assert decision["aggregateVanillaScorpionArtemOverlapResolved"] is True
    assert decision["sameRaidCouplingProofUnaffected"] is True


def test_route_security_aggregate_external_gate_closes_without_opening_runtime():
    spec = load_json(ROOT / "manifests" / "post-010-route-security-operation.json")
    review = spec["operation"]["externalOverlapReview"]
    gates = spec["gates"]

    assert review["status"] == "resolved-partial-component-overlap-only"
    assert review["vanilla"] == "resolved-no-semantic-duplicate"
    assert review["scorpion"] == "resolved-no-semantic-duplicate"
    assert review["artem"] == "resolved-no-semantic-duplicate"
    assert review["semanticDuplicateFound"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is False
    assert gates["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
