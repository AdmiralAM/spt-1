import json
import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
POLICY = json.loads((ROOT / "manifests" / "unique-container-economy-policy.json").read_text(encoding="utf-8"))
SOURCE = (ROOT / "server" / "UniqueContainerEconomyRuntime.cs").read_text(encoding="utf-8")


def test_every_audited_container_is_runtime_normalized():
    rows = POLICY["items"]
    assert len(rows) == 21
    assert len({row["templateId"] for row in rows}) == len(rows)
    for row in rows:
        expected = rf'\["{row["templateId"]}"\]\s*=\s*new\({row["priceFloorRub"]:_}'.replace("_", r"[_ ]?")
        assert re.search(expected, SOURCE), row


def test_no_catalogued_container_can_be_cheap_or_ll1():
    for row in POLICY["items"]:
        assert row["priceFloorRub"] >= 300_000
        assert row["loyaltyLevel"] >= 2
        assert 1 <= row["stock"] <= 2
    ammo = next(row for row in POLICY["items"] if row["templateId"] == "ae9e418fd5d4c4eec4a0e6ea")
    assert ammo["priceFloorRub"] == 450_000


def test_runtime_enforces_offer_guards():
    assert "LoyalLevelItems[offer.Id] = policy.LoyaltyLevel" in SOURCE
    assert "BarterScheme[offer.Id]" in SOURCE
    assert "Template = Money.ROUBLES, Count = policy.PriceFloor" in SOURCE
    assert "offer.Upd.UnlimitedCount = false" in SOURCE
    assert "offer.Upd.StackObjectsCount = policy.Stock" in SOURCE
