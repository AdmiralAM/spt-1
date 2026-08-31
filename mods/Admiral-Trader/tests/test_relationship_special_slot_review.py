import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class RelationshipSpecialSlotReviewTests(unittest.TestCase):
    def test_reviewed_special_slot_candidates_do_not_materialize(self):
        review = json.loads(
            (ROOT / "manifests" / "relationship-special-slot-review.json").read_text(encoding="utf-8")
        )
        relationship = json.loads(
            (ROOT / "manifests" / "relationship-stock.json").read_text(encoding="utf-8")
        )
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))

        self.assertEqual(review["schemaVersion"], 1)
        self.assertFalse(review["policy"]["materialize"])
        self.assertFalse(relationship["materialization"]["enabled"])
        self.assertEqual(review["conclusion"]["approvedOfferCount"], 0)
        self.assertTrue(review["conclusion"]["relationshipMaterializationStillDisabled"])

        reviewed = {entry["tpl"]: entry for entry in review["reviewed"]}
        self.assertEqual(
            set(reviewed),
            {
                "5f4f9eb969cdc30ff33f09db",
                "61605e13ffa6e502ac5e7eef",
                "5c12688486f77426843c7d32",
            },
        )

        for tpl in ("5f4f9eb969cdc30ff33f09db", "61605e13ffa6e502ac5e7eef"):
            candidate = reviewed[tpl]
            self.assertEqual(candidate["decision"], "reject-redundant-pinned-jaeger")
            self.assertEqual(candidate["evidence"]["trader"], "Jaeger")
            self.assertTrue(candidate["evidence"]["unlimitedCount"])
            self.assertEqual(candidate["evidence"]["stackObjectsCount"], 9999999)
            self.assertTrue(candidate["evidence"]["offerId"].startswith("686e34"))

        paracord = reviewed["5c12688486f77426843c7d32"]
        self.assertEqual(paracord["decision"], "reject-barter-pressure-and-nonconsumable-utility")
        self.assertTrue(paracord["evidence"]["jaegerBarterInput"])
        self.assertEqual(paracord["evidence"]["barterCount"], 12)
        self.assertFalse(paracord["evidence"]["directPinnedJaegerSale"])

        live_tpls = {item["_tpl"] for item in assort["items"]}
        self.assertTrue(set(reviewed).isdisjoint(live_tpls))
        self.assertEqual(len(assort["items"]), 11)


if __name__ == "__main__":
    unittest.main()
