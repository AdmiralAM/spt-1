import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "manifests" / "post-010-arsenal-objective-redesign.json"


def load():
    return json.loads(MANIFEST.read_text(encoding="utf-8"))


def test_arsenal_redesign_preserves_frozen_runtime_and_seven_family_shape():
    plan = load()
    assert plan["implementationAllowed"] is False
    assert plan["runtimeMaterialize"] is False
    assert plan["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
        "runtimeQuestMutationInThisSlice": False,
    }
    expected = {
        "sidearms",
        "smg-pdw",
        "shotguns",
        "assault-rifles",
        "marksman-battle-rifles",
        "precision-rifles",
        "special-weapons",
    }
    assert set(plan["families"]) == expected


def test_fieldwork_is_family_specific_and_bounded_not_count_escalation():
    plan = load()
    rules = plan["globalRules"]
    assert rules["sameObjectiveWithOnlyHigherCountForbidden"] is True
    assert rules["genericKillLadderForbidden"] is True
    assert rules["firHandoverForbidden"] is True

    archetypes = []
    for family in plan["families"].values():
        fieldwork = family["fieldwork"]
        bounds = fieldwork["bounds"]
        archetypes.append(fieldwork["archetype"])
        assert bounds["successfulRaidRequired"] is True
        if "maximumTargets" in bounds:
            assert bounds["maximumTargets"] <= rules["maximumPrimaryCombatCount"]

    assert len(set(archetypes)) >= rules["minimumDistinctFieldworkArchetypesAcrossFamilies"]
    assert len(set(archetypes)) == 7


def test_munitions_preserves_six_finite_offers_and_special_weapons_sample_only():
    plan = load()
    families = plan["families"]
    permanent = [name for name, spec in families.items() if spec["munitions"]["permanentOffer"]]
    assert len(permanent) == 6
    special = families["special-weapons"]["munitions"]
    assert special["permanentOffer"] is False
    assert special["sampleOnly"] is True

    gate = plan["materializationGate"]
    assert gate["requiresExactSpt413ConditionProofPerFamily"] is True
    assert gate["requiresVanillaQuestOverlapReview"] is True
    assert gate["requiresEconomyAdmiralRewardReview"] is True
    assert gate["requiresWeaponPoolValidation"] is True
    assert gate["requiresBatchedPhysicalMilestone"] is True
