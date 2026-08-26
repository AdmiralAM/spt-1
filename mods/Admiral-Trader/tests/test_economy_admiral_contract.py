import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RUB_TPL = "5449016a4bdc2d6f028b456f"
SPECIAL_QUEST = "f1368cb3b69c3a4917c4f206"
SPECIAL_TPL = "6217726288ed9f0845317459"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


class EconomyAdmiralContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.contract = load(ROOT / "manifests" / "economy-admiral-contract.json")
        cls.assort = load(ROOT / "db" / "assort.json")
        cls.questassort = load(ROOT / "db" / "questassort.json")
        cls.quest_dir = ROOT / "db" / "quests"
        cls.quests = {}
        for path in cls.quest_dir.glob("*.json"):
            quest = load(path)
            cls.quests[str(quest.get("_id"))] = quest

    def test_identity_and_adapter_semantics(self):
        self.assertEqual(self.contract["schemaVersion"], 2)
        self.assertEqual(self.contract["product"], "Admiral Trader")
        self.assertEqual(self.contract["owner"], "Admiral Trader")
        self.assertEqual(self.contract["targetSptVersion"], "4.1.3")
        self.assertEqual(self.contract["traderId"], "d5c27bb3169f8dfbc13f6b69")
        self.assertEqual(self.contract["integration"]["consumer"], "Economy Admiral")
        self.assertEqual(self.contract["integration"]["adapterConfidence"], "ExplicitAdapter")
        self.assertEqual(self.contract["integration"]["progressionAuthority"], "quest-gate")
        self.assertIn("effectiveProgressionLevel", self.contract["integration"]["preserveByDefault"])

    def test_all_seven_renewable_offers_match_runtime_assort(self):
        offers = self.contract["renewableOffers"]
        self.assertEqual(len(offers), 7)
        items = {row["_id"]: row for row in self.assort["items"] if row.get("parentId") == "hideout"}
        self.assertEqual(set(items), {row["offerId"] for row in offers})
        success = self.questassort["Success"]

        for row in offers:
            offer_id = row["offerId"]
            item = items[offer_id]
            upd = item["upd"]
            self.assertEqual(item["_tpl"], row["itemTpl"])
            self.assertFalse(upd["UnlimitedCount"])
            self.assertEqual(upd["StackObjectsCount"], row["stockPerReset"])
            self.assertEqual(upd["BuyRestrictionMax"], row["buyRestrictionPerReset"])
            self.assertEqual(self.assort["loyal_level_items"][offer_id], row["loyaltyLevel"])
            self.assertEqual(success[offer_id], row["questGateId"])
            self.assertEqual(row["renewability"], "Bounded")
            self.assertTrue(row["permanent"])
            self.assertEqual(row["effectiveProgressionSource"], "questGate")

            scheme = self.assort["barter_scheme"][offer_id]
            self.assertEqual(len(scheme), 1)
            self.assertEqual(len(scheme[0]), 1)
            price = scheme[0][0]
            self.assertEqual(price["_tpl"], row["price"]["currencyTpl"])
            self.assertEqual(price["count"], row["price"]["amount"])
            self.assertEqual(price["_tpl"], RUB_TPL)

    def test_every_gate_references_committed_admiral_quest_and_level(self):
        for row in self.contract["renewableOffers"]:
            quest_id = row["questGateId"]
            self.assertIn(quest_id, self.quests)
            quest = self.quests[quest_id]
            self.assertEqual(row["effectiveProgressionLevel"], quest["conditions"]["AvailableForStart"][0]["value"])
            self.assertGreaterEqual(row["effectiveProgressionLevel"], 1)

    def test_special_weapons_is_one_time_sample_only(self):
        rewards = self.contract["oneTimeRewards"]
        self.assertEqual(len(rewards), 1)
        row = rewards[0]
        self.assertEqual(row["questId"], SPECIAL_QUEST)
        self.assertEqual(row["itemTpl"], SPECIAL_TPL)
        self.assertEqual(row["renewability"], "OneTime")
        self.assertFalse(row["permanent"])
        self.assertTrue(row["sampleOnly"])
        self.assertEqual(row["units"], 1)
        self.assertNotIn(SPECIAL_TPL, {offer["itemTpl"] for offer in self.contract["renewableOffers"]})

        quest = self.quests[SPECIAL_QUEST]
        self.assertEqual(row["effectiveProgressionLevel"], quest["conditions"]["AvailableForStart"][0]["value"])
        matching = []
        for reward in quest["rewards"]["Success"]:
            for item in reward.get("items") or []:
                if item.get("_tpl") == SPECIAL_TPL:
                    matching.append(item)
        self.assertEqual(len(matching), 1)
        self.assertEqual(matching[0]["upd"]["StackObjectsCount"], 1)

    def test_current_capability_offers_use_ll1_only_as_metadata(self):
        for row in self.contract["renewableOffers"]:
            self.assertEqual(row["loyaltyLevel"], 1)
            self.assertTrue(row["questGateId"])
            self.assertGreater(row["effectiveProgressionLevel"], row["loyaltyLevel"])
        self.assertEqual(self.contract["integration"]["loyaltyRole"], "metadata-only-for-current-capability-offers")

    def test_contract_is_declared_as_runtime_package_content(self):
        project = (ROOT / "server" / "AdmiralTrader.Server.csproj").read_text(encoding="utf-8-sig")
        self.assertIn('..\\manifests\\economy-admiral-contract.json', project)
        self.assertIn('Link="manifests\\economy-admiral-contract.json"', project)
        self.assertIn('CopyToOutputDirectory="PreserveNewest"', project)


if __name__ == "__main__":
    unittest.main()
