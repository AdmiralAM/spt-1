import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class M4SelectiveAbsorptionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = json.loads((ROOT / "manifests/m4-selective-content-absorption.json").read_text(encoding="utf-8"))
        cls.quests = {json.loads(path.read_text(encoding="utf-8"))["_id"] for path in (ROOT / "db/quests").glob("*.json")}

    def test_absorption_targets_are_real_runtime_records(self):
        quest_targets = set()
        for entry in self.plan["absorbed"]:
            quest_targets.update(x for x in entry["runtimeTargets"] if len(x) == 24 and x.isalnum())
        self.assertTrue(quest_targets)
        self.assertEqual(quest_targets - self.quests, set())

    def test_m4_is_bounded_and_does_not_duplicate_accepted_campaign(self):
        result = self.plan["runtimeResult"]
        self.assertGreaterEqual(len(self.quests), 43)
        self.assertEqual(result["questCountBefore"], 43)
        self.assertEqual(result["questCountAfter"], 43)
        self.assertEqual(result["newQuestRecords"], 0)
        self.assertEqual(result["retiredQuestRecords"], 0)
        self.assertEqual(result["graphChanges"], 0)
        self.assertEqual(result["balanceChanges"], 0)
        self.assertEqual(result["assortChanges"], 0)

    def test_external_runtime_scope_is_rejected(self):
        boundary = self.plan["boundary"]
        self.assertFalse(boundary["verbatimExternalCopy"])
        self.assertFalse(boundary["externalRuntimeDependency"])
        self.assertFalse(boundary["externalIdsImported"])
        self.assertFalse(boundary["externalRewardsImported"])
        self.assertTrue(boundary["m5RelationshipStorefrontStarted"])
        self.assertEqual(boundary["m5RelationshipStorefrontState"], "materialized-in-current-runtime")

    def test_source_rejections_cover_known_duplicate_families(self):
        rejected = {entry["source"] for entry in self.plan["rejected"]}
        self.assertTrue({"Natalya Pay Back", "Natalya Weapons Training", "Andrudis runtime corpus", "Boss Command Strike", "Cultist Night Operation"} <= rejected)


if __name__ == "__main__":
    unittest.main()
