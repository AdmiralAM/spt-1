import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_reserve_bundles_have_explicit_non_parallel_campaign_disposition():
    spec = load_json(ROOT / "manifests" / "post-010-reserve-bundle-review.json")
    src = spec["supplementalSource"]

    assert src["repository"] == "laurentmekka/AndrudisQuestManiac"
    assert src["commit"] == "58c3dd0487858c7ba8e8c053b873fbe76a222637"
    assert src["activeQuestCount"] == 4641
    assert src["activeBundleCount"] == 30
    assert src["reserveQuestCount"] == 1734
    assert src["legacyReserveQuestCount"] == 803
    assert src["alternativeReserveQuestCount"] == 931

    legacy = {entry["sourceBundle"]: entry for entry in spec["legacyReserve"]}
    assert set(legacy) == {"Gear Mastery", "Honor Skills", "Skills Guru", "Weapon Expert"}
    assert legacy["Gear Mastery"]["decision"] == "consume-as-constraint-source"
    assert legacy["Weapon Expert"]["decision"] == "consume-as-coverage-source"
    assert legacy["Honor Skills"]["decision"] == "reject-direct-port"
    assert legacy["Skills Guru"]["decision"] == "reject-direct-port"
    assert all(entry["newAuthoredOperationRequired"] is False for entry in legacy.values())

    alt = spec["alternativeReserve"]
    assert alt["documentedVariantCount"] == 28
    assert alt["documentedQuestCount"] == 931
    assert alt["decision"] == "no-separate-campaign-content"
    alt_classes = {entry["kind"]: entry["decision"] for entry in alt["classes"]}
    assert alt_classes["Gunsmith Assistant variants"] == "drop"
    assert alt_classes["Hideout Assistant variants"] == "drop"
    assert alt_classes["Killboard / Killboard By Weapons"] == "reject-as-quest-content"

    boundary = spec["globalBoundary"]
    assert boundary["reserveBundlesMayCreateParallelQuestChains"] is False
    assert boundary["variantSwappingSupported"] is False
    assert boundary["skillThresholdRewardLaddersAllowed"] is False
    assert boundary["neverCompletingTrackerQuestsAllowed"] is False
    assert boundary["newAuthoredOperationsRequired"] == 0


def test_reserve_bundle_review_preserves_frozen_runtime_counts():
    spec = load_json(ROOT / "manifests" / "post-010-reserve-bundle-review.json")
    assort = load_json(ROOT / "db" / "assort.json")
    quest_files = list((ROOT / "db" / "quests").glob("*.json"))

    assert spec["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }
    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
