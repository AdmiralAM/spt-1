import json
from pathlib import Path

ROOT = Path(__file__).parents[1]

def test_belt_progression_policy_is_bounded_and_owner_safe():
    policy = json.loads((ROOT / "manifests/belt-progression-policy.json").read_text(encoding="utf-8"))
    assert policy["owner"] == "SPT-Belt-Armband-Inventory"
    assert policy["fenceMayBypassProgression"] is False
    assert policy["ownerOverrideAllowed"] is True
    bands = policy["bands"]
    assert [(x["minimumCells"], x["maximumCells"], x["minimumPriceRub"]) for x in bands] == [
        (4, 7, 100000), (8, 11, 170000), (12, 14, 240000), (15, 16, 325000), (20, 2147483647, 450000)
    ]
    assert all(x["maximumStock"] <= 3 and x["minimumRestockSeconds"] >= 7200 for x in bands)
    known = policy["knownTemplates"]
    assert len({x["templateId"] for x in known}) == len(known)
    assert known == [{"templateId":"68ac0000000000000000000c","internalCells":4,"filterStrength":"narrow-magazine","protected":True,"loyaltyLevel":1,"minimumPriceRub":100000}]
