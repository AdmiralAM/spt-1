import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


class M8CampaignExpansionTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.plan = json.loads((ROOT / "manifests/weapon-rotation-expansion-plan.json").read_text(encoding="utf-8"))
        cls.runtime = json.loads((ROOT / "manifests/m8-campaign-expansion-runtime.json").read_text(encoding="utf-8"))
        cls.optional = json.loads((ROOT / "manifests/optional-weapon-runtime.json").read_text(encoding="utf-8"))
        cls.quests = [json.loads(path.read_text(encoding="utf-8")) for path in sorted((ROOT / "db/quests").glob("*.json"))]
        cls.by_id = {quest["_id"]: quest for quest in cls.quests}

    def test_campaign_shape_and_two_weapon_lanes(self):
        self.assertEqual(len(self.quests), 172)
        self.assertEqual(self.runtime["newQuestCount"], 29)
        self.assertEqual(self.runtime["weaponAssignments"], 19)
        self.assertEqual(self.runtime["maximumConcurrentWeaponAssignments"], 2)
        weapon_rows = [row for row in self.runtime["quests"] if row["kind"] == "weapon"]
        self.assertEqual({row["lane"] for row in weapon_rows}, {"A", "B"})
        for lane in ("A", "B"):
            rows = [row for row in weapon_rows if row["lane"] == lane]
            self.assertEqual([row["order"] for row in rows], list(range(1, len(rows) + 1)))

    def test_runtime_locations_use_exact_spt_ids(self):
        forbidden = {"Ground Zero", "Customs", "Factory", "Reserve", "Streets", "The Lab"}
        for row in self.runtime["quests"]:
            quest = self.by_id[row["id"]]
            text = json.dumps(quest)
            for label in forbidden:
                self.assertNotIn(f'"{label}"', text, (row["id"], label))
        ground_zero = [row for row in self.runtime["quests"] if row["kind"] == "operation"][:4]
        for row in ground_zero:
            text = json.dumps(self.by_id[row["id"]])
            self.assertIn("Sandbox", text)
            self.assertIn("Sandbox_high", text)

    def test_access_foundation_follows_ground_zero_entry(self):
        access = self.by_id["5d404ebd654de4efecef71d2"]
        prerequisites = [
            row["target"]
            for row in access["conditions"]["AvailableForStart"]
            if row.get("conditionType") == "Quest"
        ]
        self.assertEqual(prerequisites, ["02c07ee31821696597ceabef"])

    def test_optional_weapons_are_real_alternatives_with_native_fallback(self):
        accepted = self.optional["acceptedWeapons"]
        self.assertEqual(len(accepted), self.runtime["optionalWeaponCount"])
        self.assertFalse(self.optional["requiredDependency"])
        for row in [x for x in self.runtime["quests"] if x["kind"] == "weapon"]:
            quest = self.by_id[row["id"]]
            kill = quest["conditions"]["AvailableForFinish"][0]["counter"]["conditions"][0]
            native = set(self.plan["pools"][row["pool"]])
            self.assertTrue(native.issubset(set(kill["weapon"])), row["id"])
            expected_optional = {x["tpl"] for x in accepted if x["pool"] == row["pool"]}
            self.assertTrue(expected_optional.issubset(set(kill["weapon"])), row["id"])

    def test_optional_sources_do_not_become_dependencies(self):
        csproj = (ROOT / "server/AdmiralTrader.Server.csproj").read_text(encoding="utf-8")
        self.assertNotIn("WTT-Armory.dll", csproj)
        self.assertNotIn("WTT-ContentBackport.dll", csproj)
        self.assertEqual({x["guid"] for x in self.optional["sources"]}, {"com.wtt.armory", "com.wtt.contentbackport"})

    def test_icebreaker_is_reserved_without_speculative_runtime_ids(self):
        self.assertTrue(self.runtime["icebreaker"]["reserved"])
        self.assertFalse(self.runtime["icebreaker"]["runtimePublished"])
        for quest in self.quests:
            self.assertNotIn("icebreaker", json.dumps(quest).lower())

    def test_icebreaker_source_identity_is_recorded_without_runtime_dependency(self):
        candidates = json.loads((ROOT / "manifests/optional-content-candidates.json").read_text(encoding="utf-8"))
        icebreaker = next(row for row in candidates["candidates"] if row["name"] == "Icebreaker")
        self.assertFalse(icebreaker["installed"])
        self.assertEqual(icebreaker["sourceAudit"]["version"], "1.1.0")
        self.assertEqual(icebreaker["sourceAudit"]["guid"], "com.manimal.icebreaker")
        self.assertEqual(icebreaker["sourceAudit"]["locationKey"], "icebreaker")
        self.assertEqual(icebreaker["sourceAudit"]["locationId"], "882b2fa04bbd616567022938")
        self.assertEqual(icebreaker["sourceAudit"]["bundledQuestRecords"], 14)
        self.assertEqual(icebreaker["sourceAudit"]["runtimeValidation"], "pending installation")


if __name__ == "__main__":
    unittest.main()
