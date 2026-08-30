import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_headless_specialist_bundles_merge_without_new_quest_chains():
    spec = load_json(ROOT / "manifests" / "post-010-headless-specialist-review.json")
    reviews = {entry["sourceBundle"]: entry for entry in spec["reviews"]}

    assert spec["source"]["legacyQuestCount"] == 20
    assert set(reviews) == {"Headless Raider", "Headless Rogue"}

    raider = reviews["Headless Raider"]
    rogue = reviews["Headless Rogue"]
    assert raider["legacyQuestCount"] == 10
    assert rogue["legacyQuestCount"] == 10
    assert raider["decision"] == "merge-into-existing-operation"
    assert rogue["decision"] == "merge-into-existing-operation"
    assert raider["targetOperation"] == "labs-security-disruption"
    assert rogue["targetOperation"] == "rogue-interdiction"

    for review in reviews.values():
        boundary = review["mergeBoundary"]
        assert boundary["separateQuestAllowed"] is False
        assert boundary["headshotCountLadderAllowed"] is False
        assert boundary["maximumOptionalPrecisionTargets"] <= 2
        assert review["runtimeMaterialize"] is False

    assert rogue["mergeBoundary"]["locationFreeFallbackAllowed"] is False
    assert spec["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }


def test_headless_specialist_review_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))
    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
