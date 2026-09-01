import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_post_010_procurement_operation_is_bounded_and_non_materialized():
    data = json.loads((ROOT / "manifests" / "post-010-procurement-operation.json").read_text(encoding="utf-8"))
    op = data["operation"]
    bounds = op["bounds"]
    gates = data["gates"]

    assert data["schemaVersion"] == 2
    assert data["status"] == "post-0.1.0-authored-spec-only"
    assert data["source"]["bundle"] == "Errand Boy"
    assert data["source"]["legacyQuestCount"] == 920
    assert data["source"]["decision"] == "rewrite-theme-only"

    for field in ("title", "description", "started", "success"):
        assert op[field]["en"].strip()
        assert op[field]["ru"].strip()

    assert 1 <= bounds["maximumDistinctItemTpls"] <= 3
    assert 1 <= bounds["maximumTotalHandedOverUnits"] <= 6
    assert bounds["maximumSuccessfulCompletions"] == 1
    assert bounds["repeatable"] is False
    assert bounds["escalatingSequelsAllowed"] is False
    assert bounds["genericRequestedItemQueueAllowed"] is False
    assert bounds["containerRewardLadderAllowed"] is False
    assert bounds["storefrontUnlockAllowed"] is False
    assert bounds["repairableWeaponsArmorHelmetsAllowed"] is True

    compat = op["handoverCompatibility"]
    assert compat["repairablePayloadAllowed"] is True
    assert compat["fullRangeDurabilityBounds"] == {"minDurability": 0, "maxDurability": 100}
    assert compat["exactTplSelectionStillRequired"] is True
    assert compat["exactSpt413ConditionProofStillRequired"] is False
    assert compat["conditionProof"] == "post-010-handover-durability-proof.json"
    assert compat["sourceReference"]["repository"] == "laurentmekka/AndrudisQuestManiac"
    assert compat["sourceReference"]["commit"] == "58c3dd0487858c7ba8e8c053b873fbe76a222637"
    assert "3423" in compat["sourceReference"]["finding"]
    assert "maxDurability=0" in compat["legacyRisk"]

    proof = json.loads((ROOT / "manifests" / compat["conditionProof"]).read_text(encoding="utf-8"))
    contract = proof["provenContract"]
    impact = proof["fieldExpedientSupplyImpact"]
    assert proof["runtimeMaterialize"] is False
    assert contract["conditionType"] == "HandoverItem"
    assert contract["fullUsableDurabilityRange"] == {"minDurability": 0, "maxDurability": 100}
    assert contract["maxDurabilityZeroIsNotFullRange"] is True
    assert contract["repairableWeaponArmorHelmetPayloadMayUseFullRangeBounds"] is True
    assert impact["exactSpt413HandoverConditionProofSatisfied"] is True
    assert impact["repairablePayloadNoLongerMechanicallyExcludedByDurabilitySemantics"] is True
    assert impact["admissionStillRequiresExactTplSelection"] is True
    assert impact["admissionStillRequiresOverlapAudit"] is True
    assert impact["admissionStillRequiresEconomyAdmiralReview"] is True

    rewards = op["rewardDoctrine"]
    assert rewards["permanentItemSupplyAllowed"] is False
    assert rewards["containerMilestoneRewardAllowed"] is False
    assert rewards["capabilityUnlockAllowed"] is False
    assert rewards["requiresEconomyAdmiralReview"] is True

    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
    assert gates["requiresExactSpt413HandoverConditionProof"] is False
    assert gates["requiresExactItemTplSelection"] is True
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["requiresEconomyAdmiralReview"] is True
    assert gates["requiresFrozenCampaignOverlapReview"] is True

    assert data["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }
