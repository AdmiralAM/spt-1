import json
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MANIFESTS = ROOT / "manifests"


def load(name):
    return json.loads((MANIFESTS / name).read_text(encoding="utf-8"))


def test_evacuation_plan_matches_canonical_campaign_keys_exactly():
    plan = load("post-010-evacuation-narrative-plan.json")
    campaign = load("post-010-campaign-progression.json")
    campaign_keys = [key for phase in campaign["phases"] for key in phase["operations"]]
    plan_keys = [operation["operationKey"] for operation in plan["operations"]]

    assert len(campaign_keys) == len(set(campaign_keys)) == 15
    assert plan_keys == campaign_keys
    assert plan["acceptance"]["operationCount"] == 15


def test_evacuation_plan_is_bilingual_and_uses_closed_classifications():
    plan = load("post-010-evacuation-narrative-plan.json")
    assert set(plan["classificationDefinitions"]) == {"natural", "supporting", "do-not-use"}

    for operation in plan["operations"]:
        assert operation["classification"] in plan["classificationDefinitions"]
        for field in ("narrativeRole", "allowedClaims", "forbiddenUnsupportedPromises"):
            assert set(operation[field]) == {"en", "ru"}
        assert all(operation["narrativeRole"][locale].strip() for locale in ("en", "ru"))
        for field in ("allowedClaims", "forbiddenUnsupportedPromises"):
            assert all(operation[field][locale] for locale in ("en", "ru"))
            assert all(text.strip() for locale in ("en", "ru") for text in operation[field][locale])


def test_evacuation_plan_has_no_runtime_materialization_surface():
    plan = load("post-010-evacuation-narrative-plan.json")
    assert plan["implementationAllowed"] is False
    assert plan["runtimeMaterialize"] is False
    assert plan["acceptance"]["runtimeReferencesAllowed"] is False
    assert plan["acceptance"]["runtimeLocalesMustRemainUnchanged"] is True

    manifest_name = "post-010-evacuation-narrative-plan.json"
    runtime_surfaces = [ROOT / "server", ROOT / "db", ROOT / "tools"]
    runtime_files = [path for surface in runtime_surfaces for path in surface.rglob("*") if path.is_file()]
    assert all(manifest_name not in path.read_text(encoding="utf-8", errors="ignore") for path in runtime_files)
