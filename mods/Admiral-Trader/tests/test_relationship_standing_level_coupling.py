import json
import pathlib
import unittest

ROOT = pathlib.Path(__file__).resolve().parents[1]


class RelationshipStandingLevelCouplingTests(unittest.TestCase):
    def test_policy_mirrors_real_admiral_loyalty_thresholds(self):
        base = json.loads((ROOT / "db" / "base.json").read_text(encoding="utf-8"))
        source = (ROOT / "server" / "RelationshipStandingStockPolicy.cs").read_text(encoding="utf-8")
        levels = base["loyaltyLevels"]

        expected = [
            (1, levels[0]["minStanding"], levels[0]["minLevel"], 12, 4),
            (2, levels[1]["minStanding"], levels[1]["minLevel"], 16, 6),
            (3, levels[2]["minStanding"], levels[2]["minLevel"], 20, 8),
            (4, levels[3]["minStanding"], levels[3]["minLevel"], 24, 10),
        ]
        for loyalty, standing, level, stock, buy in expected:
            self.assertIn(
                f"new({loyalty}, {standing:.2f}, {level}, {stock}, {buy})",
                source,
            )

    def test_projection_requires_both_profile_dimensions(self):
        resolver = (ROOT / "server" / "RelationshipStandingProfileResolver.cs").read_text(encoding="utf-8")
        coordinator = (ROOT / "server" / "RelationshipStandingAssortCoordinator.cs").read_text(encoding="utf-8")
        projection = (ROOT / "server" / "RelationshipStandingAssortProjection.cs").read_text(encoding="utf-8")
        policy = (ROOT / "server" / "RelationshipStandingStockPolicy.cs").read_text(encoding="utf-8")

        self.assertIn("pmcProfile?.Info?.Level", resolver)
        self.assertIn("out double standing, out int playerLevel", resolver)
        self.assertIn("out var standing, out var playerLevel", coordinator)
        self.assertIn("Apply(profileScopedAssort, standing, playerLevel)", coordinator)
        self.assertIn("Resolve(standing, playerLevel)", projection)
        self.assertIn("playerLevel < Ll2.MinimumPlayerLevel", policy)
        self.assertIn("playerLevel < Ll3.MinimumPlayerLevel", policy)
        self.assertIn("playerLevel < Ll4.MinimumPlayerLevel", policy)

    def test_materialization_remains_disabled(self):
        router = (ROOT / "server" / "RelationshipStandingAssortDynamicRouter.cs").read_text(encoding="utf-8")
        assort = json.loads((ROOT / "db" / "assort.json").read_text(encoding="utf-8"))
        self.assertIn("private const bool RuntimeMaterializationEnabled = false;", router)
        self.assertEqual(len(assort["items"]), 11)


if __name__ == "__main__":
    unittest.main()
