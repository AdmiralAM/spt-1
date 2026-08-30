import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_remaining_combat_legacy_is_consumed_without_new_quest_chains():
    spec = load_json(ROOT / "manifests" / "post-010-remaining-combat-bundle-review.json")
    reviews = {entry["sourceBundle"]: entry for entry in spec["reviews"]}

    expected = {
        "Headless PMC": 70,
        "Headless Scav": 60,
        "PMC Hunt": 70,
        "Raider Hunt": 10,
        "Rogue Hunt": 10,
        "Deep Pockets Legend": 274,
        "Iron Head Legend": 229,
        "Juggernaut Legend": 200,
        "Ultrasound Legend": 72,
        "Weapon Mastery": 1702,
    }
    assert set(reviews) == set(expected)
    assert sum(expected.values()) == 2697
    assert spec["source"]["legacyQuestCount"] == 2697

    for name, count in expected.items():
        review = reviews[name]
        assert review["legacyQuestCount"] == count
        assert review["runtimeMaterialize"] is False
        assert review["decision"] in {
            "merge-into-existing-operation",
            "merge-into-existing-operations",
            "consume-as-constraint-source",
            "consume-as-coverage-source",
        }

    boundary = spec["globalBoundary"]
    assert boundary["separateLegacyQuestChainsAllowed"] is False
    assert boundary["countOnlyEscalationAllowed"] is False
    assert boundary["perItemOrPerWeaponQuestQuotaAllowed"] is False
    assert boundary["locationFreeSpecialistFarmingAllowed"] is False
    assert boundary["newAuthoredOperationsRequired"] == 0

    assert reviews["Weapon Mastery"]["targets"] == ["arsenal-capability-backbone"]
    assert reviews["Rogue Hunt"]["targets"] == ["rogue-interdiction"]
    assert spec["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }


def test_remaining_combat_review_preserves_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))
    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
