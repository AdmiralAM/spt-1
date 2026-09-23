import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class TraderBuybackTests(unittest.TestCase):
    def test_admiral_buys_back_supported_equipment_and_progressively_improves_price(self):
        base = json.loads((ROOT / "db/base.json").read_text(encoding="utf-8"))

        self.assertEqual(
            base["items_buy"]["category"],
            [
                "5422acb9af1c889c16000029",  # weapons
                "5485a8684bdc2da71d8b4567",  # ammunition
                "5448fe124bdc2da5018b4567",  # weapon modifications
                "57864a66245977548f04a81f",
                "57864ee62459775490116fc1",
                "57864bb7245977548b3b66c2",
                "5c164d2286f774194c5e69fa",
                "5447e0e74bdc2d3c308b4567",
                "543be5f84bdc2dd4348b456a",  # equipment
                "5795f317245977243854e041",
                "5448e5284bdc2dcb718b4567",  # chest rigs
                "5448e53e4bdc2d60728b4567",  # backpacks
                "616eb7aea207f41933308f46",
            ],
        )
        payouts = [row["buy_price_coef"] for row in base["loyaltyLevels"]]
        self.assertEqual(payouts, [55, 50, 47, 44])
        self.assertTrue(all(a > b for a, b in zip(payouts, payouts[1:])))
        self.assertEqual(base["items_buy"]["id_list"], [])


if __name__ == "__main__":
    unittest.main()
