import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_player_equipment_shape_is_exact_and_fail_closed():
    proof = load_json(ROOT / "manifests" / "post-010-player-equipment-proof.json")
    assert proof["runtimeMaterialize"] is False
    assert proof["frozen010Base"] == "053a62ff5f1cb545f13bc89a96bba3acd319a823"
    source_shape = proof["source"]["observedShape"]
    assert source_shape["conditionType"] == "Equipment"
    assert source_shape["IncludeNotEquippedItems"] is False
    assert source_shape["equipmentInclusive"] == "array-of-arrays of item TPLs"
    unproven = proof["explicitlyNotProven"]
    for key in ("categoryOrParentIdSelector", "armorClassPredicate", "multiGroupBooleanSemantics", "includeNotEquippedItemsTrueSemantics", "sameRaidEquipmentThenExtractCoupling"):
        assert unproven[key]["supported"] is False
    rules = proof["materializationRules"]
    assert rules["explicitValidatedTplsRequired"] is True
    assert rules["categoryOrArmorClassInferenceForbidden"] is True
    assert rules["multiGroupSemanticsMustRemainFailClosed"] is True
    assert rules["includeNotEquippedItemsMustRemainFalse"] is True
    assert rules["sameRaidEquipmentThenExtractMustRemainFailClosed"] is True
    assert rules["noNewFrozen010QuestJson"] is True


def test_player_equipment_proof_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))
    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
