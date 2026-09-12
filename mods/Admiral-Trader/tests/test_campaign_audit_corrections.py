import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(relative):
    return json.loads((ROOT / relative).read_text(encoding="utf-8-sig"))


class CampaignAuditCorrectionTests(unittest.TestCase):
    def test_stage_pools_are_disjoint_and_cover_all_49_weapons_once(self):
        pools = load("manifests/weapon-family-runtime-pools.json")
        total = 0
        for family, complete in pools["families"].items():
            stages = pools["stagePools"][family]
            self.assertEqual(set(stages), {"qualification", "fieldwork", "munitions"})
            flattened = [tpl for stage in stages.values() for tpl in stage]
            self.assertTrue(all(stages.values()))
            self.assertEqual(len(flattened), len(set(flattened)))
            self.assertEqual(set(flattened), set(complete))
            total += len(flattened)
        self.assertEqual(total, 49)

    def test_each_runtime_arsenal_quest_uses_only_its_authored_stage_pool(self):
        pools = load("manifests/weapon-family-runtime-pools.json")["stagePools"]
        plan = load("manifests/weapon-ammo-runtime-plan.json")["quests"]
        quests = {load(path.relative_to(ROOT))["_id"]: load(path.relative_to(ROOT)) for path in (ROOT / "db/quests").glob("20-*.json")}
        for row in plan:
            kill = quests[row["id"]]["conditions"]["AvailableForFinish"][0]["counter"]["conditions"][0]
            self.assertEqual(kill["weapon"], pools[row["family"]][row["stage"]])

    def test_special_munitions_has_one_m576_sample_and_finite_unlock(self):
        qid, offer, tpl = "f1368cb3b69c3a4917c4f206", "3500e7b76f097a98ced5d61b", "5ede475339ee016e8c534742"
        quest = next(load(p.relative_to(ROOT)) for p in (ROOT / "db/quests").glob("20-*.json") if load(p.relative_to(ROOT))["_id"] == qid)
        samples = [r for r in quest["rewards"]["Success"] if r.get("items", [{}])[0].get("_tpl") == tpl]
        self.assertEqual(samples[0]["items"][0]["upd"]["StackObjectsCount"], 1)
        self.assertEqual(load("db/questassort.json")["success"][offer], qid)
        root = next(x for x in load("db/assort.json")["items"] if x["_id"] == offer)
        self.assertEqual((root["upd"]["StackObjectsCount"], root["upd"]["BuyRestrictionMax"]), (2, 2))

    def test_canonical_338_name_is_ucw_in_runtime_authorities(self):
        for relative in ("manifests/weapon-ammo-capabilities.json", "manifests/weapon-ammo-spt415-evidence.json", "db/locales/arsenal-en.json"):
            text = (ROOT / relative).read_text(encoding="utf-8-sig")
            self.assertIn("UCW", text)
            self.assertNotIn("UPZ", text)

    def test_only_audited_operations_use_the_normalized_reward_table(self):
        expected = {
            "acoustic-discipline": (7000, 30000, .008), "forward-reserve": (5000, 30000, .008),
            "low-profile": (6500, 38000, .009), "mobility-doctrine": (9000, 50000, .012),
            "borrowed-access": (7500, 44000, .010), "acoustic-contact": (9000, 50000, .012),
            "route-security": (8000, 50000, .012), "contractor-intercept": (15000, 75000, .018),
            "observation-window": (12000, 70000, .018), "heavy-assault": (15000, 85000, .020),
            "break-the-perimeter": (17000, 95000, .022), "internal-security": (22000, 135000, .030),
        }
        rewards = load("manifests/m3-campaign-reward-plan.json")["operationRewards"]
        self.assertEqual({k: (v["xp"], v["rub"], v["standing"]) for k, v in rewards.items()}, expected)


if __name__ == "__main__":
    unittest.main()
