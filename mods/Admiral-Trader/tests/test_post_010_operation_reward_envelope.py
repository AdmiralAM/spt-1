import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def authored_operation_keys():
    keys = set()
    multi = [
        "post-010-authored-operations.json",
        "post-010-access-security-operations.json",
        "post-010-protective-acoustic-operations.json",
        "post-010-wave-completion-operations.json",
    ]
    for filename in multi:
        spec = load_json(ROOT / "manifests" / filename)
        keys.update(op["key"] for op in spec["operations"])

    single_nested = [
        "post-010-chemical-support-operation.json",
        "post-010-expedition-loadout-operation.json",
        "post-010-procurement-operation.json",
        "post-010-route-security-operation.json",
    ]
    for filename in single_nested:
        spec = load_json(ROOT / "manifests" / filename)
        keys.add(spec["operation"]["key"])

    precision = load_json(ROOT / "manifests" / "post-010-precision-operation.json")
    keys.add(precision["key"])
    return keys


def test_reward_envelope_covers_the_complete_authored_wave_once():
    spec = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    authored = authored_operation_keys()
    mapped = set(spec["operationBands"])

    assert len(authored) == 21
    assert mapped == authored
    assert spec["campaignCaps"]["operationCount"] == 21


def test_numeric_envelopes_are_bounded_and_do_not_create_a_standing_faucet():
    spec = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    bands = spec["bands"]

    for band in bands.values():
        assert 0 < band["xpMin"] <= band["xpMax"] <= 18000
        assert 0 < band["rubMin"] <= band["rubMax"] <= 120000
        assert 0 < band["standingMax"] <= 0.025

    total_standing_ceiling = sum(
        bands[band_name]["standingMax"]
        for band_name in spec["operationBands"].values()
    )
    assert total_standing_ceiling <= spec["campaignCaps"]["maximumAdditionalStandingIfEveryOperationPaysItsBandCeiling"]
    assert total_standing_ceiling <= 0.37
    assert spec["campaignCaps"]["salesVolumeGrindRequired"] is False
    assert spec["campaignCaps"]["rewardEscalationForRepeatedSameObjectiveAllowed"] is False


def test_item_rewards_fail_closed_around_faucets_and_incomplete_compound_items():
    spec = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    item = spec["itemRewardPolicy"]
    integrity = spec["compoundRewardIntegrity"]
    gate = spec["materializationGate"]

    assert item["defaultItemReward"] == "none"
    assert item["durableEquipmentByDefaultAllowed"] is False
    assert item["containerRewardAllowed"] is False
    assert item["permanentStorefrontUnlockByDefaultAllowed"] is False
    assert item["bossOrRaiderGearFaucetAllowed"] is False
    assert item["rareAmmoFaucetAllowed"] is False
    assert item["renewableStimFaucetAllowed"] is False
    assert item["keyStorefrontAllowed"] is False

    assert integrity["requiredBeforeAnyWeaponArmorHelmetRigReward"] is True
    assert integrity["requiredSlotsMustBeComplete"] is True
    assert integrity["presetOrDefaultInsertChildrenMustBeValidated"] is True
    assert integrity["source"]["commit"] == "58c3dd0487858c7ba8e8c053b873fbe76a222637"

    assert gate["implementationAllowed"] is False
    assert gate["runtimeMaterialize"] is False
    assert gate["requiresEconomyAdmiralReview"] is True
    assert gate["requiresCompoundRewardIntegrityProofWhenApplicable"] is True


def test_reward_plan_preserves_frozen_runtime_counts():
    spec = load_json(ROOT / "manifests" / "post-010-operation-reward-envelope.json")
    assort = load_json(ROOT / "db" / "assort.json")
    quests = list((ROOT / "db" / "quests").glob("*.json"))

    assert spec["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }
    assert len(quests) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
