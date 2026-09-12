import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


def load(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8"))


class NatalyaStorefrontAbsorptionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.assort = load("db/natalya-signature-assort.json")
        cls.base = load("db/assort.json")
        cls.program = load("manifests/m7-natalya-absorption-program.json")
        cls.roots = [row for row in cls.assort["items"] if row.get("parentId") == "hideout"]

    def test_all_verified_native_weapon_presets_are_materialized(self):
        self.assertEqual(len(self.roots), 35)
        self.assertEqual(len(self.assort["items"]), 361)
        self.assertEqual([sum(row["loyaltyLevel"] == level for row in self.program["signatureOffers"])
                          for level in range(1, 5)], [9, 9, 9, 8])

    def test_published_offer_ids_remain_stable(self):
        expected = {
            "5bfea6e90db834001b7347f3": "67e6bf6525c53becfdba880e",
            "5df24cf80dee1b22f862e9bc": "67e6bf650c1627bd671e784b",
            "65268d8ecb944ff1e90ea385": "67e6bf65f74c6d7fd2b6730a",
            "6165ac306ef05c2ce828ef74": "67e6bf65c16dbddc52a3f240",
        }
        actual = {row["_tpl"]: row["_id"] for row in self.roots}
        for tpl, offer_id in expected.items():
            self.assertEqual(actual[tpl], offer_id)

    def test_every_offer_is_finite_complete_and_manifested(self):
        ids = {row["_id"] for row in self.assort["items"]}
        root_ids = {row["_id"] for row in self.roots}
        self.assertEqual(len(ids), len(self.assort["items"]))
        self.assertEqual(set(self.assort["barter_scheme"]), root_ids)
        self.assertEqual(set(self.assort["loyal_level_items"]), root_ids)
        self.assertEqual({row["offerId"] for row in self.program["signatureOffers"]}, root_ids)
        for row in self.assort["items"]:
            self.assertTrue(row.get("parentId") == "hideout" or row.get("parentId") in ids)
        for root in self.roots:
            self.assertFalse(root["upd"]["UnlimitedCount"])
            self.assertEqual(root["upd"]["StackObjectsCount"], 2)
            self.assertEqual(root["upd"]["BuyRestrictionMax"], 1)

    def test_signature_presets_do_not_duplicate_admiral_core_roots(self):
        base_tpls = {row["_tpl"] for row in self.base["items"] if row.get("parentId") == "hideout"}
        signature_tpls = {row["_tpl"] for row in self.roots}
        self.assertEqual(len(signature_tpls), 35)
        self.assertTrue(base_tpls.isdisjoint(signature_tpls))


if __name__ == "__main__":
    unittest.main()
