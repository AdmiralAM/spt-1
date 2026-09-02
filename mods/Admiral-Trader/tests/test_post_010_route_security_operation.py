import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def test_route_security_operation_is_bounded_and_non_materialized():
    spec = load_json(ROOT / "manifests" / "post-010-route-security-operation.json")
    op = spec["operation"]
    bounds = op["bounds"]
    gates = spec["gates"]
    authority = spec["proofAuthority"]
    selected = op["selectedLocation"]

    assert spec["schemaVersion"] == 6
    assert spec["source"]["bundle"] == "Scav Hunt"
    assert spec["source"]["legacyQuestCount"] == 60
    assert op["key"] == "route-security"
    assert op["title"]["en"] and op["title"]["ru"]
    assert op["description"]["en"] and op["description"]["ru"]
    assert op["started"]["en"] and op["started"]["ru"]
    assert op["success"]["en"] and op["success"]["ru"]

    assert authority["scavTarget"] == "post-010-role-alias-proof.json"
    assert authority["locationAndExtraction"] == "post-010-pmc-location-extraction-proof.json"
    assert authority["exactLocationEvidence"] == "post-010-visit-place-proxy-proof.json"
    assert authority["sameRaidCoupling"] == "post-010-same-raid-coupling-proof.json"
    assert authority["rewardEnvelope"] == "post-010-operation-reward-envelope.json"
    assert authority["campaignProgression"] == "post-010-campaign-progression.json"
    assert authority["vanillaRewardBenchmark"] == "reward-policy.json"
    assert authority["frozenCampaignSource"] == "db/quests/*.json"
    assert authority["scorpionArtemOverlap"] == "post-010-route-security-scorpion-artem-overlap-proof.json"
    assert authority["vanillaOverlap"] == "post-010-route-security-vanilla-overlap-proof.json"
    assert "sub-location/zone" in authority["admittedBoundary"]
    assert "same-raid" in authority["admittedBoundary"]

    assert selected["map"] == "Customs"
    assert selected["locationTarget"] == "bigmap"
    assert selected["subLocationRequired"] is False
    assert selected["namedExfilRequired"] is False
    assert "logistics" in selected["selectionReason"].lower()

    assert bounds["maximumScavTargets"] <= 6
    assert bounds["maximumSuccessfulRaids"] == 1
    assert bounds["locationBound"] is True
    assert bounds["subLocationOrZoneRequired"] is False
    assert bounds["surviveOrExtractRequired"] is True
    assert bounds["repeatable"] is False
    assert bounds["escalatingSequelsAllowed"] is False
    assert bounds["perMapCopiesAllowed"] is False
    assert bounds["fourDigitKillCountsAllowed"] is False

    assert op["rewardDoctrine"]["permanentScavGearSupplyAllowed"] is False
    assert op["rewardDoctrine"]["capabilityUnlockAllowed"] is False
    assert op["rewardDoctrine"]["itemReward"] is None
    assert op["rewardDoctrine"]["economyReviewStatus"] == "approved-static"
    assert gates["implementationAllowed"] is False
    assert gates["runtimeMaterialize"] is False
    assert gates["requiresExactSpt413ScavTargetConditionProof"] is False
    assert gates["requiresExactSpt413SurvivedExtractionProof"] is False
    assert gates["requiresSubLocationOrZoneProof"] is False
    assert gates["requiresSameRaidKillAndExtractionCouplingProof"] is True
    assert gates["requiresExactLocationSelection"] is False
    assert gates["requiresVanillaScorpionArtemOverlapAudit"] is False
    assert gates["requiresFrozenCampaignOverlapReview"] is False
    assert gates["requiresEconomyAdmiralReview"] is False

    frozen = spec["frozenBoundary"]
    assert frozen == {"questCount": 31, "rootOfferCount": 11, "relationshipRuntimeOffers": 0}


def test_route_security_proof_authorities_exist_and_temporal_coupling_stays_closed():
    spec = load_json(ROOT / "manifests" / "post-010-route-security-operation.json")
    authority = spec["proofAuthority"]

    role_proof = load_json(ROOT / "manifests" / authority["scavTarget"])
    location_proof = load_json(ROOT / "manifests" / authority["locationAndExtraction"])
    exact_location_proof = load_json(ROOT / "manifests" / authority["exactLocationEvidence"])
    same_raid_proof = load_json(ROOT / "manifests" / authority["sameRaidCoupling"])
    vanilla_overlap = load_json(ROOT / "manifests" / authority["vanillaOverlap"])
    external_overlap = load_json(ROOT / "manifests" / authority["scorpionArtemOverlap"])

    role_text = json.dumps(role_proof, ensure_ascii=False)
    location_text = json.dumps(location_proof, ensure_ascii=False)
    same_raid_text = json.dumps(same_raid_proof, ensure_ascii=False)

    assert "Savage" in role_text
    assert "Survived" in location_text
    assert exact_location_proof["selectedRoute"]["map"] == "Customs"
    assert exact_location_proof["selectedRoute"]["locationTarget"] == spec["operation"]["selectedLocation"]["locationTarget"]
    assert vanilla_overlap["decision"]["vanillaOverlapResolved"] is True
    assert external_overlap["decision"]["scorpionOverlapResolved"] is True
    assert external_overlap["decision"]["artemOverlapResolved"] is True
    assert "oneSessionOnly" in same_raid_text
    assert spec["gates"]["requiresExactLocationSelection"] is False
    assert spec["gates"]["requiresVanillaScorpionArtemOverlapAudit"] is False
    assert spec["gates"]["requiresSameRaidKillAndExtractionCouplingProof"] is True


def test_route_security_reward_is_exactly_bound_to_campaign_and_vanilla_benchmark():
    spec = load_json(ROOT / "manifests" / "post-010-route-security-operation.json")
    reward_envelope = load_json(ROOT / "manifests" / spec["proofAuthority"]["rewardEnvelope"])
    campaign = load_json(ROOT / "manifests" / spec["proofAuthority"]["campaignProgression"])
    reward_policy = load_json(ROOT / "manifests" / spec["proofAuthority"]["vanillaRewardBenchmark"])
    review = spec["operation"]["economyReview"]

    assert review["status"] == "approved-static"
    assert review["campaignPlayerLevel"] == campaign["operationLevelPlacement"]["route-security"] == 21
    assert review["riskBand"] == reward_envelope["operationBands"]["route-security"] == "standard"
    assert review["authoredReward"] == reward_envelope["operationRewards"]["route-security"]

    standard = reward_envelope["bands"]["standard"]
    reward = review["authoredReward"]
    assert standard["xpMin"] <= reward["xp"] <= standard["xpMax"]
    assert standard["rubMin"] <= reward["rub"] <= standard["rubMax"]
    assert reward["standing"] <= standard["standingMax"]
    assert reward["itemReward"] is None

    elimination = reward_policy["observedReference"]["questTypeMedians"]["Elimination"]
    assert review["vanillaComparatorMedian"] == {"xp": elimination["xp"], "rub": elimination["rub"]}
    assert reward["xp"] < elimination["xp"]
    assert reward["rub"] < elimination["rub"]
    assert review["overallVanillaStandingMedian"] == reward_policy["observedReference"]["overall"]["standing"]["median"]
    assert reward["standing"] < review["overallVanillaStandingMedian"]
    assert review["economyAdmiralMayReduceAtMaterialization"] is True
    assert review["increaseBeyondStandardBandRequiresTraderReview"] is True
    assert spec["gates"]["requiresEconomyAdmiralReview"] is False


def test_route_security_frozen_overlap_is_map_only_not_semantic_duplicate():
    spec = load_json(ROOT / "manifests" / "post-010-route-security-operation.json")
    review = spec["operation"]["frozenCampaignOverlapReview"]
    quest_dir = ROOT / "db" / "quests"
    quests = [load_json(path) for path in sorted(quest_dir.glob("*.json"))]

    assert len(quests) == review["runtimeQuestCountReviewed"] == 31
    customs = [quest for quest in quests if quest.get("location") == "bigmap"]
    assert len(customs) == review["customsBoundQuestCount"] == 1

    overlap = customs[0]
    finish_types = sorted({condition.get("conditionType") for condition in overlap["conditions"]["AvailableForFinish"]})
    assert overlap["_id"] == review["overlapQuest"]["id"] == "cba3374f11375c4e7eea9efe"
    assert overlap["QuestName"] == review["overlapQuest"]["name"] == "Access Protocol: Customs"
    assert finish_types == review["overlapQuest"]["finishConditionTypes"] == ["FindItem"]
    assert review["overlapQuest"]["classification"] == "map-only-overlap"

    overlap_text = json.dumps(overlap, ensure_ascii=False)
    assert '"conditionType": "CounterCreator"' not in overlap_text
    assert '"conditionType": "Survived"' not in overlap_text
    assert review["semanticDuplicateFound"] is False
    assert spec["gates"]["requiresFrozenCampaignOverlapReview"] is False


def test_route_security_does_not_change_frozen_runtime_counts():
    assort = load_json(ROOT / "db" / "assort.json")
    quest_dir = ROOT / "db" / "quests"
    quest_files = list(quest_dir.glob("*.json"))

    assert len(quest_files) == 31
    assert len(assort["loyal_level_items"]) == 11
    assert len(assort["barter_scheme"]) == 11
