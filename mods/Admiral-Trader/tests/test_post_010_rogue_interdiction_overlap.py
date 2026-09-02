import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_rogue_interdiction_external_overlap_is_resolved_but_strong():
    proof = load_json(ROOT / "manifests" / "post-010-rogue-interdiction-overlap-proof.json")
    assert proof["schemaVersion"] == 1
    assert proof["operationKey"] == "rogue-interdiction"
    assert proof["status"] == "resolved-strong-partial-overlap-no-exact-duplicate"
    assert proof["runtimeMaterialize"] is False

    shape = proof["operationShape"]
    assert shape["map"] == "Lighthouse"
    assert shape["target"] == "Savage"
    assert shape["savageRole"] == ["exUsec"]
    assert shape["maximumRogueTargets"] <= 6
    assert shape["surviveOrExtractRequired"] is True

    sources = proof["sources"]
    assert sources["vanilla"]["commit"] == "1b9bc2acbea0e873244d1819cba9d9fe0f14e26c"
    assert sources["scorpion"]["commit"] == "7d1bed766f859f227680377acc1e20c8f97b09bf"
    assert sources["scorpion"]["observedQuest"] == "EVENT: Making Noise"
    assert "Eliminate any targets in the Lighthouse region" in sources["scorpion"]["observedObjectives"]
    assert "Survive and extract from the location" in sources["scorpion"]["observedObjectives"]
    assert sources["artem"]["commit"] == "ee5b7c13a1980ff961ed0b2c1d8764574377b2bf"
    assert sources["artem"]["successfulExtractionObjectiveFoundForThisQuest"] is False

    decision = proof["decision"]
    assert decision["vanillaOverlapResolved"] is True
    assert decision["scorpionOverlapResolved"] is True
    assert decision["artemOverlapResolved"] is True
    assert decision["semanticDuplicateFound"] is False
    assert decision["classification"] == "strong-partial-overlap-requires-target-specific-differentiation"
    assert decision["sameRaidCouplingProofUnaffected"] is True


def test_rogue_interdiction_overlap_closes_only_external_gate():
    spec = load_json(ROOT / "manifests" / "post-010-rogue-interdiction-operation.json")
    proof = load_json(ROOT / "manifests" / spec["overlapAuthority"])
    gates = spec["gates"]
    bounds = spec["bounds"]

    assert spec["schemaVersion"] == 2
    assert spec["externalOverlapReview"]["status"] == proof["status"]
    assert spec["externalOverlapReview"]["semanticDuplicateFound"] is False
    assert bounds["maximumRogueTargets"] <= 6
    assert bounds["genericTargetFallbackAllowed"] is False
    assert bounds["repeatable"] is False
    assert bounds["escalatingSequelsAllowed"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is False
    assert gates["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
