import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]
RUB = "5449016a4bdc2d6f028b456f"


class FiniteOfferProgressionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.contract = json.loads(
            (ROOT / "manifests" / "finite-offer-progression.json").read_text(encoding="utf-8")
        )
        cls.assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        cls.baseline = json.loads(
            (ROOT / "manifests" / "baseline-stock.json").read_text(encoding="utf-8")
        )
        cls.ammo = json.loads(
            (ROOT / "manifests" / "ammo-offer-policy.json").read_text(encoding="utf-8")
        )
        cls.items = {
            item["_id"]: item
            for item in cls.assort["items"]
            if item.get("parentId") == "hideout"
        }

    def price(self, offer_id):
        schemes = self.assort["barter_scheme"][offer_id]
        self.assertEqual(len(schemes), 1)
        self.assertEqual(len(schemes[0]), 1)
        payment = schemes[0][0]
        self.assertEqual(payment["_tpl"], RUB)
        return payment["count"]

    def test_contract_is_bound_to_frozen_candidate_without_materialization(self):
        self.assertEqual(self.contract["schemaVersion"], 1)
        self.assertEqual(
            self.contract["frozen010Base"],
            "053a62ff5f1cb545f13bc89a96bba3acd319a823",
        )
        self.assertFalse(self.contract["changeControl"]["runtimeMutationInThisSlice"])
        self.assertEqual(len(self.items), 11)
        self.assertEqual(self.contract["aggregate"]["rootOfferCount"], 11)

    def test_baseline_aggregate_matches_runtime_assort(self):
        offers = self.baseline["offers"]
        stock_units = 0
        player_units = 0
        global_rub = 0
        player_rub = 0
        for offer in offers:
            item = self.items[offer["offerId"]]
            upd = item["upd"]
            price = self.price(offer["offerId"])
            self.assertFalse(upd["UnlimitedCount"])
            self.assertEqual(price, offer["priceRub"])
            self.assertEqual(upd["StackObjectsCount"], offer["stockPerReset"])
            self.assertEqual(upd["BuyRestrictionMax"], offer["buyRestriction"])
            self.assertLess(upd["BuyRestrictionMax"], upd["StackObjectsCount"])
            stock_units += upd["StackObjectsCount"]
            player_units += upd["BuyRestrictionMax"]
            global_rub += upd["StackObjectsCount"] * price
            player_rub += upd["BuyRestrictionMax"] * price

        expected = self.contract["classes"]["baseline"]
        self.assertEqual(len(offers), expected["offerCount"])
        self.assertEqual(stock_units, expected["stockUnitsPerReset"])
        self.assertEqual(player_units, expected["playerBuyUnitsPerReset"])
        self.assertEqual(global_rub, expected["globalStockRub"])
        self.assertEqual(player_rub, expected["playerFullBuyRub"])

    def test_ammunition_budget_matches_runtime_and_policy(self):
        stock_units = 0
        player_units = 0
        player_rub = 0
        quest_ids = set()
        for offer in self.ammo["offers"].values():
            runtime = next(item for item in self.items.values() if item["_tpl"] == offer["tpl"])
            upd = runtime["upd"]
            price = self.price(runtime["_id"])
            self.assertFalse(upd["UnlimitedCount"])
            self.assertEqual(upd["StackObjectsCount"], offer["stockPerReset"])
            self.assertEqual(upd["BuyRestrictionMax"], offer["buyRestriction"])
            self.assertEqual(price, offer["priceRub"])
            self.assertNotIn(offer["questId"], quest_ids)
            quest_ids.add(offer["questId"])
            stock_units += upd["StackObjectsCount"]
            player_units += upd["BuyRestrictionMax"]
            player_rub += upd["BuyRestrictionMax"] * price

        expected = self.contract["classes"]["munitionMilestones"]
        self.assertEqual(len(quest_ids), expected["offerCount"])
        self.assertEqual(stock_units, expected["stockUnitsPerReset"])
        self.assertEqual(player_units, expected["playerBuyUnitsPerReset"])
        self.assertEqual(player_rub, expected["playerFullBuyRub"])
        self.assertLessEqual(stock_units, expected["unitBudgetCeiling"])
        self.assertLessEqual(player_rub, expected["spendBudgetCeilingRub"])

    def test_clearance_and_total_spend_are_exact(self):
        baseline_ids = {offer["offerId"] for offer in self.baseline["offers"]}
        ammo_tpls = {offer["tpl"] for offer in self.ammo["offers"].values()}
        remaining = [
            item
            for offer_id, item in self.items.items()
            if offer_id not in baseline_ids and item["_tpl"] not in ammo_tpls
        ]
        self.assertEqual(len(remaining), 1)
        clearance = remaining[0]
        upd = clearance["upd"]
        self.assertFalse(upd["UnlimitedCount"])
        self.assertEqual(upd["StackObjectsCount"], 1)
        self.assertEqual(upd["BuyRestrictionMax"], 1)
        clearance_rub = self.price(clearance["_id"])
        self.assertEqual(
            clearance_rub,
            self.contract["classes"]["clearanceMilestone"]["playerFullBuyRub"],
        )

        total = (
            self.contract["classes"]["baseline"]["playerFullBuyRub"]
            + self.contract["classes"]["munitionMilestones"]["playerFullBuyRub"]
            + clearance_rub
        )
        self.assertEqual(total, self.contract["aggregate"]["playerFullBuyRubPerReset"])
        self.assertEqual(
            clearance_rub + self.contract["classes"]["munitionMilestones"]["playerFullBuyRub"],
            self.contract["aggregate"]["milestonePlayerFullBuyRubPerReset"],
        )

    def test_relationship_is_still_absent_and_cannot_fill_a_capability_gate(self):
        relationship = self.contract["classes"]["relationship"]
        self.assertEqual(relationship["offerCount"], 0)
        self.assertFalse(relationship["materialized"])
        rules = " ".join(relationship["rules"]).lower()
        self.assertIn("not replace access or munitions", rules)
        self.assertTrue(self.contract["changeControl"]["requireEconomyReviewForPriceStockOrNewOffer"])
        self.assertTrue(self.contract["changeControl"]["requireUnlockGraphReviewForMilestoneOfferChange"])


if __name__ == "__main__":
    unittest.main()
