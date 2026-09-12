import json
import unittest
from collections import deque
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"


def load_json(name: str):
    return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))


class M3PostConsolidationGraphTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.bridge = load_json("m3-post-consolidation-graph-bridge.json")
        cls.product = load_json("m3-campaign-product-spec.json")
        cls.plan = load_json("m3-baseline-consolidation-plan.json")

    def test_bridge_is_design_only_and_gated(self):
        self.assertFalse(self.bridge["runtimeMaterialize"])
        gate = self.bridge["materializationGate"]
        self.assertTrue(gate["requiresM1LifecyclePass"])
        self.assertTrue(gate["requiresM2ExistingCampaignPass"])
        self.assertTrue(gate["requiresMigrationSafeRetirement"])
        self.assertTrue(gate["requiresGraphCycleAndReachabilityValidation"])

    def test_current_external_edges_all_have_future_dispositions(self):
        rows = self.bridge["edgeDisposition"]
        operations = [row["m3Operation"] for row in rows]
        self.assertEqual(len(operations), len(set(operations)))
        self.assertEqual(
            set(operations),
            {"borrowed-access", "contractor-intercept", "observation-window", "internal-security"},
        )
        for row in rows:
            self.assertTrue(row["postConsolidationDecision"].strip())
            self.assertTrue(row["reason"].strip())

    def test_new_profile_graph_contains_core_m3_and_consolidated_capabilities(self):
        graph = self.bridge["newProfileGraphAfterConsolidation"]
        m3_keys = {row["key"] for row in self.product["operations"]}
        self.assertTrue(m3_keys.issubset(graph))
        self.assertIn("access-fundamentals", graph)
        self.assertIn("restricted-site-clearance", graph)
        self.assertIn("stand-off-control", graph)

    def test_new_profile_graph_has_no_retired_runtime_quest_ids(self):
        graph = self.bridge["newProfileGraphAfterConsolidation"]
        strings = set(graph)
        for deps in graph.values():
            strings.update(deps)
        retired_ids = {
            row["questId"]
            for row in self.plan["accessConsolidation"]["retirementCandidates"]
        }
        self.assertTrue(strings.isdisjoint(retired_ids))

    def test_graph_is_acyclic_reachable_and_prerequisites_exist(self):
        graph = self.bridge["newProfileGraphAfterConsolidation"]
        keys = set(graph)
        maximum = self.bridge["graphRules"]["maximumDirectPrerequisites"]

        for key, deps in graph.items():
            self.assertLessEqual(len(deps), maximum, key)
            self.assertEqual(len(deps), len(set(deps)), key)
            for dep in deps:
                self.assertIn(dep, keys, f"missing dependency {dep} for {key}")
                self.assertNotEqual(dep, key)

        dependents = {key: [] for key in keys}
        indegree = {key: len(graph[key]) for key in keys}
        for key, deps in graph.items():
            for dep in deps:
                dependents[dep].append(key)

        queue = deque(sorted(key for key, degree in indegree.items() if degree == 0))
        visited = []
        while queue:
            current = queue.popleft()
            visited.append(current)
            for nxt in dependents[current]:
                indegree[nxt] -= 1
                if indegree[nxt] == 0:
                    queue.append(nxt)

        self.assertEqual(set(visited), keys)
        self.assertEqual(len(visited), len(keys))

    def test_specialist_platform_remains_optional(self):
        optional = {row["key"]: row for row in self.bridge["optionalCapabilities"]}
        self.assertIn("specialist-platform", optional)
        self.assertFalse(optional["specialist-platform"]["mainCampaignGate"])
        graph_dependencies = {
            dep
            for deps in self.bridge["newProfileGraphAfterConsolidation"].values()
            for dep in deps
        }
        self.assertNotIn("specialist-platform", graph_dependencies)


if __name__ == "__main__":
    unittest.main()
