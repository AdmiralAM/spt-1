import json
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
        cls.ammo = load(MANIFESTS / "ammo-offer-policy.json")
        cls.assort = load(DB / "assort.json")
        cls.questassort = load(DB / "questassort.json")

    def test_campaign_shape_matches_doctrine(self):
        campaign = self.policy["campaign"]
        self.assertEqual(len(self.access["quests"]), campaign["accessQuestCount"])
        self.assertEqual(len(self.arsenal["quests"]), campaign["arsenalQuestCount"])
        self.assertEqual(
            len(self.access["quests"]) + len(self.arsenal["quests"]),
            campaign["expectedQuestCount"],
        )
        self.assertFalse(campaign["repeatableQuestsAllowed"])
        self.assertFalse(campaign["foundInRaidBusyworkAllowed"])

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
            expected = (
                access_policy["labsClearanceUnlockSlots"]
                if quest["id"] == clearance_id
                else access_policy["otherAccessUnlockSlots"]
            )
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
                self.assertEqual(
                    stage_map[current]["prerequisites"],
                    [stage_map[previous]["id"]],
                    msg=f"{family} must advance only inside its own track",
                )
            for quest in quests:
                for prerequisite in quest["prerequisites"]:
                    self.assertEqual(id_to_family[prerequisite], family)

    def test_permanent_ammo_unlocks_exist_only_for_munitions_stage(self):
        arsenal_policy = self.policy["arsenalProtocol"]
        munitions = {
            q["family"]: q["id"]
            for q in self.arsenal["quests"]
            if q["stage"] == arsenal_policy["permanentUnlockStage"]
        }
        offers = self.ammo["offers"]
        expected_families = set(arsenal_policy["familiesWithPermanentAmmoOffer"])
        self.assertEqual(set(offers), expected_families)
        for family, offer in offers.items():
            self.assertEqual(offer["questId"], munitions[family])

        for family in arsenal_policy["sampleOnlyFamilies"]:
            self.assertNotIn(family, offers)
        self.assertFalse(self.ammo["specialWeapons"]["permanentOffer"])
        self.assertTrue(self.ammo["specialWeapons"]["sampleOnly"])

    def test_trader_logistics_are_finite_quest_gated_and_bounded(self):
        logistics = self.policy["logistics"]
        items = self.assort["items"]
        success = self.questassort["Success"]
        self.assertEqual(len(items), logistics["expectedPermanentOfferCount"])
        self.assertEqual(len(success), logistics["expectedPermanentOfferCount"])
        self.assertEqual(len(self.ammo["offers"]), logistics["expectedAmmoPermanentOfferCount"])

        item_ids = {item["_id"] for item in items}
        self.assertEqual(set(success), item_ids)
        for item in items:
            upd = item["upd"]
            self.assertFalse(upd["UnlimitedCount"])
            self.assertGreater(upd["StackObjectsCount"], 0)
            self.assertGreater(upd["BuyRestrictionMax"], 0)
            self.assertLessEqual(
                upd["StackObjectsCount"], logistics["maximumPermanentOfferStockPerReset"]
            )
            self.assertLessEqual(
                upd["BuyRestrictionMax"], logistics["maximumPermanentOfferStockPerReset"]
            )
            self.assertEqual(
                self.assort["loyal_level_items"][item["_id"]],
                logistics["questUnlockLoyaltyLevel"],
            )

    def test_ammo_offer_manifest_matches_packaged_assort(self):
        packaged_by_tpl = {item["_tpl"]: item for item in self.assort["items"]}
        for family, offer in self.ammo["offers"].items():
            item = packaged_by_tpl[offer["tpl"]]
            self.assertEqual(item["upd"]["StackObjectsCount"], offer["stockPerReset"], family)
            self.assertEqual(item["upd"]["BuyRestrictionMax"], offer["buyRestriction"], family)
            price = self.assort["barter_scheme"][item["_id"]][0][0]["count"]
            self.assertEqual(price, offer["priceRub"], family)
            self.assertEqual(self.questassort["Success"][item["_id"]], offer["questId"], family)


if __name__ == "__main__":
    unittest.main()
