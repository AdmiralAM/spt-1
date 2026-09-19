import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = json.loads(
    (ROOT / "manifests" / "external-trader-consolidation.json").read_text(encoding="utf-8")
)
CONSOLIDATION = (ROOT / "server" / "LegacyTraderConsolidation.cs").read_text(encoding="utf-8")
REGISTRATION = (ROOT / "server" / "TraderRegistration.cs").read_text(encoding="utf-8")
AUDIT = (ROOT / "docs" / "painter-artem-consolidation-audit.md").read_text(encoding="utf-8")
IDENTITIES = json.loads((ROOT / "manifests" / "external-content-identities.json").read_text(encoding="utf-8"))
PAINTER_POOL = json.loads((ROOT / "manifests" / "painter-special-delivery-pool.json").read_text(encoding="utf-8"))
PAINTER_REGISTRATION = (ROOT / "server" / "PainterContentRegistration.cs").read_text(encoding="utf-8")
IMPORT_TOOL = (ROOT / "tools" / "Import-PainterContent.ps1").read_text(encoding="utf-8")
HYDRATE_TOOL = (ROOT / "tools" / "Hydrate-PainterBundles.ps1").read_text(encoding="utf-8")
POST_CONSOLIDATION_AUDIT = (ROOT / "docs" / "post-consolidation-content-audit.md").read_text(encoding="utf-8")


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
        "questUnlocks": 44,
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


def test_every_external_identity_is_inventoried_without_collisions() -> None:
    expected = {
        "painter": {"questIds": 12, "offerIds": 7, "customItemIds": 5},
        "tgc": {"itemTemplateIds": 117, "offerIds": 114, "suitIds": 4},
        "artem": {"questIds": 23, "offerIds": 281, "questUnlockOfferIds": 41, "suitIds": 64},
    }
    for provider, fields in expected.items():
        for field, count in fields.items():
            values = IDENTITIES[provider][field]
            assert len(values) == count
            assert len(values) == len(set(values))
            assert all(len(value) == 24 and set(value) <= set("0123456789abcdef") for value in values)
    offer_sets = [set(IDENTITIES[name]["offerIds"]) for name in ("painter", "tgc", "artem")]
    assert not (offer_sets[0] & offer_sets[1] or offer_sets[0] & offer_sets[2] or offer_sets[1] & offer_sets[2])
    assert set(IDENTITIES["artem"]["questUnlockOfferIds"]) <= offer_sets[2]


def test_exact_cross_pr_authority_and_content_only_painter_contract() -> None:
    assert IDENTITIES["authority"]["tgcIntegrationHead"] == "bd1500b86c356f5e97fade75cf0c1df974ae9621"
    assert IDENTITIES["authority"]["beltHead"] == "c48238023e0d1d0dc8fabcabafb79db66154b90b"
    assert "Painter-4.0.dll must not be present" in IMPORT_TOOL
    assert "PainterContentRegistration" in PAINTER_REGISTRATION
    assert "Painter_3.0.0.7z" in HYDRATE_TOOL
    assert "CreateItemFromClone" in PAINTER_REGISTRATION
    assert '["ru"] = PainterRussianLocales[item.Id]' in PAINTER_REGISTRATION
    assert PAINTER_REGISTRATION.count('new() { Name = "') >= 5
    assert "ValidateExternalAssort" in CONSOLIDATION
    assert "ValidateExternalQuestGraph" in CONSOLIDATION


def test_post_consolidation_audit_names_bounded_candidates_before_removal() -> None:
    assert "no offers or quests removed by this audit" in POST_CONSOLIDATION_AUDIT
    assert "13 exact product-tree overlaps" in POST_CONSOLIDATION_AUDIT
    for offer_id in (
        "66bf757f27d0b097db0acf0c",
        "66bf757f27d0b097db0acf10",
        "66bf757f27d0b097db0acf0a",
    ):
        assert offer_id in POST_CONSOLIDATION_AUDIT
    for quest_id in (
        "208db81b5ce195bf0c176852",
        "8dad0d354ac000b7bbf05b9a",
        "3c6e085fc02f0597efdb5d5a",
        "31ab6a69a8436df6b3834b0a",
        "e520cec55b83621928e9e4ec",
    ):
        assert quest_id in POST_CONSOLIDATION_AUDIT
    assert "PruneUnavailableOfferTrees" in CONSOLIDATION


def test_painter_special_delivery_retains_its_authored_loot_contract() -> None:
    assert PAINTER_POOL["containerId"] == "668ff5bde41a0cce3b142464"
    assert PAINTER_POOL["rewardCount"] == 20
    assert PAINTER_POOL["foundInRaid"] is True
    assert len(PAINTER_POOL["rewardTplPool"]) == 175
    assert all(len(item_id) == 24 and set(item_id) <= set("0123456789abcdef") for item_id in PAINTER_POOL["rewardTplPool"])
    assert all(weight in {1, 5, 10} for weight in PAINTER_POOL["rewardTplPool"].values())
