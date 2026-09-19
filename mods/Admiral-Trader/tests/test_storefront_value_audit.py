import csv
import unittest
from collections import Counter
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class StorefrontValueAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        with (ROOT / "docs/storefront-value-audit.csv").open(encoding="utf-8-sig", newline="") as handle:
            cls.rows = list(csv.DictReader(handle))

    def test_complete_installed_and_optional_storefront_is_accounted_for(self):
        self.assertEqual(546, len(self.rows))
        self.assertEqual(546, len({row["offerId"] for row in self.rows}))
        self.assertEqual(
            {
                "Admiral core": 47,
                "Natalya": 35,
                "Artem": 281,
                "Painter": 7,
                "TGC runtime": 114,
                "WTT Armory optional": 43,
                "WTT Backport optional": 19,
            },
            Counter(row["source"] for row in self.rows),
        )

    def test_every_offer_has_a_payment_loyalty_and_stock_record(self):
        for row in self.rows:
            self.assertNotIn("missing-payment", row["flags"], row["offerId"])
            self.assertIn(int(row["loyalty"]), range(1, 5), row["offerId"])
            self.assertIn(row["globalStock"], {"finite", "unlimited"}, row["offerId"])
            self.assertTrue(row["stock"], row["offerId"])
            self.assertTrue(row["buyLimit"], row["offerId"])
            self.assertIn(row["route"].split("+")[0], {"cash", "barter"}, row["offerId"])

    def test_all_preserved_quest_unlocks_are_visible_in_the_audit(self):
        gated = [row for row in self.rows if row["questGate"]]
        self.assertEqual(62, len(gated))
        self.assertEqual(
            {"Admiral core": 18, "Artem": 41, "Painter": 3},
            Counter(row["source"] for row in gated),
        )

    def test_optional_sources_remain_explicitly_optional(self):
        for row in self.rows:
            expected = "true" if row["source"].startswith("WTT ") else "false"
            self.assertEqual(expected, row["optional"], row["offerId"])

    def test_unbounded_rows_are_only_the_eight_inherited_basic_ammo_or_magazine_routes(self):
        rows = [row for row in self.rows if "unbounded-purchase" in row["flags"]]
        self.assertEqual(8, len(rows))
        self.assertEqual({"Artem"}, {row["source"] for row in rows})
        self.assertEqual({"unlimited"}, {row["globalStock"] for row in rows})
        self.assertEqual({"none"}, {row["buyLimit"] for row in rows})

    def test_high_value_external_rows_have_effective_player_caps(self):
        rows = {row["offerId"]: row for row in self.rows}
        self.assertEqual("1", rows["672e2804a0529208b4e10e18"]["buyLimit"])
        self.assertEqual("1", rows["672e2e75a8f42643cd43c4b8"]["buyLimit"])
        self.assertNotIn("unbounded-purchase", rows["672e2804a0529208b4e10e18"]["flags"])
        self.assertNotIn("unbounded-purchase", rows["672e2e75a8f42643cd43c4b8"]["flags"])


if __name__ == "__main__":
    unittest.main()
