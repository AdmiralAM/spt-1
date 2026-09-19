import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = json.loads(
    (ROOT / "manifests" / "external-trader-consolidation.json").read_text(encoding="utf-8")
)
CONSOLIDATION = (ROOT / "server" / "LegacyTraderConsolidation.cs").read_text(encoding="utf-8")
REGISTRATION = (ROOT / "server" / "TraderRegistration.cs").read_text(encoding="utf-8")
AUDIT = (ROOT / "docs" / "painter-artem-consolidation-audit.md").read_text(encoding="utf-8")


def test_persistent_ownership_and_measured_external_scope() -> None:
    assert MANIFEST["ownerTraderId"] == "d5c27bb3169f8dfbc13f6b69"
    assert MANIFEST["legacyTraderIds"] == {
        "painter": "668aaff35fd574b6dcc4a686",
        "artem": "66bf757f27d0b097db0acea5",
    }
    assert len({MANIFEST["ownerTraderId"], *MANIFEST["legacyTraderIds"].values()}) == 3
    assert MANIFEST["preserveAdmiralContent"] is True
    assert MANIFEST["combinedExternalRuntime"] == {
        "quests": 35,
        "rootOffers": 402,
        "assortItemRows": 946,
        "questUnlocks": 41,
        "suits": 68,
    }


def test_profile_migration_is_explicit_idempotent_and_save_first() -> None:
    assert "admiral-trader-legacy-consolidation-v1" in CONSOLIDATION
    assert "Math.Max(admiral.Standing" in CONSOLIDATION
    assert "Math.Max(admiral.SalesSum" in CONSOLIDATION
    assert "Math.Max(admiral.LoyaltyLevel" in CONSOLIDATION
    assert "MergePurchases(profile, legacyId)" in CONSOLIDATION
    assert "MergeDialogue(profile, legacyId)" in CONSOLIDATION
    assert CONSOLIDATION.index("await saveServer.SaveProfileAsync") < CONSOLIDATION.index(
        "tradersTable.Remove(legacyId)"
    )
    assert 'RegisterLegacyCompatibilityShells(traderBase)' in REGISTRATION


def test_scope_excludes_belt_container_compatibility_and_records_real_campaign_size() -> None:
    lowered = CONSOLIDATION.lower()
    for forbidden in ("belt", "armband", "securecontainer", "secure container"):
        assert forbidden not in lowered
    assert "207 quests without Icebreaker" in AUDIT
    assert "217 with the installed ten-quest Icebreaker chain" in AUDIT
