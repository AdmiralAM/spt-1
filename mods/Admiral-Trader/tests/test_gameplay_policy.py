import json
import math
import unittest
from collections import defaultdict
from pathlib import Path


MODULE = Path(__file__).resolve().parents[1]
MANIFESTS = MODULE / "manifests"
DB = MODULE / "db"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class GameplayPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.policy = load(MANIFESTS / "gameplay-policy.json")
        cls.access = load(MANIFESTS / "keys-authored-spec.json")
        cls.arsenal = load(MANIFESTS / "weapon-ammo-runtime-plan.json")
        cls.arsenal_spec = load(MANIFESTS / "weapon-ammo-authored-spec.json")
        cls.ammo = load(MANIFESTS / "ammo-offer-policy.json")
        cls.baseline = load(MANIFESTS / "baseline-stock.json")
        cls.assort = load(DB / "assort.json")
        cls.questassort = load(DB / "questassort.json")
        cls.base = load(DB / "base.json")

    @classmethod
    def baseline_offer_ids(cls):
        return {row["offerId"] for row in cls.baseline["offers"]}

    @classmethod
    def root_items_by_id(cls):
        return {item["_id"]: item for item in cls.assort["items"] if item["parentId"] == "hideout"}

    @classmethod
    def milestone_offer_ids(cls):
        return set(cls.root_items_by_id()) - cls.baseline_offer_ids()

    def test_campaign_shape_matches_current_backbone_without_artificial_cap(self):
        campaign = self.policy["campaign"]
        current_count = len(self.access["quests"]) + len(self.arsenal["quests"])
        self.assertEqual(len(self.access["quests"]), campaign["accessQuestCount"])
        self.assertEqual(len(self.arsenal["quests"]), campaign["arsenalQuestCount"])
        self.assertGreaterEqual(current_count, campaign["minimumAuthoredQuestCount"])
        self.assertIsNone(campaign["questCountArtificialCap"])
        self.assertFalse(campaign["repeatableQuestsAllowed"])
        self.assertFalse(campaign["foundInRaidBusyworkAllowed"])
        self.assertFalse(campaign["rawTechnicalObjectiveTextAllowed"])

    def test_gameplay_alpha_requires_non_quest_gated_baseline_stock(self):
        stock = self.policy["traderStock"]
        baseline_ids = self.baseline_offer_ids()
        roots = self.root_items_by_id()
        self.assertTrue(stock["baselineStockRequired"])
        self.assertFalse(stock["baselineOffersMustBeQuestGated"])
        self.assertTrue(stock["baselineOffersMustBeFinite"])
        self.assertTrue(stock["relationshipStockAllowed"])
        self.assertTrue(stock["milestoneOffersMayBeQuestGated"])
        self.assertTrue(stock["milestoneOffersMustBeFinite"])
        self.assertFalse(stock["generalPurposeSupermarketAllowed"])
        self.assertEqual(set(stock["directOverlapAuditRequired"]), {"vanilla", "Scorpion", "Artem"})
        self.assertTrue(baseline_ids)
        self.assertTrue(baseline_ids.issubset(roots))
        self.assertTrue(baseline_ids.isdisjoint(self.questassort["success"]))
        for offer in self.baseline["offers"]:
            self.assertIsNone(offer["questGate"])
            item = roots[offer["offerId"]]
            self.assertFalse(item["upd"]["UnlimitedCount"])
            self.assertEqual(item["upd"]["StackObjectsCount"], offer["stockPerReset"])
            self.assertEqual(item["upd"]["BuyRestrictionMax"], offer["buyRestriction"])
            self.assertEqual(self.assort["loyal_level_items"][offer["offerId"]], offer["loyaltyLevel"])

    def test_access_protocol_avoids_collection_busywork(self):
        rules = self.access["designRules"]
        access_policy = self.policy["accessProtocol"]
        self.assertEqual(rules["consumePlayerKeys"], access_policy["consumePlayerKeys"])
        self.assertEqual(rules["foundInRaidRequired"], access_policy["foundInRaidRequired"])
        self.assertFalse(rules["moneyCollectionObjectives"])
        self.assertFalse(rules["allKeysCollectionObjectives"])

        clearance_id = access_policy["labsClearanceQuestId"]
        clearance = [q for q in self.access["quests"] if q["id"] == clearance_id]
        self.assertEqual(len(clearance), 1)
        for quest in self.access["quests"]:
            expected = access_policy["labsClearanceUnlockSlots"] if quest["id"] == clearance_id else access_policy["otherAccessUnlockSlots"]
            self.assertEqual(quest["rewardBudget"]["unlockSlots"], expected)

    def test_arsenal_families_are_independent_three_stage_tracks(self):
        campaign = self.policy["campaign"]
        arsenal_policy = self.policy["arsenalProtocol"]
        by_family = defaultdict(list)
        id_to_family = {}
        for quest in self.arsenal["quests"]:
            by_family[quest["family"]].append(quest)
            id_to_family[quest["id"]] = quest["family"]

        self.assertEqual(len(by_family), campaign["weaponFamilyCount"])
        self.assertFalse(arsenal_policy["crossFamilyPrerequisitesAllowed"])
        self.assertFalse(arsenal_policy["handoverAmmoObjectivesAllowed"])

        stage_order = arsenal_policy["stageOrder"]
        for family, quests in by_family.items():
            self.assertEqual(len(quests), campaign["stagesPerWeaponFamily"])
            stage_map = {q["stage"]: q for q in quests}
            self.assertEqual(set(stage_map), set(stage_order))
            self.assertEqual(stage_map[stage_order[0]]["prerequisites"], [])
            for previous, current in zip(stage_order, stage_order[1:]):
                self.assertEqual(stage_map[current]["prerequisites"], [stage_map[previous]["id"]], msg=f"{family} must advance only inside its own track")
            for quest in quests:
                for prerequisite in quest["prerequisites"]:
                    self.assertEqual(id_to_family[prerequisite], family)

    def test_permanent_ammo_unlocks_exist_only_for_munitions_stage(self):
        arsenal_policy = self.policy["arsenalProtocol"]
        munitions = {q["family"]: q["id"] for q in self.arsenal["quests"] if q["stage"] == arsenal_policy["permanentUnlockStage"]}
        offers = self.ammo["offers"]
        expected_families = set(arsenal_policy["familiesWithPermanentAmmoOffer"])
        self.assertEqual(set(offers), expected_families)
        for family, offer in offers.items():
            self.assertEqual(offer["questId"], munitions[family])

        for family in arsenal_policy["sampleOnlyFamilies"]:
            self.assertNotIn(family, offers)
        self.assertFalse(self.ammo["specialWeapons"]["permanentOffer"])
        self.assertTrue(self.ammo["specialWeapons"]["sampleOnly"])

    def test_current_milestone_logistics_are_finite_quest_gated_and_bounded(self):
        logistics = self.policy["logistics"]
        roots = self.root_items_by_id()
        milestone_ids = self.milestone_offer_ids()
        success = self.questassort["success"]
        self.assertEqual(set(self.questassort), {"started", "success", "fail"})
        self.assertEqual(len(milestone_ids), logistics["expectedMilestonePermanentOfferCount"])
        self.assertEqual(len(success), logistics["expectedMilestonePermanentOfferCount"])
        self.assertEqual(len(self.ammo["offers"]), logistics["expectedAmmoMilestoneOfferCount"])
        self.assertEqual(set(success), milestone_ids)

        for offer_id in milestone_ids:
            item = roots[offer_id]
            upd = item["upd"]
            self.assertFalse(upd["UnlimitedCount"])
            self.assertGreater(upd["StackObjectsCount"], 0)
            self.assertGreater(upd["BuyRestrictionMax"], 0)
            self.assertLessEqual(upd["StackObjectsCount"], logistics["maximumPermanentOfferStockPerReset"])
            self.assertLessEqual(upd["BuyRestrictionMax"], logistics["maximumPermanentOfferStockPerReset"])
            self.assertEqual(self.assort["loyal_level_items"][offer_id], logistics["questUnlockLoyaltyLevel"])

        ammo_units = sum(offer["stockPerReset"] for offer in self.ammo["offers"].values())
        full_reset_spend = sum(offer["stockPerReset"] * offer["priceRub"] for offer in self.ammo["offers"].values())
        self.assertLessEqual(ammo_units, logistics["maximumAmmoUnitsAcrossPermanentOffersPerReset"])
        self.assertLessEqual(full_reset_spend, logistics["maximumAmmoFullResetSpendRub"])

        pricing = self.ammo["pricing"]
        self.assertLessEqual(pricing["multiplier"], logistics["maximumReferencePriceMultiplier"])
        for family, offer in self.ammo["offers"].items():
            rounded_expected = math.ceil(offer["referenceRub"] * pricing["multiplier"] / pricing["roundUpToRub"]) * pricing["roundUpToRub"]
            self.assertEqual(offer["priceRub"], rounded_expected, family)

    def test_loyalty_is_relationship_status_not_capability_authority(self):
        loyalty = self.policy["loyalty"]
        logistics = self.policy["logistics"]
        tiers = self.base["loyaltyLevels"]

        self.assertEqual(loyalty["role"], "relationship-status")
        self.assertFalse(loyalty["capabilityAuthority"])
        self.assertFalse(loyalty["standingMayBypassQuestGates"])
        self.assertFalse(loyalty["salesSumMayGateProgression"])
        self.assertTrue(loyalty["relationshipStockMayUseLoyalty"])
        self.assertFalse(loyalty["servicesMayUnlockByLoyalty"])
        self.assertFalse(loyalty["priceAdvantagesMayUnlockByLoyalty"])
        self.assertEqual(len(tiers), loyalty["expectedTierCount"])
        self.assertEqual([row["minStanding"] for row in tiers], loyalty["expectedStandingThresholds"])
        self.assertTrue(all(row["minSalesSum"] == 0 for row in tiers))
        self.assertEqual(len({row["buy_price_coef"] for row in tiers}), 1)
        self.assertFalse(self.base["repair"]["availability"])
        self.assertFalse(self.base["insurance"]["availability"])

        baseline_ids = self.baseline_offer_ids()
        milestone_ids = self.milestone_offer_ids()
        self.assertTrue(baseline_ids.isdisjoint(self.questassort["success"]))
        self.assertEqual(set(self.questassort["success"]), milestone_ids)
        self.assertEqual({self.assort["loyal_level_items"][offer_id] for offer_id in milestone_ids}, {logistics["questUnlockLoyaltyLevel"]})

        access_standing = sum(q["rewardBudget"]["standing"] for q in self.access["quests"])
        arsenal_standing = sum(stage["standing"] for family in self.arsenal_spec["families"] for stage in family["stages"])
        total = round(access_standing + arsenal_standing, 8)
        self.assertEqual(total, loyalty["authoredCampaignStandingTotal"])
        self.assertGreaterEqual(total, max(loyalty["expectedStandingThresholds"]))

    def test_reward_communication_policy_rejects_hidden_payoff_design(self):
        reward = self.policy["rewardCommunication"]
        self.assertTrue(reward["importantUnlockMustBeExplainedInQuestText"])
        self.assertTrue(reward["standingMustBePlayerLegible"])
        self.assertFalse(reward["genericMoneyOnlyPreferred"])
        self.assertTrue(reward["distinctiveItemOrSamplePreferredForMilestones"])

    def test_ammo_offer_manifest_matches_packaged_assort(self):
        packaged_by_tpl = {item["_tpl"]: item for item in self.assort["items"]}
        for family, offer in self.ammo["offers"].items():
            item = packaged_by_tpl[offer["tpl"]]
            self.assertEqual(item["upd"]["StackObjectsCount"], offer["stockPerReset"], family)
            self.assertEqual(item["upd"]["BuyRestrictionMax"], offer["buyRestriction"], family)
            price = self.assort["barter_scheme"][item["_id"]][0][0]["count"]
            self.assertEqual(price, offer["priceRub"], family)
            self.assertEqual(self.questassort["success"][item["_id"]], offer["questId"], family)


if __name__ == "__main__":
    unittest.main()
