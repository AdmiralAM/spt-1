import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class RelationshipCurrentSliceDispositionTests(unittest.TestCase):
    def load(self, name):
        return json.loads((ROOT / "manifests" / name).read_text(encoding="utf-8"))

    def test_current_slice_is_complete_without_new_root_offers(self):
        relationship = self.load("relationship-stock.json")
        finite = self.load("finite-offer-progression.json")
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))

        self.assertEqual(relationship["schemaVersion"], 5)
        disposition = relationship["currentSliceDisposition"]
        self.assertEqual(disposition["mode"], "standing-uplift-only-no-new-root-offers")
        self.assertEqual(disposition["decision"], "close-new-relationship-offer-search-for-current-slice")
        self.assertEqual(disposition["rootOfferCountBefore"], 11)
        self.assertEqual(disposition["rootOfferCountAfter"], 11)
        self.assertEqual(disposition["newRelationshipOfferCount"], 0)
        self.assertFalse(disposition["runtimeMaterialize"])
        self.assertFalse(disposition["physicalCheckpointRequestedNow"])
        self.assertFalse(relationship["materialization"]["newOfferPathEnabledForCurrentSlice"])
        self.assertEqual(len(assort["items"]), 11)

        relationship_class = finite["classes"]["relationship"]
        self.assertEqual(relationship_class["offerCount"], 0)
        self.assertEqual(relationship_class["currentSliceNewRootOffers"], 0)
        self.assertEqual(relationship_class["rootOfferCountAfterCurrentSlice"], 11)
        self.assertFalse(relationship_class["newRootOfferPathOpen"])
        self.assertEqual(finite["aggregate"]["rootOfferCount"], 11)
        self.assertEqual(finite["aggregate"]["currentRelationshipRootOfferDelta"], 0)

    def test_relationship_progression_is_the_approved_field_marker_capacity_envelope(self):
        relationship = self.load("relationship-stock.json")
        uplift = self.load("relationship-standing-stock-uplift.json")

        expected = [
            {"loyaltyLevel": 1, "standing": 0.0, "stockPerReset": 12, "buyRestriction": 4},
            {"loyaltyLevel": 2, "standing": 0.1, "stockPerReset": 16, "buyRestriction": 6},
            {"loyaltyLevel": 3, "standing": 0.3, "stockPerReset": 20, "buyRestriction": 8},
            {"loyaltyLevel": 4, "standing": 0.55, "stockPerReset": 24, "buyRestriction": 10},
        ]
        self.assertEqual(relationship["currentSliceDisposition"]["tierEnvelope"], expected)
        self.assertEqual(uplift["upliftPlan"]["tiers"], [dict(x, state=s) for x, s in zip(expected, ["frozen-baseline", "post-0.1.0-economy-approved", "post-0.1.0-economy-approved", "post-0.1.0-economy-approved"])])
        self.assertEqual(uplift["upliftPlan"]["offerId"], "ad2000000000000000000004")
        self.assertEqual(uplift["upliftPlan"]["priceRub"], 16500)
        self.assertFalse(uplift["authority"]["newRootOffersAdded"])
        self.assertFalse(uplift["authority"]["runtimeMaterialize"])

    def test_current_tiers_do_not_encode_merchandise_quota(self):
        relationship = self.load("relationship-stock.json")
        self.assertEqual([x["loyaltyLevel"] for x in relationship["tiers"]], [2, 3, 4])
        self.assertTrue(all(x["selection"]["minimumRequiredOffers"] == 0 for x in relationship["tiers"]))
        self.assertTrue(all(x["selection"]["maximumOffers"] == 0 for x in relationship["tiers"]))
        self.assertTrue(all(x["selection"]["qualityGateOverridesCount"] for x in relationship["tiers"]))
        self.assertGreaterEqual(len(relationship["currentSliceDisposition"]["reopenNewOfferPathOnlyIf"]), 5)


if __name__ == "__main__":
    unittest.main()
