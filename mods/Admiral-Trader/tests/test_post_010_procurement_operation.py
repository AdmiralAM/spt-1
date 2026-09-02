import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def test_post_010_procurement_operation_is_bounded_and_non_materialized():
    data = json.loads((ROOT / "manifests" / "post-010-procurement-operation.json").read_text(encoding="utf-8"))
    op = data["operation"]
    bounds = op["bounds"]
    gates = data["gates"]

    assert data["schemaVersion"] == 6
    assert data["status"] == "post-0.1.0-authored-spec-only"
    assert data["source"] == {"bundle": "Errand Boy", "legacyQuestCount": 920, "decision": "rewrite-theme-only"}

    for field in ("title", "description", "started", "success"):
        assert op[field]["en"].strip()
        assert op[field]["ru"].strip()

    payload = op["selectedPayload"]
    assert payload["status"] == "selected-economy-reviewed"
    assert payload["distinctTpls"] == 3
    assert payload["totalUnits"] == 6
    assert [(item["tpl"], item["count"]) for item in payload["items"]] == [
        ("590c5bbd86f774785762df04", 2),
        ("57347c1124597737fb1379e3", 3),
        ("61bf83814088ec1a363d7097", 1),
    ]
    assert len({item["tpl"] for item in payload["items"]}) == payload["distinctTpls"]
    assert sum(item["count"] for item in payload["items"]) == payload["totalUnits"]

    doctrine = payload["selectionDoctrine"]
    assert doctrine["ordinaryFieldServiceOnly"] is True
    assert doctrine["repairableEquipmentSelected"] is False
    assert doctrine["rareMilitaryComponentSelected"] is False
    assert doctrine["highValueElectronicsSelected"] is False
    assert doctrine["permanentStorefrontUnlockImplied"] is False
    assert doctrine["selectionDoesNotConstituteEconomyApproval"] is False

    economy = op["economyReview"]
    assert economy["status"] == "approved-after-payload-rebalance"
    assert economy["previousPayload"]["handbookBurdenRub"] == 94914
    assert economy["approvedPayload"]["handbookBurdenRub"] == 59413
    assert economy["burdenReductionRub"] == 35501
    assert economy["rewardEnvelopeChanged"] is False
    assert economy["approvedReward"] == {"xp": 5000, "rub": 30000, "standing": 0.008, "itemReward": None}
    assert economy["runtimeEvidenceRequiredForThisEconomyDecision"] is False

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

    compat = op["handoverCompatibility"]
    assert compat["exactTplSelectionStillRequired"] is False
    assert compat["selectedPayloadUsesRepairableEquipment"] is False
    assert compat["exactSpt413ConditionProofStillRequired"] is False
    assert compat["conditionProof"] == "post-010-handover-durability-proof.json"

    overlap = op["overlapCompatibility"]
    assert overlap["frozenCampaignOverlapResolved"] is True
    assert overlap["scorpionOverlapResolved"] is True
    assert overlap["artemOverlapResolved"] is True
    assert overlap["vanillaOverlapResolved"] is True
    assert overlap["externalVanillaScorpionArtemOverlapResolved"] is True

    rewards = op["rewardDoctrine"]
    assert rewards["permanentItemSupplyAllowed"] is False
    assert rewards["containerMilestoneRewardAllowed"] is False
    assert rewards["capabilityUnlockAllowed"] is False
    assert rewards["requiresEconomyAdmiralReview"] is False

    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
    assert gates["requiresExactSpt413HandoverConditionProof"] is False
    assert gates["requiresExactItemTplSelection"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is False
    assert gates["requiresEconomyAdmiralReview"] is False
    assert gates["requiresFrozenCampaignOverlapReview"] is False
    assert data["frozenBoundary"] == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}
