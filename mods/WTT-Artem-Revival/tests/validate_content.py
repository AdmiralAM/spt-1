import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / "server" / "Resources"
DB = RES / "db"
TRADER_ID = "66bf757f27d0b097db0acea5"
QUEST_ROOT = DB / "CustomQuests" / TRADER_ID


def load(path: Path):
    with path.open("r", encoding="utf-8-sig") as handle:
        return json.load(handle)


def fail(message: str):
    print(f"ERROR: {message}", file=sys.stderr)
    raise SystemExit(1)


def main():
    required = [
        RES / "bundles.json",
        DB / "base.json",
        DB / "assort.json",
        QUEST_ROOT / "Quests" / "ArtemQuests.json",
        QUEST_ROOT / "QuestAssort" / "Artem_QuestAssort.json",
        QUEST_ROOT / "Locales" / "artemenglish.json",
        DB / "CustomQuestZones" / "ArtemZones.json",
        DB / "CustomClothing" / "Artem Clothes.json",
    ]
    missing = [str(p.relative_to(ROOT)) for p in required if not p.exists()]
    if missing:
        fail("missing required resources: " + ", ".join(missing))

    quests = load(QUEST_ROOT / "Quests" / "ArtemQuests.json")
    assort = load(DB / "assort.json")
    quest_assort = load(QUEST_ROOT / "QuestAssort" / "Artem_QuestAssort.json")

    root_offers = {item["_id"]: item for item in assort["items"] if item.get("parentId") == "hideout"}
    barter = assort["barter_scheme"]
    loyalty = assort["loyal_level_items"]

    for offer_id in root_offers:
        if offer_id not in barter:
            fail(f"root offer {offer_id} has no barter_scheme")
        if offer_id not in loyalty:
            fail(f"root offer {offer_id} has no loyalty level")

    prereq_edges = []
    authored_success_unlocks = []
    for quest_id, quest in quests.items():
        image = quest.get("image", "")
        if image:
            image_name = image.rsplit("/", 1)[-1]
            if not (QUEST_ROOT / "Images" / image_name).exists():
                fail(f"quest {quest_id} references missing image {image_name}")

        for cond in quest.get("conditions", {}).get("AvailableForStart", []):
            if cond.get("conditionType") == "Quest":
                target = cond.get("target")
                if target not in quests:
                    fail(f"quest {quest_id} requires missing quest {target}")
                prereq_edges.append((target, quest_id))

        for reward in quest.get("rewards", {}).get("Success", []):
            if reward.get("type") == "AssortmentUnlock":
                target = reward.get("target")
                if target not in root_offers:
                    fail(f"quest {quest_id} unlocks missing assort offer {target}")
                authored_success_unlocks.append((target, quest_id))

    # Directed-cycle check for quest prerequisites.
    graph = {qid: [] for qid in quests}
    for src, dst in prereq_edges:
        graph[src].append(dst)
    visiting, visited = set(), set()

    def visit(node):
        if node in visiting:
            fail(f"quest dependency cycle reaches {node}")
        if node in visited:
            return
        visiting.add(node)
        for nxt in graph[node]:
            visit(nxt)
        visiting.remove(node)
        visited.add(node)

    for qid in quests:
        visit(qid)

    for status, mapping in quest_assort.items():
        if not isinstance(mapping, dict):
            fail(f"quest assort {status} is not an object")
        for offer_id, quest_id in mapping.items():
            if quest_id not in quests:
                fail(f"quest assort {status} maps {offer_id} to missing quest {quest_id}")
            if offer_id not in root_offers:
                fail(f"quest assort {status} references missing offer {offer_id}")

    success_mapping = quest_assort.get("success", {})
    for offer_id, quest_id in authored_success_unlocks:
        mapped = success_mapping.get(offer_id)
        if mapped != quest_id:
            fail(
                f"quest {quest_id} authors Success AssortmentUnlock {offer_id}, "
                f"but QuestAssort success maps it to {mapped!r}"
            )

    print(
        f"OK: {len(quests)} quests, {len(prereq_edges)} prerequisite edges, "
        f"{len(root_offers)} root offers, {len(authored_success_unlocks)} authored success unlocks"
    )


if __name__ == "__main__":
    main()
