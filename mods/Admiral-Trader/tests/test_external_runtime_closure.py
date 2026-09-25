import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
ADMIRAL = "d5c27bb3169f8dfbc13f6b69"
PAINTER = "668aaff35fd574b6dcc4a686"
ARTEM = "66bf757f27d0b097db0acea5"
LEGACY = {PAINTER, ARTEM}


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def iter_nodes(value, path=()):
    if isinstance(value, dict):
        yield path, value
        for key, child in value.items():
            yield from iter_nodes(child, path + (str(key),))
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from iter_nodes(child, path + (str(index),))


def external_quests():
    return {
        PAINTER: load(next((ROOT / f"external/painter/db/CustomQuests/{PAINTER}/Quests").glob("*.json"))),
        ARTEM: load(next((ROOT / f"db/CustomQuests/{ARTEM}/Quests").glob("*.json"))),
    }


def test_every_legacy_quest_reference_is_a_declared_runtime_remap_input() -> None:
    counts = {PAINTER: 0, ARTEM: 0}
    allowed_keys = {"traderId", "target"}
    for owner, quests in external_quests().items():
        for quest_id, quest in quests.items():
            assert quest["traderId"] == owner, quest_id
            for path, node in iter_nodes(quest):
                for key, value in node.items():
                    if isinstance(value, str) and value in LEGACY:
                        counts[value] += 1
                        assert value == owner, (quest_id, path, key, value)
                        assert key in allowed_keys, (quest_id, path, key, value)
                        if key == "target":
                            assert node.get("type") == "TraderStanding", (quest_id, path, node)
                        elif path and path[-1] != quest_id and "type" in node:
                            assert node["type"] in {
                                "AssortmentUnlock",
                                "Completion",
                                "Discover",
                                "Elimination",
                                "Exploration",
                                "PickUp",
                                "WeaponAssembly",
                            }, (quest_id, path, node)
    assert counts[PAINTER] > 20
    assert counts[ARTEM] > 60


def test_assort_unlock_and_dialogue_payloads_have_no_legacy_trader_owner() -> None:
    paths = [
        ROOT / "db/assort.json",
        ROOT / "db/natalya-signature-assort.json",
        ROOT / "db/questassort.json",
        ROOT / "db/dialogue.json",
        ROOT / "external/artem/db/assort.json",
        ROOT / "external/painter/db/assort.json",
        next((ROOT / f"db/CustomQuests/{ARTEM}/QuestAssort").glob("*.json")),
    ]
    for path in paths:
        text = path.read_text(encoding="utf-8-sig")
        assert PAINTER not in text, path
        assert ARTEM not in text, path


def test_clothing_legacy_owner_is_bounded_input_and_runtime_suits_are_reowned() -> None:
    clothing = load(ROOT / "db/CustomClothing/Artem Clothes.json")
    assert len(clothing) == 64
    assert {row["traderId"] for row in clothing} == {ARTEM}

    consolidation = (ROOT / "server/LegacyTraderConsolidation.cs").read_text(encoding="utf-8")
    assert "LegacyTraderIds.Contains(suit.Tid.ToString(), StringComparer.Ordinal)" in consolidation
    assert "suit.Tid = AdmiralId" in consolidation
    assert "reward.Target = RuntimeIdentity.TraderId" in consolidation
    assert "condition.TraderId = RuntimeIdentity.TraderId" in consolidation
    assert "quest.TraderId = AdmiralId" in consolidation


def test_legacy_traders_are_only_temporary_migration_shells() -> None:
    registration = (ROOT / "server/TraderRegistration.cs").read_text(encoding="utf-8")
    consolidation = (ROOT / "server/LegacyTraderConsolidation.cs").read_text(encoding="utf-8")
    assert "RegisterLegacyCompatibilityShells" in registration
    assert consolidation.index("await saveServer.SaveProfileAsync") < consolidation.index("tradersTable.Remove(legacyId)")
    assert "RemoveEmptyCompatibilityShells" in consolidation
    assert "traderConfig.UpdateTime.RemoveAll" in consolidation
    assert "ragfairConfig.Traders.Remove" in consolidation


def test_persistent_identity_sets_remain_unique_and_admiral_owned() -> None:
    identities = load(ROOT / "manifests/external-content-identities.json")
    all_quest_ids = identities["painter"]["questIds"] + identities["artem"]["questIds"]
    all_offer_ids = (
        identities["painter"]["offerIds"]
        + identities["tgc"]["offerIds"]
        + identities["artem"]["offerIds"]
    )
    assert len(all_quest_ids) == len(set(all_quest_ids)) == 35
    assert len(all_offer_ids) == len(set(all_offer_ids)) == 402
    assert ADMIRAL not in set(all_quest_ids) | set(all_offer_ids) | LEGACY
