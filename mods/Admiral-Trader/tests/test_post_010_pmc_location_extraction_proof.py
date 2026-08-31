import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_pmc_location_extraction_shapes_are_bounded_and_fail_closed():
    proof = load_json(ROOT / "manifests" / "post-010-pmc-location-extraction-proof.json")

    assert proof["runtimeMaterialize"] is False
    assert proof["frozen010Base"] == "053a62ff5f1cb545f13bc89a96bba3acd319a823"

    proven = proof["proven"]
    assert proven["anyPmcTarget"]["conditionType"] == "Kills"
    assert proven["anyPmcTarget"]["target"] == "AnyPmc"
    assert proven["anyPmcTarget"]["savageRole"] == []

    location = proven["locationBoundCounter"]
    assert location["conditionType"] == "Location"
    assert location["shape"] == "CounterCreator.counter.conditions[]"
    assert location["targetType"] == "string[]"

    survived = proven["survivedExtraction"]
    assert survived["conditionTypes"] == ["Location", "ExitStatus"]
    assert survived["status"] == ["Survived"]

    exfil = proven["specificExfil"]
    assert exfil["conditionTypes"] == ["Location", "ExitName", "ExitStatus"]

    unproven = proof["notProvenByThisSlice"]
    assert unproven["usecVsBearFactionSplit"] is True
    assert unproven["followerOrGuardAliases"] is True
    assert unproven["zoneIdOrSubLocationSemantics"] is True
    assert unproven["killThenExtractSameRaidCoupling"] is True
    assert unproven["oneSessionOnlySemantics"] is True
    assert unproven["selectedOperationMapTargets"] is True
    assert unproven["selectedOperationExfilNames"] is True

    rules = proof["materializationRules"]
    assert rules["anyPmcMayBeUsedWithoutFactionSplit"] is True
    assert rules["mapTargetMustBeExplicitlySelected"] is True
    assert rules["namedExfilMustBeValidatedAgainstTargetLocation"] is True
    assert rules["sameRaidKillAndExtractMustRemainFailClosed"] is True
    assert rules["noNewFrozen010QuestJson"] is True


def test_condition_proof_matrix_tracks_the_exact_proof_boundary():
    matrix = load_json(ROOT / "manifests" / "post-010-condition-proof-matrix.json")
    proof = load_json(ROOT / "manifests" / "post-010-pmc-location-extraction-proof.json")

    assert matrix["schemaVersion"] >= 3
    assert matrix["runtimeMaterialize"] is False
    assert matrix["evidence"]["pmcLocationExtractionProof"] == "post-010-pmc-location-extraction-proof.json"

    proven = {entry["capability"]: entry for entry in matrix["staticallyProvenShapes"]}
    assert proven["any-PMC elimination"]["shape"] == {
        "target": proof["proven"]["anyPmcTarget"]["target"],
        "savageRole": proof["proven"]["anyPmcTarget"]["savageRole"],
    }
    assert proven["map-bound counter"]["conditionType"] == proof["proven"]["locationBoundCounter"]["conditionType"]
    assert proven["successful extraction"]["status"] == proof["proven"]["survivedExtraction"]["status"]
    assert proven["specific named extraction"]["conditionTypes"] == proof["proven"]["specificExfil"]["conditionTypes"]

    unproven = {entry["capability"] for entry in matrix["stillUnprovenForPost010"]}
    assert "PMC faction/any-PMC and follower/guard discrimination" not in unproven
    assert "map or sub-location restriction" not in unproven
    assert "survive/extract and same-raid coupling" not in unproven
    assert "PMC faction split and follower/guard discrimination" in unproven
    assert "sub-location or zone restriction" in unproven
    assert "kill/action then extract in the same raid" in unproven

    rules = matrix["materializationRule"]
    assert rules["mapAndExfilTargetsMustBeExplicitlySelected"] is True
    assert rules["sameRaidCouplingMustRemainFailClosed"] is True
    assert rules["noNewFrozen010QuestJson"] is True


def test_condition_proof_slice_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))

    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
