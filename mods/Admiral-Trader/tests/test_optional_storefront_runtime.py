import json, unittest
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]

class OptionalStorefrontRuntimeTests(unittest.TestCase):
    def test_optional_sources_are_bounded_and_never_required(self):
        manifest=json.loads((ROOT/"manifests/optional-storefront-runtime.json").read_text(encoding="utf-8"))
        self.assertFalse(manifest["requiredDependencies"])
        self.assertEqual(manifest["offerCount"],62)
        self.assertEqual(manifest["totalAdmiralOffersWhenPresent"],113)
        self.assertGreaterEqual(sum(x["loyaltyLevel"] == 1 for x in manifest["offers"]),20)
        self.assertEqual({x["source"] for x in manifest["offers"]},{"WTT Armory","WTT Content Backport"})
        self.assertEqual(sum(x["category"] == "complete weapon" for x in manifest["offers"]),20)
        self.assertEqual(len(manifest["questRewardReplacements"]),5)
        self.assertTrue(all(x["mode"] == "replace-existing-item-reward" for x in manifest["questRewardReplacements"]))

    def test_optional_rewards_are_single_bounded_replacements(self):
        rewards=json.loads((ROOT/"db/optional/storefront/quest-reward-replacements.json").read_text(encoding="utf-8"))
        self.assertEqual(len(rewards),5)
        for quest_id,reward in rewards.items():
            self.assertEqual(reward["type"],"Item",quest_id)
            self.assertEqual(reward["value"],1,quest_id)
            self.assertEqual(reward["items"][0]["_id"],reward["target"],quest_id)
            self.assertTrue(all(x.get("upd",{}).get("StackObjectsCount")==1 for x in reward["items"]),quest_id)

    def test_optional_assorts_have_complete_native_shapes_and_unique_ids(self):
        seen=set()
        for filename,expected_roots in (("wtt-armory-assort.json",43),("content-backport-assort.json",19)):
            assort=json.loads((ROOT/"db/optional/storefront"/filename).read_text(encoding="utf-8"))
            roots=[x for x in assort["items"] if x.get("parentId")=="hideout"]
            self.assertEqual(len(roots),expected_roots)
            self.assertEqual(set(assort),{"items","barter_scheme","loyal_level_items"})
            self.assertEqual(set(assort["barter_scheme"]),{x["_id"] for x in roots})
            self.assertEqual(set(assort["loyal_level_items"]),{x["_id"] for x in roots})
            ids={x["_id"] for x in assort["items"]}
            self.assertEqual(len(ids),len(assort["items"]))
            self.assertFalse(seen & ids); seen |= ids
            for root in roots:
                self.assertFalse(root["upd"]["UnlimitedCount"])
                self.assertGreater(root["upd"]["BuyRestrictionMax"],0)
                self.assertLessEqual(root["upd"]["BuyRestrictionMax"],60)
                self.assertGreater(len([x for x in assort["items"] if x["_id"]==root["_id"] or x.get("parentId") in ids]),0)

if __name__=="__main__": unittest.main()
