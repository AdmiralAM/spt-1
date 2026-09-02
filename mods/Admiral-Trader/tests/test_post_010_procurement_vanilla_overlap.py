import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_field_expedient_supply_vanilla_overlap_is_real_but_not_semantic_duplication():
    operation = json.loads((ROOT / "manifests" / "post-010-procurement-operation.json").read_text(encoding="utf-8"))
    proof = json.loads((ROOT / "manifests" / "post-010-procurement-vanilla-overlap-proof.json").read_text(encoding="utf-8"))

    payload_tpls = [item["tpl"] for item in operation["operation"]["selectedPayload"]["items"]]
    assert payload_tpls == proof["payloadTpls"]
    assert proof["scope"] == "pinned-vanilla-demand-dataset"
    assert proof["runtimeMaterialize"] is False

    source = proof["source"]
    assert source["repository"] == "TarkovTracker/tarkovdata"
    assert source["commit"] == "1b9bc2acbea0e873244d1819cba9d9fe0f14e26c"
    assert set(source["paths"]) == {"quests.json", "hideout.json"}

    contract = proof["proofContract"]
    assert contract["wd40QuestObjectiveDetected"] is True
    assert contract["wd40QuestObjectiveType"] == "find"
    assert contract["wd40QuestObjectiveCount"] == 1
    assert contract["ductTapeQuestObjectiveDetected"] is False
    assert contract["sewingKitQuestObjectiveDetected"] is False
    assert contract["wd40HideoutDemandDetected"] is True
    assert contract["ductTapeHideoutDemandDetected"] is True
    assert contract["sewingKitHideoutDemandDetected"] is True
    assert contract["selectedPayloadVanillaDemandOverlapDetected"] is True
    assert contract["semanticDuplicateDetected"] is False
    assert contract["supplyFaucetDetected"] is False
    assert contract["competingAcquisitionPressureDetected"] is True
    assert contract["vanillaOverlapReviewSatisfied"] is True
    assert contract["payloadReplacementRequired"] is False
    assert contract["economyApprovalClaimed"] is False

    overlap = operation["operation"]["overlapCompatibility"]
    assert overlap["vanillaProof"] == "post-010-procurement-vanilla-overlap-proof.json"
    assert overlap["vanillaOverlapResolved"] is True
    assert overlap["scorpionOverlapResolved"] is True
    assert overlap["artemOverlapResolved"] is True
    assert overlap["externalVanillaScorpionArtemOverlapResolved"] is True

    gates = operation["gates"]
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is False
    assert gates["requiresEconomyAdmiralReview"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
