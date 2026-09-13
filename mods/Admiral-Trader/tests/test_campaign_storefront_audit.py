import json
import re
import unittest
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


class CampaignStorefrontAuditTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.quests = [json.loads(path.read_text(encoding="utf-8")) for path in (ROOT / "db/quests").glob("*.json")]
        cls.ru = {}
        for path in (ROOT / "db/locales").glob("*ru.json"):
            cls.ru.update(json.loads(path.read_text(encoding="utf-8")))

    def test_graph_and_russian_player_copy_are_complete(self):
        ids = {quest["_id"] for quest in self.quests}
        self.assertEqual(len(self.quests), 172)
        self.assertEqual(len(ids), 172)
        for quest in self.quests:
            quest_id = quest["_id"]
            for suffix in ("name", "description", "startedMessageText", "successMessageText"):
                self.assertTrue(self.ru.get(f"{quest_id} {suffix}"), f"{quest_id} {suffix}")
            self.assertRegex(self.ru[f"{quest_id} name"], r"[А-Яа-яЁё]", quest_id)
            description = self.ru[f"{quest_id} description"]
            self.assertNotIn("Требования:", description, quest_id)
            self.assertNotIn("Награды:", description, quest_id)
            for condition in quest["conditions"]["AvailableForStart"]:
                if condition["conditionType"] == "Quest":
                    self.assertIn(condition["target"], ids, quest_id)

    def test_core_storefront_is_finite_complete_and_staged(self):
        assorts = [load("db/assort.json"), load("db/natalya-signature-assort.json")]
        all_roots = []
        for assort in assorts:
            item_ids = {item["_id"] for item in assort["items"]}
            roots = [item for item in assort["items"] if item.get("parentId") == "hideout"]
            all_roots.extend((root, assort) for root in roots)
            self.assertFalse([
                item["_id"] for item in assort["items"]
                if item.get("parentId") not in (None, "hideout") and item["parentId"] not in item_ids
            ])
            for root in roots:
                self.assertIn(root["_id"], assort["barter_scheme"])
                self.assertIn(root["_id"], assort["loyal_level_items"])
                self.assertFalse(root["upd"]["UnlimitedCount"])
        self.assertEqual(len(all_roots), 82)
        self.assertEqual(Counter(assort["loyal_level_items"][root["_id"]] for root, assort in all_roots), {1: 30, 2: 21, 3: 21, 4: 10})

    def test_editorial_copy_has_no_placeholder_briefs_or_success_messages(self):
        placeholder_fragments = (
            "Задача выполнена. Результат принят.",
            "Адмирал формирует долгую программу полевых испытаний.",
            "Подтвердите рабочее покрытие",
            "Подтвердите базовое покрытие",
            "Подтвердите позднее покрытие",
            "Подтвердите работоспособность доступа",
        )
        for quest in self.quests:
            quest_id = quest["_id"]
            description = self.ru[f"{quest_id} description"]
            success = self.ru[f"{quest_id} successMessageText"]
            for fragment in placeholder_fragments:
                self.assertNotIn(fragment, description, quest_id)
                self.assertNotIn(fragment, success, quest_id)


if __name__ == "__main__":
    unittest.main()
