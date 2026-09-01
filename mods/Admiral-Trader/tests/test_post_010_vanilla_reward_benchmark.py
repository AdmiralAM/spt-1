import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(name: str):
    return json.loads((ROOT / "manifests" / name).read_text(encoding="utf-8"))


def test_post_010_reward_envelope_consumes_pinned_vanilla_benchmark():
    envelope = load_json("post-010-operation-reward-envelope.json")
    policy = load_json("reward-policy.json")
    authority = envelope["vanillaRewardBenchmarkAuthority"]
    benchmark = policy["benchmark"]
    observed = policy["observedReference"]["overall"]

    assert authority["manifest"] == "reward-policy.json"
    assert authority["sourceRepository"] == benchmark["sourceRepository"]
    assert authority["sourceCommit"] == benchmark["sourceCommit"]
    assert authority["sourcePath"] == benchmark["sourcePath"]
    assert authority["questCountObserved"] == benchmark["questCountObserved"] == 558
    assert authority["method"] == benchmark["method"]
    assert authority["benchmarkReviewComplete"] is True
    assert authority["nearestComparatorRequired"] is True
    assert authority["explicitLevelBucketAloneIsNotComparator"] is True
    assert envelope["materializationGate"]["requiresVanillaRewardBenchmark"] is False

    ref = authority["overallReference"]
    assert ref == {
        "xpMedian": observed["xp"]["median"],
        "xpP75": observed["xp"]["p75"],
        "xpP90": observed["xp"]["p90"],
        "rubMedian": observed["rub"]["median"],
        "rubP75": observed["rub"]["p75"],
        "rubP90": observed["rub"]["p90"],
        "standingMedian": observed["standing"]["median"],
        "standingP75": observed["standing"]["p75"],
        "standingP90": observed["standing"]["p90"],
    }


def test_post_010_reward_ceilings_remain_conservative_against_pinned_overall_distribution():
    envelope = load_json("post-010-operation-reward-envelope.json")
    ref = envelope["vanillaRewardBenchmarkAuthority"]["overallReference"]
    critical = envelope["bands"]["critical"]

    assert critical["xpMax"] < ref["xpP75"]
    assert critical["rubMax"] < ref["rubP90"]
    assert critical["standingMax"] < ref["standingP75"]

    assert envelope["materializationGate"]["implementationAllowed"] is False
    assert envelope["materializationGate"]["runtimeMaterialize"] is False
    assert envelope["materializationGate"]["requiresEconomyAdmiralReview"] is True
    assert envelope["frozenBoundary"] == {
        "questCount": 31,
        "rootOfferCount": 11,
        "relationshipRuntimeOffers": 0,
    }
