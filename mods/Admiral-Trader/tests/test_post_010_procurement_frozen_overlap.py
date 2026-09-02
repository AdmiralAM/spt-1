import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_field_expedient_supply_does_not_overlap_frozen_runtime_surfaces():
    operation = json.loads((ROOT / "manifests" / "post-010-procurement-operation.json").read_text(encoding="utf-8"))
    proof = json.loads((ROOT / "manifests" / "post-010-procurement-frozen-overlap-proof.json").read_text(encoding="utf-8"))
    payload_tpls = [item["tpl"] for item in operation["operation"]["selectedPayload"]["items"]]
    assert payload_tpls == proof["payloadTpls"]
    assert proof["scope"] == "frozen-admiral-runtime-only"
    assert proof["runtimeMaterialize"] is False

    quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
    assert len(quest_files) == proof["expectedFrozenBoundary"]["questCount"] == 31
    runtime_surfaces = quest_files + [ROOT / "db" / "assort.json", ROOT / "db" / "questassort.json"]
    for path in runtime_surfaces:
        text = path.read_text(encoding="utf-8")
        for tpl in payload_tpls:
            assert tpl not in text, f"selected procurement TPL {tpl} unexpectedly overlaps frozen runtime surface {path.name}"

    assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
    root_items = [item for item in assort["items"] if item.get("parentId") == "hideout"]
    assert len(root_items) == proof["expectedFrozenBoundary"]["rootOfferCount"] == 11

    contract = proof["proofContract"]
    assert contract["selectedPayloadAbsentFromFrozenQuestConditionsAndRewards"] is True
    assert contract["selectedPayloadAbsentFromFrozenAssort"] is True
    assert contract["selectedPayloadAbsentFromFrozenQuestAssort"] is True
    assert contract["frozenCampaignSemanticDuplicationDetected"] is False
    assert contract["frozenCampaignOverlapReviewSatisfied"] is True
    assert contract["economyApprovalClaimed"] is False

    gates = operation["gates"]
    assert gates["requiresFrozenCampaignOverlapReview"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is False
    assert gates["requiresEconomyAdmiralReview"] is True
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
