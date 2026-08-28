import json
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFEST = ROOT / "manifests" / "persistent-identities.json"
QUESTS = ROOT / "db" / "quests"
ASSORT = ROOT / "db" / "assort.json"
BASE = ROOT / "db" / "base.json"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class PersistentIdentityLedgerTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.ledger = load(MANIFEST)
        cls.base = load(BASE)
        cls.assort = load(ASSORT)
        cls.quest_ids = {load(path)["_id"] for path in QUESTS.glob("*.json")}
        cls.offer_ids = {
            row["_id"]
            for row in cls.assort["items"]
            if row.get("parentId") == "hideout"
        }

    def test_current_identity_sets_exactly_match_runtime(self):
        current = self.ledger["current"]
        self.assertEqual(current["traderIds"], [self.base["_id"]])
        self.assertEqual(set(current["questIds"]), self.quest_ids)
        self.assertEqual(set(current["offerIds"]), self.offer_ids)
        self.assertEqual(len(current["questIds"]), 31)
        self.assertEqual(len(current["offerIds"]), 11)

    def test_current_and_retired_ids_never_overlap(self):
        current = self.ledger["current"]
        retired = self.ledger["retired"]
        for domain in ("traderIds", "questIds", "offerIds"):
            self.assertEqual(len(current[domain]), len(set(current[domain])), domain)
            self.assertEqual(len(retired[domain]), len(set(retired[domain])), domain)
            self.assertTrue(set(current[domain]).isdisjoint(retired[domain]), domain)

    def test_retirement_policy_is_fail_closed(self):
        policy = self.ledger["policy"]
        self.assertTrue(policy["preserveDistributedIds"])
        self.assertFalse(policy["reuseRetiredIds"])
        self.assertFalse(policy["silentRemovalAllowed"])
        self.assertTrue(policy["retirementRequiresRecoveryCoverage"])


if __name__ == "__main__":
    unittest.main()
