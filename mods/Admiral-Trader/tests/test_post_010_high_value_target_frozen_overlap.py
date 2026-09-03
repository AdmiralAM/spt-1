import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUESTS = ROOT / "db" / "quests"


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def walk_strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for child in value.values():
            yield from walk_strings(child)
    elif isinstance(value, list):
        for child in value:
            yield from walk_strings(child)


def test_command_window_has_no_frozen_glukhar_semantic_collision():
    proof = load_json(ROOT / "manifests" / "post-010-high-value-target-frozen-overlap-proof.json")
    operation = load_json(ROOT / "manifests" / "post-010-high-value-target-operation.json")
    quest_files = sorted(QUESTS.glob("*.json"))

    assert len(quest_files) == proof["frozenQuestCountReviewed"] == 31
    glukhar_hits = []
    for path in quest_files:
        strings = list(walk_strings(load_json(path)))
        if "bossGluhar" in strings:
            glukhar_hits.append(path.name)

    assert glukhar_hits == []
    assert proof["result"]["bossGluharReferences"] == 0
    assert proof["result"]["semanticDuplicateCount"] == 0
    assert proof["result"]["decision"] == "PASS"
    assert operation["selectedTarget"]["savageRole"] == proof["candidateShape"]["savageRole"] == ["bossGluhar"]
    assert operation["selectedTarget"]["locationTarget"] == proof["candidateShape"]["location"] == "RezervBase"
    assert operation["gates"]["requiresFrozenCampaignOverlapReview"] is False
    assert operation["gates"]["requiresVanillaScorpionArtemOverlapAudit"] is True
    assert operation["gates"]["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert operation["gates"]["runtimeMaterialize"] is False
