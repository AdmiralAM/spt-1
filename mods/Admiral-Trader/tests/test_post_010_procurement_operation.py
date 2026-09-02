import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_post_010_procurement_operation_is_bounded_and_non_materialized():
    data = json.loads((ROOT / "manifests" / "post-010-procurement-operation.json").read_text(encoding="utf-8"))
    op = data["operation"]
    bounds = op["bounds"]
    gates = data["gates"]

    assert data["schemaVersion"] == 4
    assert data["status"] == "post-0.1.0-authored-spec-only"
    assert data["source"]["bundle"] == "Errand Boy"
    assert data["source"]["legacyQuestCount"] == 920
    assert data["source"]["decision"] == "rewrite-theme-only"

    for field in ("title", "description", "started", "success"):
        assert op[field]["en"].strip()
        assert op[field]["ru"].strip()

    payload = op["selectedPayload"]
    assert payload["status"] == "selected-pending-external-overlap-and-economy-review"
    assert payload["distinctTpls"] == 3
    assert payload["totalUnits"] == 6
    assert [(item["tpl"], item["count"]) for item in payload["items"]] == [
        ("590c5bbd86f774785762df04", 2),
        ("57347c1124597737fb1379e3", 2),
        ("61bf83814088ec1a363d7097", 2),
    ]
    assert len({item["tpl"] for item in payload["items"]}) == payload["distinctTpls"]
    assert sum(item["count"] for item in payload["items"]) == payload["totalUnits"]
    doctrine = payload["selectionDoctrine"]
    assert doctrine["ordinaryFieldServiceOnly"] is True
    assert doctrine["repairableEquipmentSelected"] is False
    assert doctrine["rareMilitaryComponentSelected"] is False
    assert doctrine["highValueElectronicsSelected"] is False
    assert doctrine["permanentStorefrontUnlockImplied"] is False
    assert doctrine["selectionDoesNotConstituteEconomyApproval"] is True

    assert 1 <= bounds["maximumDistinctItemTpls"] <= 3
    assert payload["distinctTpls"] <= bounds["maximumDistinctItemTpls"]
    assert 1 <= bounds["maximumTotalHandedOverUnits"] <= 6
    assert payload["totalUnits"] <= bounds["maximumTotalHandedOverUnits"]
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
    assert compat["exactTplSelectionStillRequired"] is False
    assert compat["selectedPayloadUsesRepairableEquipment"] is False
    assert compat["exactSpt413ConditionProofStillRequired"] is False
    assert compat["conditionProof"] == "post-010-handover-durability-proof.json"

    overlap = op["overlapCompatibility"]
    assert overlap["frozenCampaignProof"] == "post-010-procurement-frozen-overlap-proof.json"
    assert overlap["frozenCampaignOverlapResolved"] is True
    assert overlap["externalVanillaScorpionArtemOverlapResolved"] is False

    rewards = op["rewardDoctrine"]
    assert rewards["permanentItemSupplyAllowed"] is False
    assert rewards["containerMilestoneRewardAllowed"] is False
    assert rewards["capabilityUnlockAllowed"] is False
    assert rewards["requiresEconomyAdmiralReview"] is True

    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
    assert gates["requiresExactSpt413HandoverConditionProof"] is False
    assert gates["requiresExactItemTplSelection"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert gates["requiresEconomyAdmiralReview"] is True
    assert gates["requiresFrozenCampaignOverlapReview"] is False

    assert data["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }
