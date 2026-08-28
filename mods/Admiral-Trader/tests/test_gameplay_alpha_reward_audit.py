import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"
QUESTS = ROOT / "db" / "quests"
DB = ROOT / "db"
RUB_TPL = "5449016a4bdc2d6f028b456f"
TRADER_ID = "d5c27bb3169f8dfbc13f6b69"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


def success_rewards(quest):
    return (quest.get("rewards") or {}).get("Success") or []


def scalar_reward(quest, reward_type):
    rows = [row for row in success_rewards(quest) if row.get("type") == reward_type]
    if len(rows) != 1:
        raise AssertionError(f"{quest['_id']} expected exactly one {reward_type}, got {len(rows)}")
    return rows[0]


def rub_reward(quest):
    matches = []
    for row in success_rewards(quest):
        if row.get("type") != "Item":
            continue
        items = row.get("items") or []
        if len(items) == 1 and items[0].get("_tpl") == RUB_TPL:
            matches.append((row, items[0]))
    if len(matches) != 1:
        raise AssertionError(f"{quest['_id']} expected exactly one RUB reward, got {len(matches)}")
    row, item = matches[0]
    units = int((item.get("upd") or {}).get("StackObjectsCount", 0))
    return row, item, units


def non_rub_item_rewards(quest):
    result = []
    for row in success_rewards(quest):
        if row.get("type") != "Item":
            continue
        items = row.get("items") or []
        if len(items) == 1 and items[0].get("_tpl") == RUB_TPL:
            continue
        result.append(row)
    return result


class GameplayAlphaRewardAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.audit = load(MANIFESTS / "gameplay-alpha-reward-audit.json")
        cls.reward_policy = load(MANIFESTS / "reward-policy.json")
        cls.access = load(MANIFESTS / "keys-authored-spec.json")
        cls.arsenal_spec = load(MANIFESTS / "weapon-ammo-authored-spec.json")
        cls.arsenal_plan = load(MANIFESTS / "weapon-ammo-runtime-plan.json")
        cls.questassort = load(DB / "questassort.json")
        cls.runtime = {q["_id"]: q for q in (load(path) for path in sorted(QUESTS.glob("*.json")))}

        cls.expected = {}
        for row in cls.access["quests"]:
            budget = row["rewardBudget"]
            cls.expected[row["id"]] = {
                "domain": "access",
                "slug": row["slug"],
                "xp": int(budget["xp"]),
                "rub": int(budget["rub"]),
                "standing": float(budget["standing"]),
                "unlockSlots": int(budget["unlockSlots"]),
                "sampleUnits": None,
            }

        authored_by_slug = {
            stage["slug"]: stage
            for family in cls.arsenal_spec["families"]
            for stage in family["stages"]
        }
        plan_by_slug = {row["slug"]: row for row in cls.arsenal_plan["quests"]}
        if set(authored_by_slug) != set(plan_by_slug):
            raise AssertionError("arsenal authored/runtime-plan slug coverage drift")
        for slug, stage in authored_by_slug.items():
            runtime_row = plan_by_slug[slug]
            cls.expected[runtime_row["id"]] = {
                "domain": "arsenal",
                "slug": slug,
                "xp": int(stage["xp"]),
                "rub": int(stage["rub"]),
                "standing": float(stage["standing"]),
                "unlockSlots": int(stage.get("unlockSlots", 0)),
                "sampleUnits": int(stage["sampleAmmoUnits"]) if "sampleAmmoUnits" in stage else None,
            }

    def test_runtime_quest_set_exactly_matches_authored_backbone(self):
        expected = self.audit["expected"]
        self.assertEqual(len(self.expected), expected["questCount"])
        self.assertEqual(len(self.runtime), expected["questCount"])
        self.assertEqual(set(self.runtime), set(self.expected))
        self.assertEqual(sum(1 for row in self.expected.values() if row["domain"] == "access"), expected["accessQuestCount"])
        self.assertEqual(sum(1 for row in self.expected.values() if row["domain"] == "arsenal"), expected["arsenalQuestCount"])

    def test_every_materialized_reward_matches_authored_budget(self):
        totals = {"xp": 0, "rub": 0, "standing": 0.0}
        max_seen = {"xp": 0, "rub": 0, "standing": 0.0}
        for quest_id, budget in self.expected.items():
            quest = self.runtime[quest_id]
            xp = scalar_reward(quest, "Experience")
            standing = scalar_reward(quest, "TraderStanding")
            rub_row, rub_item, rub = rub_reward(quest)

            self.assertEqual(int(xp["value"]), budget["xp"], quest_id)
            self.assertAlmostEqual(float(standing["value"]), budget["standing"], places=8, msg=quest_id)
            self.assertEqual(standing.get("target"), TRADER_ID, quest_id)
            self.assertEqual(rub, budget["rub"], quest_id)
            self.assertEqual(int(rub_row["value"]), rub, quest_id)
            self.assertEqual(rub_row.get("target"), rub_item.get("_id"), quest_id)
            self.assertEqual(rub_item.get("_tpl"), RUB_TPL, quest_id)

            totals["xp"] += int(xp["value"])
            totals["rub"] += rub
            totals["standing"] += float(standing["value"])
            max_seen["xp"] = max(max_seen["xp"], int(xp["value"]))
            max_seen["rub"] = max(max_seen["rub"], rub)
            max_seen["standing"] = max(max_seen["standing"], float(standing["value"]))

        expected = self.audit["expected"]
        self.assertEqual(totals["xp"], expected["totalExperience"])
        self.assertEqual(totals["rub"], expected["totalRub"])
        self.assertAlmostEqual(totals["standing"], expected["totalTraderStanding"], places=8)
        self.assertEqual(max_seen["xp"], expected["maximumSingleQuestExperience"])
        self.assertEqual(max_seen["rub"], expected["maximumSingleQuestRub"])
        self.assertAlmostEqual(max_seen["standing"], expected["maximumSingleQuestStanding"], places=8)

    def test_every_item_reward_has_native_mail_root_identity(self):
        for quest_id, quest in self.runtime.items():
            for reward in success_rewards(quest):
                if reward.get("type") != "Item":
                    continue
                items = reward.get("items") or []
                self.assertGreater(len(items), 0, quest_id)
                target = reward.get("target")
                roots = [item for item in items if item.get("_id") == target]
                self.assertEqual(len(roots), 1, f"{quest_id}: Item reward target must identify exactly one reward root")
                root = roots[0]
                self.assertRegex(str(root.get("_id") or ""), r"^[0-9a-f]{24}$", quest_id)
                self.assertRegex(str(root.get("_tpl") or ""), r"^[0-9a-f]{24}$", quest_id)
                self.assertGreater(int((root.get("upd") or {}).get("StackObjectsCount", 0)), 0, quest_id)
                self.assertEqual(int(reward.get("value", 0)), int(root["upd"]["StackObjectsCount"]), quest_id)

    def test_current_rewards_stay_below_declared_vanilla_p90_envelope(self):
        envelope = self.audit["vanillaEnvelope"]
        policy_ref = self.reward_policy["observedReference"]["overall"]
        self.assertEqual(envelope["overallP90Experience"], policy_ref["xp"]["p90"])
        self.assertEqual(envelope["overallP90Rub"], policy_ref["rub"]["p90"])
        self.assertEqual(envelope["overallP90Standing"], policy_ref["standing"]["p90"])

        for quest_id, quest in self.runtime.items():
            xp = int(scalar_reward(quest, "Experience")["value"])
            standing = float(scalar_reward(quest, "TraderStanding")["value"])
            _, _, rub = rub_reward(quest)
            self.assertLessEqual(xp, envelope["overallP90Experience"], quest_id)
            self.assertLessEqual(rub, envelope["overallP90Rub"], quest_id)
            self.assertLessEqual(standing, envelope["overallP90Standing"], quest_id)

    def test_samples_exist_only_where_authored_and_never_exceed_budget(self):
        sample_quests = 0
        for quest_id, budget in self.expected.items():
            rewards = non_rub_item_rewards(self.runtime[quest_id])
            if budget["sampleUnits"] is None:
                self.assertEqual(rewards, [], quest_id)
                continue
            sample_quests += 1
            self.assertEqual(len(rewards), 1, quest_id)
            items = rewards[0].get("items") or []
            self.assertEqual(len(items), 1, quest_id)
            units = int((items[0].get("upd") or {}).get("StackObjectsCount", 0))
            self.assertEqual(units, budget["sampleUnits"], quest_id)

        self.assertEqual(sample_quests, self.audit["expected"]["munitionsSampleRewardCount"])

    def test_permanent_unlocks_are_exactly_authored_and_not_a_hidden_faucet(self):
        success = self.questassort["success"]
        gated_quest_ids = list(success.values())
        expected_unlock_quests = {qid for qid, row in self.expected.items() if row["unlockSlots"] == 1}
        self.assertEqual(len(success), self.audit["expected"]["permanentUnlockCount"])
        self.assertEqual(set(gated_quest_ids), expected_unlock_quests)
        self.assertEqual(len(gated_quest_ids), len(set(gated_quest_ids)))
        self.assertTrue(all(row["unlockSlots"] <= self.audit["antiFaucet"]["maximumPermanentUnlocksPerQuest"] for row in self.expected.values()))

        special_id = self.arsenal_plan["quests"][-1]["id"]
        self.assertEqual(self.arsenal_plan["quests"][-1]["slug"], "special-munitions")
        self.assertNotIn(special_id, gated_quest_ids)
        self.assertEqual(self.audit["expected"]["specialWeaponsPermanentUnlockCount"], 0)

    def test_all_current_quests_are_nonrepeatable(self):
        self.assertFalse(self.audit["antiFaucet"]["repeatableQuestsAllowed"])
        self.assertTrue(all(quest.get("restartable") is False for quest in self.runtime.values()))


if __name__ == "__main__":
    unittest.main()
