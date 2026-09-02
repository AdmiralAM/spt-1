import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def walk(value):
    if isinstance(value, dict):
        yield value
        for child in value.values():
            yield from walk(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk(child)


def test_expedition_loadout_has_no_frozen_campaign_semantic_duplicate():
    proof = load_json(ROOT / "manifests" / "post-010-expedition-loadout-frozen-overlap-proof.json")
    quest_paths = sorted((ROOT / "db" / "quests").glob("*.json"))

    assert len(quest_paths) == proof["frozenAuthority"]["expectedQuestCount"] == 31

    selected_tpls = set(proof["frozenAuthority"]["selectedEquipmentTpls"])
    equipment_conditions = []
    selected_tpl_hits = []

    for path in quest_paths:
        quest = load_json(path)
        serialized = json.dumps(quest, sort_keys=True)
        for tpl in selected_tpls:
            if tpl in serialized:
                selected_tpl_hits.append((path.name, tpl))
        for node in walk(quest):
            condition_type = node.get("conditionType") or node.get("_parent") or node.get("type")
            if condition_type == "Equipment":
                equipment_conditions.append(path.name)

    assert selected_tpl_hits == []
    assert equipment_conditions == []
    assert proof["review"]["result"] == "no-semantic-duplicate"
    assert proof["review"]["requiresReReviewOnFrozenEquipmentObjective"] is True
    assert proof["review"]["requiresReReviewOnSelectedTplReference"] is True
    assert proof["boundary"]["multiGroupEquipmentBooleanStillUnproven"] is True
    assert proof["boundary"]["sameRaidEquipmentAndExtractionCouplingStillUnproven"] is True
    assert proof["boundary"]["externalOverlapStillUnreviewed"] is True
    assert proof["boundary"]["runtimeMaterialize"] is False


def test_expedition_loadout_operation_consumes_frozen_overlap_proof_without_opening_runtime():
    spec = load_json(ROOT / "manifests" / "post-010-expedition-loadout-operation.json")
    proof = load_json(ROOT / "manifests" / spec["proofAuthority"]["frozenOverlap"])

    assert proof["operationKey"] == spec["operation"]["key"]
    assert spec["gates"]["requiresFrozenCampaignOverlapReview"] is False
    assert spec["gates"]["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert spec["gates"]["requiresMultiGroupEquipmentBooleanProof"] is True
    assert spec["gates"]["requiresSameRaidEquipmentAndExtractionCouplingProof"] is True
    assert spec["gates"]["implementationAllowed"] is False
    assert spec["gates"]["runtimeMaterialize"] is False
