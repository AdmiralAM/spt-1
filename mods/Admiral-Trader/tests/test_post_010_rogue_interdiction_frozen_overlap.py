import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_rogue_interdiction_has_no_frozen_rogue_semantic_duplicate():
    proof = load_json(ROOT / "manifests" / "post-010-rogue-interdiction-frozen-overlap-proof.json")
    spec = load_json(ROOT / "manifests" / "post-010-rogue-interdiction-operation.json")
    quests = [load_json(path) for path in sorted((ROOT / "db" / "quests").glob("*.json"))]

    assert len(quests) == proof["frozenRuntimeQuestCountReviewed"] == 31
    rogue_quests = []
    for quest in quests:
        text = json.dumps(quest, ensure_ascii=False)
        if '"exUsec"' in text:
            rogue_quests.append(quest)

    assert len(rogue_quests) == proof["expectedRogueSemanticQuestCount"] == 0
    assert proof["status"] == "resolved-no-frozen-rogue-semantic-overlap"
    assert proof["semanticDuplicateFound"] is False
    assert spec["schemaVersion"] == 3
    assert spec["frozenCampaignOverlapReview"]["runtimeQuestCountReviewed"] == 31
    assert spec["frozenCampaignOverlapReview"]["rogueSemanticQuestCount"] == 0
    assert spec["frozenCampaignOverlapReview"]["semanticDuplicateFound"] is False
    assert spec["gates"]["requiresFrozenCampaignOverlapReview"] is False
    assert spec["gates"]["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert spec["gates"]["implementationAllowed"] is False
    assert spec["gates"]["runtimeMaterialize"] is False
