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

        self.assertEqual(review["schemaVersion"], 2)
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

        compass = reviewed["5f4f9eb969cdc30ff33f09db"]
        self.assertEqual(compass["decision"], "reject-existing-admiral-baseline-and-pinned-jaeger")
        self.assertEqual(compass["evidence"]["trader"], "Jaeger")
        self.assertTrue(compass["evidence"]["unlimitedCount"])
        self.assertEqual(compass["evidence"]["stackObjectsCount"], 9999999)
        self.assertEqual(compass["evidence"]["frozenAdmiralOfferId"], "ad2000000000000000000001")

        rangefinder = reviewed["61605e13ffa6e502ac5e7eef"]
        self.assertEqual(rangefinder["decision"], "reject-redundant-pinned-jaeger")
        self.assertEqual(rangefinder["evidence"]["trader"], "Jaeger")
        self.assertTrue(rangefinder["evidence"]["unlimitedCount"])
        self.assertEqual(rangefinder["evidence"]["stackObjectsCount"], 9999999)
        self.assertFalse(rangefinder["evidence"]["frozenAdmiralDirectTplHit"])

        paracord = reviewed["5c12688486f77426843c7d32"]
        self.assertEqual(paracord["decision"], "reject-existing-admiral-baseline-and-barter-pressure")
        self.assertTrue(paracord["evidence"]["jaegerBarterInput"])
        self.assertEqual(paracord["evidence"]["barterCount"], 12)
        self.assertFalse(paracord["evidence"]["directPinnedJaegerSale"])
        self.assertEqual(paracord["evidence"]["frozenAdmiralOfferId"], "ad2000000000000000000002")

        live_by_tpl = {item["_tpl"]: item["_id"] for item in assort["items"]}
        self.assertEqual(live_by_tpl["5f4f9eb969cdc30ff33f09db"], "ad2000000000000000000001")
        self.assertEqual(live_by_tpl["5c12688486f77426843c7d32"], "ad2000000000000000000002")
        self.assertNotIn("61605e13ffa6e502ac5e7eef", live_by_tpl)
        self.assertEqual(len(assort["items"]), 11)


if __name__ == "__main__":
    unittest.main()
