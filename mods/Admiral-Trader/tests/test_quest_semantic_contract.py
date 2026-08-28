import json
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
QUEST_DIR = ROOT / "db" / "quests"
LOCALE_DIR = ROOT / "db" / "locales"


def load(path: Path):
    return json.loads(path.read_text(encoding="utf-8"))


class QuestSemanticContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [load(path) for path in sorted(QUEST_DIR.glob("*.json"))]
        cls.quest_by_id = {q["_id"]: q for q in cls.quests}
        cls.en = {}
        cls.ru = {}
        for name in ("en.json", "arsenal-en.json", "gameplay-alpha-en.json", "objectives-en.json"):
            cls.en.update(load(LOCALE_DIR / name))
        for name in ("ru.json", "arsenal-ru.json", "gameplay-alpha-ru.json", "objectives-ru.json"):
            cls.ru.update(load(LOCALE_DIR / name))
        cls.questassort = load(ROOT / "db" / "questassort.json")["success"]
        cls.capabilities = load(ROOT / "manifests" / "weapon-ammo-capabilities.json")["families"]
        cls.ammo_offers = load(ROOT / "manifests" / "ammo-offer-policy.json")["offers"]

    def test_expected_backbone_is_exact(self):
        self.assertEqual(len(self.quests), 31)
        self.assertEqual(len(self.quest_by_id), 31)

    def test_every_finish_condition_has_player_facing_objective(self):
        for quest in self.quests:
            finish = (quest.get("conditions") or {}).get("AvailableForFinish") or []
            self.assertEqual(len(finish), 1, quest["_id"])
            cid = finish[0]["id"]
            self.assertIn(cid, self.en, quest["_id"])
            self.assertIn(cid, self.ru, quest["_id"])
            self.assertTrue(self.en[cid].strip(), quest["_id"])
            self.assertTrue(self.ru[cid].strip(), quest["_id"])

    def test_arsenal_stage_mechanics_match_names(self):
        qualification_count = 0
        for quest in self.quests:
            name = quest.get("QuestName", "")
            if not name.startswith("Arsenal Protocol:"):
                continue
            finish = quest["conditions"]["AvailableForFinish"][0]
            self.assertEqual(finish.get("conditionType"), "CounterCreator", quest["_id"])
            self.assertEqual(quest.get("type"), "Elimination", quest["_id"])
            kill = finish["counter"]["conditions"][0]
            self.assertEqual(kill.get("conditionType"), "Kills", quest["_id"])
            self.assertTrue(kill.get("weapon"), quest["_id"])
            if name.endswith("Qualification"):
                qualification_count += 1
                self.assertEqual(int(finish.get("value", 0)), 1, quest["_id"])
                self.assertFalse(kill.get("weaponCaliber"), quest["_id"])
            elif name.endswith("Fieldwork") or name.endswith("Munitions"):
                self.assertGreater(int(finish.get("value", 0)), 1, quest["_id"])
            else:
                self.fail(f"unknown Arsenal stage naming: {name}")
        self.assertEqual(qualification_count, 7)

    def test_munitions_unlocks_match_backend_payoff(self):
        quest_to_offer = {quest_id: offer_id for offer_id, quest_id in self.questassort.items()}
        expected_munitions = set()
        for family, offer in self.ammo_offers.items():
            qid = str(offer["questId"])
            expected_munitions.add(qid)
            self.assertIn(qid, self.quest_by_id, family)
            self.assertIn(qid, quest_to_offer, family)
            quest = self.quest_by_id[qid]
            self.assertTrue(quest["QuestName"].endswith("Munitions"), qid)
            success = (quest.get("rewards") or {}).get("Success") or []
            item_rewards = [r for r in success if r.get("type") == "Item"]
            capability = self.capabilities[family]
            tpl = capability["tpl"]
            self.assertTrue(any(any(item.get("_tpl") == tpl for item in (r.get("items") or [])) for r in item_rewards), qid)
        self.assertEqual(set(quest_to_offer) - {"68a6527a3c73b2e85977d7a1"}, expected_munitions)

    def test_access_unlock_is_only_clearance(self):
        access = [q for q in self.quests if q.get("QuestName", "").startswith("Access Protocol:")]
        self.assertEqual(len(access), 10)
        mapped_access = [q for q in access if q["_id"] in self.questassort.values()]
        self.assertEqual([q["_id"] for q in mapped_access], ["68a6527a3c73b2e85977d7a1"])

    def test_reward_and_text_contracts_do_not_claim_nonexistent_item_unlocks(self):
        unlock_quests = set(self.questassort.values())
        positive_unlock_phrases = (
            "completion unlocks",
            "now unlocked",
            "now available",
            "is now unlocked",
            "allotment is now available",
            "offer is now unlocked",
        )
        for quest in self.quests:
            qid = quest["_id"]
            text = " ".join(self.en.get(f"{qid} {field}", "") for field in ("description", "successMessageText", "completePlayerMessage")).lower()
            claims_unlock = any(phrase in text for phrase in positive_unlock_phrases)
            if claims_unlock:
                self.assertIn(qid, unlock_quests, qid)

    def test_special_weapons_negative_unlock_statement_matches_backend(self):
        qid = "f1368cb3b69c3a4917c4f206"
        text = " ".join(self.en.get(f"{qid} {field}", "") for field in ("description", "successMessageText", "completePlayerMessage")).lower()
        self.assertTrue("does not unlock" in text or "no permanent ammunition offer is unlocked" in text)
        self.assertNotIn(qid, set(self.questassort.values()))


if __name__ == "__main__":
    unittest.main()
