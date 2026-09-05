import json
import unittest
from collections import deque
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"


def load_json(name: str):
    return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))


class M3CampaignProductSpecTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.product = load_json("m3-campaign-product-spec.json")
        cls.editorial = load_json("m3-campaign-editorial-copy.json")
        cls.rewards = load_json("m3-campaign-reward-plan.json")
        cls.uniqueness = load_json("m3-campaign-uniqueness-review.json")
        cls.progression = load_json("m3-campaign-progression.json")

    def test_design_files_remain_fail_closed(self):
        self.assertFalse(self.product["implementationAllowed"])
        self.assertFalse(self.product["runtimeMaterialize"])
        self.assertFalse(self.editorial["runtimeMaterialize"])
        self.assertFalse(self.rewards["runtimeMaterialize"])
        self.assertFalse(self.uniqueness["runtimeMaterialize"])
        self.assertFalse(self.progression["runtimeMaterialize"])
        self.assertTrue(self.product["materializationGate"]["requiresM1LifecyclePass"])
        self.assertTrue(self.product["materializationGate"]["requiresM2ExistingCampaignPass"])

    def test_campaign_keys_are_unique_and_cross_manifests_match(self):
        operations = self.product["operations"]
        product_keys = [row["key"] for row in operations]
        self.assertEqual(len(product_keys), len(set(product_keys)))
        self.assertEqual(len(product_keys), 12)

        editorial_keys = [row["key"] for row in self.editorial["quests"]]
        reward_keys = list(self.rewards["operationRewards"])
        fingerprint_keys = list(self.uniqueness["operationFingerprints"])
        progression_keys = list(self.progression["levels"])

        self.assertEqual(set(editorial_keys), set(product_keys))
        self.assertEqual(set(reward_keys), set(product_keys))
        self.assertEqual(set(fingerprint_keys), set(product_keys))
        self.assertEqual(set(progression_keys), set(product_keys))
        self.assertEqual(set(self.progression["prerequisites"]), set(product_keys))
        self.assertEqual(len(editorial_keys), len(set(editorial_keys)))

    def test_act_membership_is_exact_and_nonduplicated(self):
        product_keys = {row["key"] for row in self.product["operations"]}
        flattened = [key for act in self.product["acts"] for key in act["operations"]]
        self.assertEqual(set(flattened), product_keys)
        self.assertEqual(len(flattened), len(set(flattened)))

        operation_act = {row["key"]: row["act"] for row in self.product["operations"]}
        for act in self.product["acts"]:
            for key in act["operations"]:
                self.assertEqual(operation_act[key], act["key"])

    def test_progression_graph_is_acyclic_reachable_and_level_monotonic(self):
        keys = set(self.progression["levels"])
        prereqs = self.progression["prerequisites"]
        roots = set(self.progression["rootOperations"])

        self.assertTrue(roots)
        self.assertTrue(roots.issubset(keys))
        self.assertEqual(roots, {key for key, values in prereqs.items() if not values})

        for key, dependencies in prereqs.items():
            self.assertLessEqual(len(dependencies), 2, key)
            self.assertEqual(len(dependencies), len(set(dependencies)), key)
            for dependency in dependencies:
                self.assertIn(dependency, keys)
                self.assertNotEqual(dependency, key)
                self.assertLessEqual(
                    self.progression["levels"][dependency],
                    self.progression["levels"][key],
                    f"level regression {dependency} -> {key}",
                )

        dependents = {key: [] for key in keys}
        indegree = {key: len(prereqs[key]) for key in keys}
        for key, dependencies in prereqs.items():
            for dependency in dependencies:
                dependents[dependency].append(key)

        queue = deque(sorted(key for key, degree in indegree.items() if degree == 0))
        visited = []
        while queue:
            current = queue.popleft()
            visited.append(current)
            for dependent in dependents[current]:
                indegree[dependent] -= 1
                if indegree[dependent] == 0:
                    queue.append(dependent)

        self.assertEqual(set(visited), keys, "progression graph must be acyclic and reachable")
        self.assertEqual(len(visited), len(keys))

        reachable = set(roots)
        changed = True
        while changed:
            changed = False
            for key, dependencies in prereqs.items():
                if key not in reachable and all(dep in reachable for dep in dependencies):
                    reachable.add(key)
                    changed = True
        self.assertEqual(reachable, keys)

    def test_progression_branches_only_reference_admitted_operations(self):
        admitted = {row["key"] for row in self.product["operations"]}
        for branch, operations in self.progression["branches"].items():
            self.assertTrue(operations, branch)
            self.assertTrue(set(operations).issubset(admitted), branch)
        self.assertEqual(self.progression["graphContracts"]["operationCount"], len(admitted))

    def test_merged_and_held_operations_are_not_admitted_as_standalone(self):
        product_keys = {row["key"] for row in self.product["operations"]}
        legacy_decisions = {
            row["legacyKey"]: row["decision"] for row in self.product["mergedOrHeld"]
        }
        self.assertEqual(
            legacy_decisions,
            {
                "ballistic-head-test": "MERGE-INTO-HEAVY-ASSAULT",
                "precision-denial": "MERGE-INTO-OBSERVATION-WINDOW",
                "endurance-circuit": "HOLD-FOR-REWRITE",
            },
        )
        self.assertTrue(
            {"ballistic-head-test", "precision-denial", "endurance-circuit"}.isdisjoint(product_keys)
        )
        self.assertEqual(
            self.uniqueness["admissionOutcome"],
            {
                "admitted": 12,
                "merged": ["ballistic-head-test", "precision-denial"],
                "held": ["endurance-circuit"],
                "fillerAdded": 0,
            },
        )
        held_graph_keys = {row["legacyKey"] for row in self.progression["heldOutsideGraph"]}
        self.assertEqual(held_graph_keys, {"endurance-circuit"})

    def test_reward_plan_matches_product_spec_and_totals(self):
        product_rewards = {row["key"]: row["reward"] for row in self.product["operations"]}
        plan_rewards = self.rewards["operationRewards"]
        self.assertEqual(product_rewards, plan_rewards)

        total_xp = sum(row["xp"] for row in plan_rewards.values())
        total_rub = sum(row["rub"] for row in plan_rewards.values())
        total_standing = round(sum(row["standing"] for row in plan_rewards.values()), 6)
        summary = self.rewards["campaignTotals"]

        self.assertEqual(total_xp, summary["xp"])
        self.assertEqual(total_rub, summary["rub"])
        self.assertAlmostEqual(total_standing, summary["standing"], places=6)
        self.assertEqual(len(plan_rewards), summary["operationCount"])
        self.assertEqual(summary["itemRewardCount"], 0)
        self.assertEqual(summary["permanentUnlockCount"], 0)
        self.assertTrue(all(row["itemReward"] is None for row in plan_rewards.values()))

    def test_act_budgets_sum_to_campaign_totals(self):
        acts = self.rewards["actBudgets"]
        summary = self.rewards["campaignTotals"]
        self.assertEqual(sum(row["operationCount"] for row in acts), summary["operationCount"])
        self.assertEqual(sum(row["xp"] for row in acts), summary["xp"])
        self.assertEqual(sum(row["rub"] for row in acts), summary["rub"])
        self.assertAlmostEqual(sum(row["standing"] for row in acts), summary["standing"], places=6)

    def test_editorial_copy_is_complete_in_both_languages(self):
        seen_titles = set()
        for quest in self.editorial["quests"]:
            title = quest["title"]
            briefing = quest["briefing"]
            objective = quest["objective"]
            success = quest["success"]

            self.assertTrue(title["en"].strip())
            self.assertTrue(title["ru"].strip())
            self.assertNotEqual(title["en"].casefold(), title["ru"].casefold())
            self.assertNotIn(title["en"], seen_titles)
            seen_titles.add(title["en"])

            self.assertGreaterEqual(len(briefing["en"]), 120)
            self.assertGreaterEqual(len(briefing["ru"]), 120)
            self.assertGreaterEqual(len(success["en"]), 35)
            self.assertGreaterEqual(len(success["ru"]), 35)
            self.assertTrue(objective["en"])
            self.assertTrue(objective["ru"])
            self.assertEqual(len(objective["en"]), len(objective["ru"]))
            self.assertRegex(briefing["en"], r"[A-Za-z]")
            self.assertRegex(briefing["ru"], r"[А-Яа-яЁё]")

    def test_player_copy_does_not_leak_design_or_validator_language(self):
        forbidden = (
            "bounded",
            "allowlist",
            "anti-grind",
            "semantic overlap",
            "materialization",
            "x2/x5/x10",
            "exact tpl",
            "proven category",
            "final selected",
        )
        for quest in self.editorial["quests"]:
            chunks = []
            for field in ("briefing", "success"):
                chunks.extend(quest[field].values())
            for language in ("en", "ru"):
                chunks.extend(quest["objective"][language])
            text = "\n".join(chunks).casefold()
            for phrase in forbidden:
                self.assertNotIn(phrase, text, f"{quest['key']} leaks design phrase {phrase!r}")

    def test_operation_identity_is_not_just_a_count_or_equipment_ladder(self):
        for operation in self.product["operations"]:
            self.assertTrue(operation["playerProblem"].strip())
            self.assertTrue(operation["distinctiveDecision"].strip())
            self.assertTrue(operation["uniquenessBoundary"].strip())
            self.assertTrue(operation["mechanicIntent"])
            self.assertNotEqual(operation["playerProblem"], operation["distinctiveDecision"])

        product_keys = {row["key"] for row in self.product["operations"]}
        self.assertIn("mobility-doctrine", product_keys)
        self.assertIn("heavy-assault", product_keys)
        self.assertNotIn("ballistic-head-test", product_keys)
        self.assertIn("observation-window", product_keys)
        self.assertNotIn("precision-denial", product_keys)

    def test_uniqueness_conflict_groups_have_enforced_exit_conditions(self):
        admitted = {row["key"] for row in self.product["operations"]}
        groups = self.uniqueness["conflictGroups"]
        self.assertGreaterEqual(len(groups), 5)
        seen_names = set()
        for group in groups:
            self.assertNotIn(group["group"], seen_names)
            seen_names.add(group["group"])
            self.assertTrue(group["risk"].strip())
            self.assertTrue(group["mergeTrigger"].strip())
            for key in group["operations"]:
                self.assertIn(key, admitted)

    def test_fingerprints_are_nonempty_and_not_identical(self):
        fingerprints = self.uniqueness["operationFingerprints"]
        normalized = []
        for key, values in fingerprints.items():
            self.assertTrue(values, key)
            self.assertEqual(len(values), len(set(values)), key)
            normalized.append(tuple(values))
        self.assertEqual(len(normalized), len(set(normalized)))

    def test_russian_titles_avoid_known_machine_translation_calques(self):
        titles = {row["title"]["ru"] for row in self.editorial["quests"]}
        forbidden_titles = {
            "Дисциплина сигнала",
            "Точный запрет",
            "Калибровка защиты",
        }
        self.assertTrue(titles.isdisjoint(forbidden_titles))

    def test_no_duplicate_briefings_or_success_lines(self):
        en_briefings = [row["briefing"]["en"] for row in self.editorial["quests"]]
        ru_briefings = [row["briefing"]["ru"] for row in self.editorial["quests"]]
        en_success = [row["success"]["en"] for row in self.editorial["quests"]]
        ru_success = [row["success"]["ru"] for row in self.editorial["quests"]]
        self.assertEqual(len(en_briefings), len(set(en_briefings)))
        self.assertEqual(len(ru_briefings), len(set(ru_briefings)))
        self.assertEqual(len(en_success), len(set(en_success)))
        self.assertEqual(len(ru_success), len(set(ru_success)))

    def test_reward_values_are_positive_and_standing_is_bounded(self):
        for key, reward in self.rewards["operationRewards"].items():
            self.assertGreater(reward["xp"], 0, key)
            self.assertGreater(reward["rub"], 0, key)
            self.assertGreater(reward["standing"], 0, key)
            self.assertLessEqual(reward["standing"], 0.025, key)


if __name__ == "__main__":
    unittest.main()
