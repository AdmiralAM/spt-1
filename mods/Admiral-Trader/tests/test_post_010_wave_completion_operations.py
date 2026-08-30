import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_replacement_wave_completion_is_bounded_bilingual_and_non_materialized():
    spec = load_json(ROOT / "manifests" / "post-010-wave-completion-operations.json")
    ops = spec["operations"]

    assert spec["frozen010Base"] == "053a62ff5f1cb545f13bc89a96bba3acd319a823"
    assert spec["implementationAllowed"] is False
    assert len(ops) == 6
    assert {op["key"] for op in ops} == {
        "protection-calibration",
        "ballistic-head-test",
        "heavy-assault-loadout",
        "signal-discipline",
        "endurance-circuit",
        "precision-denial",
    }

    for op in ops:
        assert op["workingTitle"]["en"] and op["workingTitle"]["ru"]
        assert op["playerText"]["en"]["description"]
        assert op["playerText"]["en"]["started"]
        assert op["playerText"]["en"]["success"]
        assert op["playerText"]["ru"]["description"]
        assert op["playerText"]["ru"]["started"]
        assert op["playerText"]["ru"]["success"]
        assert op["objectiveIntent"]
        assert op["rewardDoctrine"]
        assert op["proofGates"]
        assert op["runtimeMaterialize"] is False
        assert op["antiGrind"]["repeatable"] is False

    assert spec["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }


def test_completion_wave_closes_previously_planned_21_contract_shape_without_runtime_growth():
    staged_files = [
        "post-010-authored-operations.json",
        "post-010-access-security-operations.json",
        "post-010-protective-acoustic-operations.json",
        "post-010-precision-operation.json",
        "post-010-procurement-operation.json",
        "post-010-route-security-operation.json",
        "post-010-chemical-support-operation.json",
        "post-010-expedition-loadout-operation.json",
        "post-010-wave-completion-operations.json",
    ]

    keys = set()
    count = 0
    for filename in staged_files:
        data = load_json(ROOT / "manifests" / filename)
        if "operations" in data:
            ops = data["operations"]
        elif "operation" in data:
            ops = [data["operation"]]
        else:
            ops = [data]
        for op in ops:
            key = op["key"]
            assert key not in keys
            keys.add(key)
            count += 1

    # The previously approved replacement wave targeted ~21 distinct Admiral
    # contracts instead of hundreds of generated legacy tasks.
    assert count == 21

    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))
    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
