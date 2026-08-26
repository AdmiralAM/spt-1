import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "audit_baseline_stock_overlap.py"
spec = importlib.util.spec_from_file_location("baseline_stock_overlap", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


class BaselineStockOverlapTests(unittest.TestCase):
    def test_root_tpls_ignore_assort_children(self):
        data = {"items": [
            {"_id": "root", "_tpl": "a" * 24, "parentId": "hideout"},
            {"_id": "child", "_tpl": "b" * 24, "parentId": "root"},
        ]}
        self.assertEqual(module.root_tpls_from_assort(data), {"a" * 24})

    def test_documented_overlap_is_reported(self):
        proposal = {
            "schemaVersion": 1,
            "stockClass": "Baseline",
            "offers": [{
                "tpl": "a" * 24,
                "role": "field-support",
                "overlapPolicy": "Documented",
                "uniquenessRationale": "Admiral sells it as part of a bounded specialist kit rather than a general catalogue.",
            }],
        }
        result = module.audit(proposal, {"vanilla": {"a" * 24}, "scorpion": set()})
        row = result["offers"][0]
        self.assertEqual(row["overlapProviders"], ["vanilla"])
        self.assertEqual(result["summary"]["offersWithDocumentedOverlap"], 1)

    def test_unique_offer_fails_on_overlap(self):
        proposal = {
            "schemaVersion": 1,
            "stockClass": "Baseline",
            "offers": [{
                "tpl": "c" * 24,
                "role": "specialist-tool",
                "overlapPolicy": "Unique",
                "uniquenessRationale": "",
            }],
        }
        with self.assertRaises(ValueError):
            module.audit(proposal, {"scorpion": {"c" * 24}})

    def test_overlap_without_rationale_fails_closed(self):
        proposal = {
            "schemaVersion": 1,
            "stockClass": "Baseline",
            "offers": [{
                "tpl": "d" * 24,
                "role": "field-support",
                "overlapPolicy": "Documented",
                "uniquenessRationale": "",
            }],
        }
        with self.assertRaises(ValueError):
            module.audit(proposal, {"artem": {"d" * 24}})

    def test_duplicate_tpl_fails_closed(self):
        offer = {
            "tpl": "e" * 24,
            "role": "field-support",
            "overlapPolicy": "Documented",
            "uniquenessRationale": "intentional overlap",
        }
        proposal = {"schemaVersion": 1, "stockClass": "Baseline", "offers": [offer, dict(offer)]}
        with self.assertRaises(ValueError):
            module.audit(proposal, {"vanilla": set()})


if __name__ == "__main__":
    unittest.main()
