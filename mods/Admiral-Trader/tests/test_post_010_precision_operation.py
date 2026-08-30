import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class Post010PrecisionOperationTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.spec = json.loads(
            (ROOT / "manifests" / "post-010-precision-operation.json").read_text(encoding="utf-8")
        )

    def test_spec_is_non_materialized_and_bound_to_frozen_base(self):
        self.assertEqual(self.spec["schemaVersion"], 1)
        self.assertEqual(self.spec["status"], "post-0.1.0-authored-spec-only")
        self.assertEqual(
            self.spec["frozen010Base"],
            "053a62ff5f1cb545f13bc89a96bba3acd319a823",
        )
        self.assertFalse(self.spec["implementationAllowed"])
        self.assertFalse(self.spec["runtimeMaterialize"])

    def test_precision_operation_rejects_sniper_life_ladder(self):
        self.assertEqual(self.spec["sourceBundle"], "Sniper Life")
        anti_grind = self.spec["antiGrind"]
        self.assertLessEqual(anti_grind["maximumTargetCount"], 2)
        self.assertEqual(anti_grind["maximumRequiredSuccessfulRaids"], 1)
        self.assertTrue(anti_grind["noDistanceLadder"])
        self.assertTrue(anti_grind["noEscalatingSequels"])
        self.assertTrue(anti_grind["noWeaponCollection"])
        self.assertTrue(anti_grind["noPerMapCopies"])
        self.assertFalse(anti_grind["repeatable"])

    def test_copy_is_bilingual_and_fieldcraft_focused(self):
        for locale in ("en", "ru"):
            text = self.spec["playerText"][locale]
            for field in ("description", "started", "success"):
                self.assertGreater(len(text[field].strip()), 30)
        intent = " ".join(self.spec["objectiveIntent"]).lower()
        self.assertIn("precision", intent)
        self.assertIn("survive/extract", intent)
        self.assertIn("precision arsenal", intent)

    def test_runtime_semantics_fail_closed(self):
        gates = " ".join(self.spec["proofGates"]).lower()
        self.assertIn("exact spt 4.1.3", gates)
        self.assertIn("distance", gates)
        self.assertIn("survived/extraction", gates)
        self.assertIn("overlap", gates)
        self.assertIn("economy admiral", gates)

    def test_reward_doctrine_does_not_create_capability_faucet(self):
        reward = self.spec["rewardDoctrine"].lower()
        for forbidden in ("no sniper-rifle storefront", "weapon unlock", "optics faucet", "rare ammunition supply"):
            self.assertIn(forbidden, reward)

    def test_frozen_runtime_counts_are_unchanged(self):
        quest_files = sorted((ROOT / "db" / "quests").glob("*.json"))
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        root_offers = [item for item in assort["items"] if item.get("parentId") == "hideout"]
        self.assertEqual(len(quest_files), 31)
        self.assertEqual(len(root_offers), 11)


if __name__ == "__main__":
    unittest.main()
