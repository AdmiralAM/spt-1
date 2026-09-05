import importlib.util
import unittest
from pathlib import Path

MODULE_PATH = Path(__file__).resolve().parents[1] / "tools" / "audit_spt415_lifecycle_contract.py"
spec = importlib.util.spec_from_file_location("spt415_lifecycle_audit", MODULE_PATH)
module = importlib.util.module_from_spec(spec)
assert spec and spec.loader
spec.loader.exec_module(module)


class Spt415LifecycleContractAuditTests(unittest.TestCase):
    def test_summarizes_native_lifecycle_fields(self):
        quests = {
            "q1": {
                "acceptanceAndFinishingSource": "eft",
                "progressSource": "eft",
                "instantComplete": False,
                "restartable": False,
                "conditions": {"Started": [], "Success": [], "Fail": []},
                "rewards": {"Started": [], "Success": [{"type": "Experience"}], "Fail": []},
            },
            "q2": {
                "acceptanceAndFinishingSource": "eft",
                "instantComplete": False,
                "restartable": True,
                "status": 2,
                "conditions": {"Started": [{"conditionType": "Quest"}], "Success": [], "Fail": []},
                "rewards": {"Started": [{"type": "Item"}], "Success": [], "Fail": []},
            },
        }
        result = module.audit(quests)
        summary = result["exactVanilla"]
        self.assertEqual(result["targetSptVersion"], "4.1.5")
        self.assertEqual(summary["questCount"], 2)
        self.assertEqual(summary["acceptanceAndFinishingSource"], {"eft": 2})
        self.assertEqual(summary["progressSource"], {"<missing>": 1, "eft": 1})
        self.assertEqual(summary["instantComplete"], {"false": 2})
        self.assertEqual(summary["restartable"], {"false": 1, "true": 1})
        self.assertEqual(summary["statusPresenceCount"], 1)
        self.assertEqual(summary["sptStatusPresenceCount"], 0)
        self.assertEqual(summary["nonEmptyLifecycleConditions"]["Started"], 1)
        self.assertEqual(summary["nonEmptyLifecycleRewards"]["Started"], 1)
        self.assertEqual(summary["nonEmptyLifecycleRewards"]["Success"], 1)

    def test_rejects_non_object_database(self):
        with self.assertRaises(ValueError):
            module.audit([])


if __name__ == "__main__":
    unittest.main()
