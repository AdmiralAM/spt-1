import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]


class WeaponRotationExpansionPlanTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = json.loads(
            (ROOT / "manifests" / "weapon-rotation-expansion-plan.json").read_text(encoding="utf-8")
        )
        cls.runtime = json.loads(
            (ROOT / "manifests" / "weapon-rotation-runtime.json").read_text(encoding="utf-8")
        )
        cls.rewards = json.loads(
            (ROOT / "manifests" / "weapon-rotation-rewards.json").read_text(encoding="utf-8")
        )

    def test_two_twenty_assignment_lanes_are_authored(self):
        lanes = self.plan["lanes"]
        self.assertEqual(2, len(lanes))
        self.assertEqual([20, 20], sorted(len(entries) for entries in lanes.values()))
        for entries in lanes.values():
            self.assertEqual(list(range(1, 21)), [entry[0] for entry in entries])

    def test_every_assignment_uses_a_nonempty_exact_native_pool(self):
        pools = self.plan["pools"]
        referenced = []
        for entries in self.plan["lanes"].values():
            for _, pool_name, _, locations, _ in entries:
                self.assertIn(pool_name, pools)
                self.assertGreaterEqual(len(pools[pool_name]), 2)
                self.assertEqual(len(pools[pool_name]), len(set(pools[pool_name])))
                self.assertTrue(all(len(tpl) == 24 for tpl in pools[pool_name]))
                self.assertTrue(locations == ["any"] or 2 <= len(locations) <= 4)
                referenced.extend(pools[pool_name])
        self.assertGreaterEqual(len(set(referenced)), 100)

    def test_examples_and_broader_early_catalog_are_covered(self):
        early = []
        for entries in self.plan["lanes"].values():
            for _, pool_name, _, _, _ in entries[:8]:
                early.extend(self.plan["pools"][pool_name])
        expected = {
            "63171672192e68c5460cebc5",  # AUG A3
            "5ba26383d4351e00334c93d9",  # MP7A1
            "5bd70322209c4d00d7167b8f",  # MP7A2
            "57dc2fa62459775949412633",  # AKS-74U
            "5448bd6b4bdc2dfc2f8b4569",  # PM
            "576a581d2459771e7b1bc4f1",  # Grach
            "59e6152586f77473dc057aa1",  # VPO-136
            "574d967124597745970e7c94",  # SKS
            "5e870397991fd70db46995c8",  # Mossberg 590A1
        }
        self.assertTrue(expected.issubset(set(early)))

    def test_active_rotation_covers_complete_logical_families_at_sensible_stages(self):
        lanes = self.plan["lanes"]
        self.assertEqual(lanes["B-rifle-precision"][8][1], "battle-rifles")
        self.assertEqual(lanes["B-rifle-precision"][9][1], "nine-by-thirty-nine")
        self.assertEqual(
            set(self.plan["pools"]["nine-by-thirty-nine"]),
            {
                "644674a13d52156624001fbc",  # 9A-91
                "645e0c6b3b381ede770e1cc9",  # VSK-94
                "651450ce0e00edc794068371",  # SR-3M
                "57c44b372459772d2b39b8ce",  # AS VAL
                "57838ad32459774a17445cd2",  # VSS Vintorez
            },
        )
        self.assertEqual(
            set(self.plan["pools"]["sks-hunter"]),
            {"574d967124597745970e7c94", "587e02ff24597743df3deaeb", "5c501a4d2e221602b412b540"},
        )
        early_pools = {row[1] for lane in lanes.values() for row in lane[:4]}
        late_pools = {row[1] for lane in lanes.values() for row in lane[8:]}
        self.assertIn("service-pistols", early_pools)
        self.assertIn("early-smg", early_pools)
        self.assertIn("nine-by-thirty-nine", late_pools)
        self.assertIn("magnum-precision", late_pools)

    def test_all_forty_runtime_records_materialize_the_authored_family_matrix(self):
        assignments = self.runtime["assignments"]
        self.assertEqual(40, len(assignments))
        self.assertEqual({"A": 20, "B": 20}, self.runtime["laneCounts"])
        self.assertEqual(40, len({row["id"] for row in assignments}))
        self.assertTrue(self.runtime["stableQuestIdsRetained"])
        expected = []
        for lane, key in (("A", "A-close-support"), ("B", "B-rifle-precision")):
            expected.extend((lane, row[0], row[1]) for row in self.plan["lanes"][key])
        actual = [(row["lane"], row["order"], row["pool"]) for row in assignments]
        self.assertEqual(expected, actual)

    def test_runtime_quests_use_the_complete_native_and_optional_family(self):
        optional = json.loads((ROOT / "manifests" / "optional-weapon-runtime.json").read_text(encoding="utf-8"))
        optional_by_pool = {}
        for row in optional["acceptedWeapons"]:
            optional_by_pool.setdefault(row["pool"], []).append(row["tpl"])
        quest_by_id = {}
        for path in (ROOT / "db" / "quests").glob("*.json"):
            quest = json.loads(path.read_text(encoding="utf-8"))
            quest_by_id[quest["_id"]] = quest
        for row in self.runtime["assignments"]:
            weapons = []
            pending = [quest_by_id[row["id"]]["conditions"]["AvailableForFinish"]]
            while pending:
                value = pending.pop()
                if isinstance(value, dict):
                    if value.get("conditionType") == "Kills":
                        weapons.extend(value.get("weapon") or [])
                    pending.extend(value.values())
                elif isinstance(value, list):
                    pending.extend(value)
            expected = self.plan["pools"][row["pool"]] + optional_by_pool.get(row["pool"], [])
            self.assertEqual(expected, weapons, row["id"])

    def test_each_stage_offers_contrasting_weapon_roles(self):
        close_roles = {row[0]: row[1] for row in self.plan["lanes"]["A-close-support"]}
        rifle_roles = {row[0]: row[1] for row in self.plan["lanes"]["B-rifle-precision"]}
        for order in range(1, 21):
            self.assertNotEqual(close_roles[order], rifle_roles[order])
        self.assertEqual("service-pistols", close_roles[1])
        self.assertEqual("starter-service-rifles", rifle_roles[1])
        self.assertEqual("manual-shotguns", close_roles[3])
        self.assertEqual("compact-rifles", rifle_roles[3])
        self.assertEqual("light-machine-guns", close_roles[16])
        self.assertEqual("advanced-intermediate", rifle_roles[16])

    def test_every_weapon_assignment_has_a_staged_non_cash_reward(self):
        rows = self.rewards["rewards"]
        self.assertEqual(40, len(rows))
        self.assertEqual(10, self.rewards["completeWeaponRewards"])
        self.assertEqual(30, self.rewards["fieldSupportRewards"])
        self.assertEqual({row["id"] for row in self.runtime["assignments"]}, {row["questId"] for row in rows})
        self.assertEqual(
            {(lane, order) for lane in ("A", "B") for order in (4, 8, 12, 16, 20)},
            {(row["lane"], row["order"]) for row in rows if row["kind"] == "complete-weapon"},
        )
        quests = {}
        for path in (ROOT / "db" / "quests").glob("*.json"):
            quest = json.loads(path.read_text(encoding="utf-8"))
            quests[quest["_id"]] = quest
        for row in rows:
            success = quests[row["questId"]]["rewards"]["Success"]
            reward = next(reward for reward in success if reward["id"] == row["rewardId"])
            cash = next(reward for reward in success if reward.get("items", [{}])[0].get("_tpl") == "5449016a4bdc2d6f028b456f")
            self.assertGreaterEqual(cash["value"], self.rewards["minimumRemainingRoubles"])
            self.assertEqual(row["rootTemplate"], reward["items"][0]["_tpl"])
            self.assertEqual(row["itemTreeSize"], len(reward["items"]))
            if row["kind"] == "complete-weapon":
                self.assertGreater(len(reward["items"]), 1)

    def test_external_content_never_replaces_native_route(self):
        extension = self.plan["wttArmory"]
        self.assertFalse(extension["requiredDependency"])
        self.assertIn("native SPT weapon", extension["rule"])
        self.assertIn("No quest", extension["absenceBehavior"])

    def test_verified_wtt_sources_remain_optional_and_collision_free(self):
        candidates = json.loads(
            (ROOT / "manifests" / "optional-content-candidates.json").read_text(encoding="utf-8")
        )
        by_name = {entry["name"]: entry for entry in candidates["candidates"]}
        self.assertEqual("2.0.5", by_name["WTT Armory"]["runtime"]["version"])
        self.assertEqual("2.0.1", by_name["WTT Content Backport"]["runtime"]["version"])
        self.assertEqual("3.0.6", by_name["WTT CommonLib"]["runtime"]["version"])
        self.assertEqual(0, candidates["stableCampaignChanges"]["dependencies"])
        self.assertEqual(0, candidates["compatibilityEvidence"]["crossModTemplateIdCollisions"])
        self.assertEqual(0, candidates["compatibilityEvidence"]["nativeTemplateIdCollisions"])
        self.assertTrue(candidates["compatibilityEvidence"]["admiralRuntimeChangeRequired"])
        self.assertFalse(candidates["compatibilityEvidence"]["economyContract"]["foreignTemplateMutationAllowed"])


if __name__ == "__main__":
    unittest.main()
