import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"
DB = ROOT / "db"
RUB_TPL = "5449016a4bdc2d6f028b456f"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def root_items(assort):
    return {row["_id"]: row for row in assort["items"] if row.get("parentId") == "hideout"}


def rub_price(assort, offer_id):
    scheme = assort["barter_scheme"][offer_id]
    if len(scheme) != 1 or len(scheme[0]) != 1 or scheme[0][0].get("_tpl") != RUB_TPL:
        raise AssertionError(f"{offer_id} is not a single-RUB offer")
    return int(scheme[0][0]["count"])


class GameplayAlphaStockAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.audit = load(MANIFESTS / "gameplay-alpha-stock-audit.json")
        cls.policy = load(MANIFESTS / "gameplay-policy.json")
        cls.baseline = load(MANIFESTS / "baseline-stock.json")
        cls.ammo = load(MANIFESTS / "ammo-offer-policy.json")
        cls.assort = load(DB / "assort.json")
        cls.questassort = load(DB / "questassort.json")
        cls.roots = root_items(cls.assort)
        cls.baseline_ids = {row["offerId"] for row in cls.baseline["offers"]}
        cls.relationship_ids = {row["offerId"] for row in cls.audit["relationshipOffers"]}
        cls.milestone_ids = set(cls.roots) - cls.baseline_ids - cls.relationship_ids

    def test_offer_classification_is_exact_and_non_overlapping(self):
        expected = self.audit["expected"]
        self.assertEqual(len(self.roots), expected["totalRootOfferCount"])
        self.assertEqual(len(self.baseline_ids), expected["baselineOfferCount"])
        self.assertEqual(len(self.relationship_ids), expected["relationshipOfferCount"])
        self.assertEqual(len(self.milestone_ids), expected["milestoneOfferCount"])
        self.assertTrue(self.baseline_ids.isdisjoint(self.relationship_ids))
        self.assertTrue(self.baseline_ids.isdisjoint(self.milestone_ids))
        self.assertTrue(self.relationship_ids.isdisjoint(self.milestone_ids))
        self.assertEqual(self.baseline_ids | self.relationship_ids | self.milestone_ids, set(self.roots))
        self.assertTrue((self.baseline_ids | self.relationship_ids).isdisjoint(self.questassort["success"]))
        self.assertEqual(self.milestone_ids, set(self.questassort["success"]))

    def test_baseline_personal_limits_are_stricter_than_global_stock(self):
        global_units = player_units = global_value = player_value = 0
        for row in self.baseline["offers"]:
            offer_id = row["offerId"]
            item = self.roots[offer_id]
            upd = item["upd"]
            price = rub_price(self.assort, offer_id)
            self.assertEqual(price, row["priceRub"])
            self.assertFalse(upd["UnlimitedCount"])
            self.assertEqual(upd["StackObjectsCount"], row["stockPerReset"])
            self.assertEqual(upd["BuyRestrictionMax"], row["buyRestriction"])
            self.assertLessEqual(upd["BuyRestrictionMax"], upd["StackObjectsCount"])
            global_units += int(upd["StackObjectsCount"])
            player_units += int(upd["BuyRestrictionMax"])
            global_value += price * int(upd["StackObjectsCount"])
            player_value += price * int(upd["BuyRestrictionMax"])
        expected = self.audit["expected"]
        self.assertEqual(global_units, expected["baselineGlobalStockUnits"])
        self.assertEqual(player_units, expected["baselinePlayerBuyLimitUnits"])
        self.assertEqual(global_value, expected["baselineGlobalStockRubValue"])
        self.assertEqual(player_value, expected["baselinePlayerFullBuyRub"])

    def test_relationship_stock_is_loyalty_gated_bounded_and_not_quest_gated(self):
        global_units = player_units = global_value = player_value = 0
        levels = []
        for row in self.audit["relationshipOffers"]:
            offer_id = row["offerId"]
            item = self.roots[offer_id]
            upd = item["upd"]
            price = rub_price(self.assort, offer_id)
            loyalty = int(self.assort["loyal_level_items"][offer_id])
            self.assertEqual(item["_tpl"], row["tpl"])
            self.assertEqual(price, row["priceRub"])
            self.assertEqual(loyalty, row["loyaltyLevel"])
            self.assertIn(loyalty, (2, 3, 4))
            self.assertFalse(upd["UnlimitedCount"])
            self.assertEqual(upd["StackObjectsCount"], row["stockPerReset"])
            self.assertEqual(upd["BuyRestrictionMax"], 1)
            self.assertNotIn(offer_id, self.questassort["success"])
            levels.append(loyalty)
            global_units += int(upd["StackObjectsCount"])
            player_units += int(upd["BuyRestrictionMax"])
            global_value += price * int(upd["StackObjectsCount"])
            player_value += price * int(upd["BuyRestrictionMax"])
        expected = self.audit["expected"]
        self.assertEqual(sorted(set(levels)), [2, 3, 4])
        self.assertEqual(global_units, expected["relationshipGlobalStockUnits"])
        self.assertEqual(player_units, expected["relationshipPlayerBuyLimitUnits"])
        self.assertEqual(global_value, expected["relationshipGlobalStockRubValue"])
        self.assertEqual(player_value, expected["relationshipPlayerFullBuyRub"])

    def test_ammo_pressure_matches_authored_policy_and_preserves_headroom(self):
        units = sum(int(row["stockPerReset"]) for row in self.ammo["offers"].values())
        spend = sum(int(row["stockPerReset"]) * int(row["priceRub"]) for row in self.ammo["offers"].values())
        expected = self.audit["expected"]
        headroom = self.audit["policyHeadroom"]
        logistics = self.policy["logistics"]
        self.assertEqual(units, expected["ammoUnitsPerReset"])
        self.assertEqual(spend, expected["ammoFullResetSpendRub"])
        self.assertEqual(logistics["maximumAmmoUnitsAcrossPermanentOffersPerReset"], headroom["ammoUnitCeiling"])
        self.assertEqual(logistics["maximumAmmoFullResetSpendRub"], headroom["ammoSpendCeilingRub"])
        self.assertEqual(headroom["ammoUnitCeiling"] - units, headroom["ammoUnitHeadroom"])
        self.assertEqual(headroom["ammoSpendCeilingRub"] - spend, headroom["ammoSpendHeadroomRub"])
        self.assertLessEqual(units, headroom["ammoUnitCeiling"])
        self.assertLessEqual(spend, headroom["ammoSpendCeilingRub"])

    def test_milestone_and_total_player_spend_are_factual(self):
        ammo_tpls = {row["tpl"] for row in self.ammo["offers"].values()}
        ammo_ids = {offer_id for offer_id in self.milestone_ids if self.roots[offer_id]["_tpl"] in ammo_tpls}
        clearance_ids = self.milestone_ids - ammo_ids
        self.assertEqual(len(ammo_ids), self.audit["expected"]["ammoMilestoneOfferCount"])
        self.assertEqual(len(clearance_ids), 1)
        clearance_id = next(iter(clearance_ids))
        self.assertEqual(rub_price(self.assort, clearance_id), self.audit["expected"]["clearanceMilestoneRub"])
        milestone_spend = sum(rub_price(self.assort, offer_id) * int(self.roots[offer_id]["upd"]["BuyRestrictionMax"]) for offer_id in self.milestone_ids)
        self.assertEqual(milestone_spend, self.audit["expected"]["milestonePlayerFullBuyRub"])
        total = milestone_spend + self.audit["expected"]["baselinePlayerFullBuyRub"] + self.audit["expected"]["relationshipPlayerFullBuyRub"]
        self.assertEqual(total, self.audit["expected"]["allCurrentOffersPlayerFullBuyRub"])

    def test_special_weapons_remains_sample_only(self):
        self.assertTrue(self.audit["classification"]["specialWeapons"].startswith("sample-only"))
        self.assertFalse(self.ammo["specialWeapons"]["permanentOffer"])
        self.assertTrue(self.ammo["specialWeapons"]["sampleOnly"])
        self.assertFalse(self.policy["logistics"]["specialWeaponsPermanentOfferAllowed"])
        self.assertTrue(self.policy["logistics"]["specialWeaponsSampleOnly"])


if __name__ == "__main__":
    unittest.main()
