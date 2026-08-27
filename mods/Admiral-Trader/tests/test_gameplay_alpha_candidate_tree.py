import subprocess
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
VALIDATOR = ROOT / "tools" / "validate_gameplay_alpha_candidate_tree.py"


class GameplayAlphaCandidateTreeTests(unittest.TestCase):
    def test_source_tree_matches_candidate_composition(self) -> None:
        result = subprocess.run(
            [sys.executable, str(VALIDATOR), str(ROOT)],
            text=True,
            capture_output=True,
            check=False,
        )
        self.assertEqual(result.returncode, 0, msg=result.stdout + result.stderr)
        self.assertIn("offers=11", result.stdout)
        self.assertIn("milestoneUnlocks=7", result.stdout)
        self.assertIn("quests=31", result.stdout)


if __name__ == "__main__":
    unittest.main()
