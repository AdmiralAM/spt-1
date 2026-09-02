import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_route_security_scorpion_artem_overlap_is_pinned_and_bounded():
    proof = json.loads((ROOT / "manifests" / "post-010-route-security-scorpion-artem-overlap-proof.json").read_text(encoding="utf-8"))

    assert proof["schemaVersion"] == 1
    assert proof["operationKey"] == "route-security"
    assert proof["status"] == "resolved-no-semantic-duplicate"
    assert proof["runtimeMaterialize"] is False

    shape = proof["operationShape"]
    assert shape["map"] == "Customs"
    assert shape["locationTarget"] == "bigmap"
    assert shape["maximumOrdinaryScavTargets"] <= 6
    assert shape["surviveOrExtractRequired"] is True
    assert shape["sameRaidCoupling"] == "fail-closed-separate-gate"

    scorpion = proof["sources"]["scorpion"]
    assert scorpion["repository"] == "acidphantasm/scorpion-csharp"
    assert scorpion["commit"] == "7d1bed766f859f227680377acc1e20c8f97b09bf"
    assert scorpion["ordinaryScavClearancePlusCustomsExtractionDuplicateFound"] is False
    assert scorpion["classification"] == "partial-component-overlap-only"

    artem = proof["sources"]["artem"]
    assert artem["repository"] == "WelcomeToThursday/WTT-Artem"
    assert artem["commit"] == "ee5b7c13a1980ff961ed0b2c1d8764574377b2bf"
    assert artem["ordinaryScavClearancePlusCustomsExtractionDuplicateFound"] is False
    assert artem["classification"] == "partial-component-overlap-only"

    decision = proof["decision"]
    assert decision["scorpionOverlapResolved"] is True
    assert decision["artemOverlapResolved"] is True
    assert decision["semanticDuplicateFound"] is False
    assert decision["vanillaOverlapStillRequired"] is True
    assert decision["aggregateExternalOverlapGateMayClose"] is False
    assert decision["sameRaidCouplingProofUnaffected"] is True
