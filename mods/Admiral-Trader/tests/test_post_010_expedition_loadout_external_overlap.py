import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_expedition_external_overlap_is_resolved_without_materialization():
    proof = load_json(ROOT / "manifests" / "post-010-expedition-loadout-external-overlap-proof.json")
    assert proof["schemaVersion"] == 1
    assert proof["operationKey"] == "expedition-loadout"
    assert proof["status"] == "resolved-partial-overlap-no-semantic-duplicate"
    assert proof["runtimeMaterialize"] is False

    pair = proof["operationShape"]["requiredPair"]
    assert [(item["slot"], item["tpl"]) for item in pair] == [
        ("TacticalVest", "572b7adb24597762ae139821"),
        ("Backpack", "56e33680d2720be2748b4576"),
    ]
    assert proof["operationShape"]["handoverRequired"] is False
    assert proof["operationShape"]["successfulExtractionRequired"] is True

    sources = proof["sources"]
    assert sources["vanilla"]["commit"] == "1b9bc2acbea0e873244d1819cba9d9fe0f14e26c"
    assert sources["vanilla"]["observedQuest"] == "Setup"
    assert sources["vanilla"]["selectedPairDuplicateFound"] is False
    assert sources["scorpion"]["commit"] == "7d1bed766f859f227680377acc1e20c8f97b09bf"
    assert sources["scorpion"]["observedQuest"] == "EVENT: That's Hot"
    assert sources["scorpion"]["selectedPairDuplicateFound"] is False
    assert sources["artem"]["commit"] == "ee5b7c13a1980ff961ed0b2c1d8764574377b2bf"
    assert sources["artem"]["selectedPairDuplicateFound"] is False

    decision = proof["decision"]
    assert decision["semanticDuplicateFound"] is False
    assert decision["externalOverlapGateMayClose"] is True
    assert decision["multiGroupBooleanProofUnaffected"] is True
    assert decision["sameRaidCouplingProofUnaffected"] is True


def test_expedition_external_overlap_closes_only_external_gate():
    spec = load_json(ROOT / "manifests" / "post-010-expedition-loadout-operation.json")
    proof = load_json(ROOT / "manifests" / spec["proofAuthority"]["externalOverlap"])
    gates = spec["gates"]

    assert proof["decision"]["semanticDuplicateFound"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is False
    assert gates["requiresMultiGroupEquipmentBooleanProof"] is True
    assert gates["requiresSameRaidEquipmentAndExtractionCouplingProof"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
