import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_field_expedient_supply_scorpion_overlap_subgate_is_pinned_and_bounded():
    operation = json.loads((ROOT / "manifests" / "post-010-procurement-operation.json").read_text(encoding="utf-8"))
    proof = json.loads((ROOT / "manifests" / "post-010-procurement-scorpion-overlap-proof.json").read_text(encoding="utf-8"))

    payload_tpls = [item["tpl"] for item in operation["operation"]["selectedPayload"]["items"]]
    assert payload_tpls == proof["payloadTpls"]
    assert proof["scope"] == "pinned-scorpion-csharp-only"
    assert proof["runtimeMaterialize"] is False

    source = proof["source"]
    assert source["repository"] == "acidphantasm/scorpion-csharp"
    assert source["branch"] == "master"
    assert source["commit"] == "7d1bed766f859f227680377acc1e20c8f97b09bf"

    contract = proof["proofContract"]
    assert contract["wd40TplMatchCount"] == 0
    assert contract["ductTapeTplMatchCount"] == 0
    assert contract["sewingKitTplMatchCount"] == 0
    assert contract["selectedPayloadExactTplOverlapDetected"] is False
    assert contract["scorpionExactTplOverlapReviewSatisfied"] is True
    assert contract["vanillaOverlapClaimed"] is False
    assert contract["artemOverlapClaimed"] is False
    assert contract["economyApprovalClaimed"] is False

    # The aggregate external gate must remain fail-closed until vanilla and Artem are resolved too.
    gates = operation["gates"]
    overlap = operation["operation"]["overlapCompatibility"]
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert overlap["externalVanillaScorpionArtemOverlapResolved"] is False
    assert gates["requiresEconomyAdmiralReview"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
