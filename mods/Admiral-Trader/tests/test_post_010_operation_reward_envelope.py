import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
def load_json(path: Path): return json.loads(path.read_text(encoding="utf-8"))

def authored_operation_keys():
    keys=set()
    for filename in ["post-010-authored-operations.json","post-010-access-security-operations.json","post-010-protective-acoustic-operations.json","post-010-wave-completion-operations.json"]:
        spec=load_json(ROOT/"manifests"/filename); keys.update(op["key"] for op in spec["operations"])
    for filename in ["post-010-chemical-support-operation.json","post-010-expedition-loadout-operation.json","post-010-procurement-operation.json","post-010-route-security-operation.json"]:
        spec=load_json(ROOT/"manifests"/filename); keys.add(spec["operation"]["key"])
    precision=load_json(ROOT/"manifests"/"post-010-precision-operation.json"); keys.add(precision["key"])
    return keys

def test_reward_envelope_covers_the_complete_authored_wave_once():
    spec=load_json(ROOT/"manifests"/"post-010-operation-reward-envelope.json"); authored=authored_operation_keys(); mapped=set(spec["operationBands"])
    assert len(authored)==19 and "expedition-discipline" not in authored and "field-medicine-under-pressure" not in authored and mapped==authored and spec["campaignCaps"]["operationCount"]==19
    assert spec["campaignPlacementAuthority"]["operationCount"]==19

def test_numeric_envelopes_are_bounded_and_do_not_create_a_standing_faucet():
    spec=load_json(ROOT/"manifests"/"post-010-operation-reward-envelope.json"); bands=spec["bands"]
    for band in bands.values():
        assert 0<band["xpMin"]<=band["xpMax"]<=18000; assert 0<band["rubMin"]<=band["rubMax"]<=120000; assert 0<band["standingMax"]<=0.025
    total=sum(bands[name]["standingMax"] for name in spec["operationBands"].values())
    assert round(total,6)==spec["campaignCaps"]["maximumAdditionalStandingIfEveryOperationPaysItsBandCeiling"] and total<=0.37
    assert spec["campaignCaps"]["salesVolumeGrindRequired"] is False and spec["campaignCaps"]["rewardEscalationForRepeatedSameObjectiveAllowed"] is False

def test_authored_reward_allocation_covers_every_operation_and_stays_inside_its_band():
    spec=load_json(ROOT/"manifests"/"post-010-operation-reward-envelope.json"); authored=authored_operation_keys(); allocations=spec["operationRewards"]
    assert set(allocations)==authored==set(spec["operationBands"])
    standing_total=0.0
    for key,reward in allocations.items():
        band=spec["bands"][spec["operationBands"][key]]
        assert band["xpMin"]<=reward["xp"]<=band["xpMax"] and band["rubMin"]<=reward["rub"]<=band["rubMax"] and 0<reward["standing"]<=band["standingMax"]
        assert reward["itemReward"] is None; standing_total+=reward["standing"]
    assert round(standing_total,6)==spec["campaignCaps"]["maximumAuthoredStandingAllocation"]
    assert standing_total<spec["campaignCaps"]["maximumAdditionalStandingIfEveryOperationPaysItsBandCeiling"]
    assert spec["allocationPolicy"]["runtimeMaterializationFromThisTableAllowed"] is False

def test_item_rewards_fail_closed_around_faucets_and_incomplete_compound_items():
    spec=load_json(ROOT/"manifests"/"post-010-operation-reward-envelope.json"); item=spec["itemRewardPolicy"]; integrity=spec["compoundRewardIntegrity"]; gate=spec["materializationGate"]
    assert item["defaultItemReward"]=="none" and item["durableEquipmentByDefaultAllowed"] is False and item["containerRewardAllowed"] is False
    assert item["permanentStorefrontUnlockByDefaultAllowed"] is False and item["bossOrRaiderGearFaucetAllowed"] is False and item["rareAmmoFaucetAllowed"] is False
    assert item["renewableStimFaucetAllowed"] is False and item["keyStorefrontAllowed"] is False
    assert integrity["requiredBeforeAnyWeaponArmorHelmetRigReward"] is True and integrity["requiredSlotsMustBeComplete"] is True and integrity["presetOrDefaultInsertChildrenMustBeValidated"] is True
    assert gate["implementationAllowed"] is False and gate["runtimeMaterialize"] is False and gate["requiresEconomyAdmiralReview"] is True

def test_reward_plan_preserves_frozen_runtime_counts():
    spec=load_json(ROOT/"manifests"/"post-010-operation-reward-envelope.json"); assort=load_json(ROOT/"db"/"assort.json"); quests=list((ROOT/"db"/"quests").glob("*.json"))
    assert spec["frozenBoundary"]=={"questCount":31,"rootOfferCount":11,"relationshipRuntimeOffers":0}; assert len(quests)==31; assert len(assort["loyal_level_items"])==11; assert len(assort["barter_scheme"])==11
