import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRADER_ID = "d5c27bb3169f8dfbc13f6b69"


class InsuranceRuntimeTests(unittest.TestCase):
    def test_native_insurance_contract_is_complete(self):
        base = json.loads((ROOT / "db/base.json").read_text(encoding="utf-8"))
        dialogue = json.loads((ROOT / "db/dialogue.json").read_text(encoding="utf-8"))
        manifest = json.loads((ROOT / "manifests/runtime-manifest.json").read_text(encoding="utf-8"))
        self.assertEqual(base["_id"], TRADER_ID)
        self.assertEqual(base["insurance"], {
            "availability": True,
            "min_payment": 0,
            "min_return_hour": 6,
            "max_return_hour": 12,
            "max_storage_time": 120,
            "excluded_category": ["62e9103049c018f425059f38"],
        })
        self.assertEqual([row["insurance_price_coef"] for row in base["loyaltyLevels"]], [25, 22, 19, 16])
        self.assertEqual(set(dialogue), {
            "insuranceStart", "insuranceFound", "insuranceExpired", "insuranceComplete",
            "insuranceFailed", "insuranceFailedLabs", "insuranceFailedLabyrinth",
        })
        self.assertTrue(all(len(messages) >= 1 for messages in dialogue.values()))
        self.assertEqual(manifest["data"]["dialogue"], "db/dialogue.json")
        self.assertEqual(manifest["insurance"]["returnChancePercent"], 90)

    def test_registration_uses_spt_insurance_config_without_profile_mutation(self):
        source = (ROOT / "server/TraderRegistration.cs").read_text(encoding="utf-8")
        self.assertIn("InsuranceConfig insuranceConfig", source)
        self.assertIn("insuranceConfig.ReturnChancePercent.TryAdd(traderBase.Id, 90)", source)
        self.assertIn("Dialogue = dialogue", source)
        self.assertNotIn("InsuranceList", source)
        self.assertNotIn("InsuredItems", source)


if __name__ == "__main__":
    unittest.main()
