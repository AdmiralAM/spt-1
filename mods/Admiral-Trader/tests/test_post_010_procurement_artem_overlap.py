import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_field_expedient_supply_artem_overlap_is_explicit_and_semantically_bounded():
    operation = json.loads((ROOT / "manifests" / "post-010-procurement-operation.json").read_text(encoding="utf-8"))
    proof = json.loads((ROOT / "manifests" / "post-010-procurement-artem-overlap-proof.json").read_text(encoding="utf-8"))

    payload_tpls = [item["tpl"] for item in operation["operation"]["selectedPayload"]["items"]]
    assert payload_tpls == proof["payloadTpls"]
    assert proof["scope"] == "pinned-wtt-artem-only"
    assert proof["runtimeMaterialize"] is False

    source = proof["source"]
    assert source["repository"] == "WelcomeToThursday/WTT-Artem"
    assert source["branch"] == "main"
    assert source["commit"] == "ee5b7c13a1980ff961ed0b2c1d8764574377b2bf"

    contract = proof["proofContract"]
    assert contract["wd40TplMatchCount"] == 0
    assert contract["ductTapeTplMatchCount"] == 0
    assert set(contract["sewingKitMatchLocations"]) == {
        "Resources/db/assort.json",
        "Resources/db/CustomQuests/66bf757f27d0b097db0acea5/Quests/ArtemQuests.json",
    }
    assert contract["selectedPayloadExactTplOverlapDetected"] is True
    assert contract["overlapItemTpl"] == "61bf83814088ec1a363d7097"
    assert contract["artemQuestName"] == "Expanding Wardrobe"
    assert contract["artemQuestHandoverCount"] == 1
    assert contract["artemQuestOnlyFoundInRaid"] is False
    assert contract["semanticDuplicateDetected"] is False
    assert contract["supplyFaucetDetected"] is False
    assert contract["competingSinkDetected"] is True
    assert contract["artemOverlapReviewSatisfied"] is True
    assert contract["payloadReplacementRequired"] is False
    assert contract["vanillaOverlapClaimed"] is False
    assert contract["economyApprovalClaimed"] is False

    overlap = operation["operation"]["overlapCompatibility"]
    assert overlap["artemProof"] == "post-010-procurement-artem-overlap-proof.json"
    assert overlap["artemOverlapResolved"] is True
    assert overlap["scorpionOverlapResolved"] is True
    assert overlap["vanillaOverlapResolved"] is False
    assert overlap["externalVanillaScorpionArtemOverlapResolved"] is False

    gates = operation["gates"]
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["requiresEconomyAdmiralReview"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
