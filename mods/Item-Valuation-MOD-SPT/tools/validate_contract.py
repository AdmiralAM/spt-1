from __future__ import annotations

import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CONFIG = ROOT / "config" / "config.json"
SOURCE = ROOT / "server" / "Mod.cs"
PROJECT = ROOT / "server" / "ItemValuationModSpt.Server.csproj"


def fail(message: str) -> None:
    raise SystemExit(message)


config = json.loads(CONFIG.read_text(encoding="utf-8"))
source = SOURCE.read_text(encoding="utf-8")
project = PROJECT.read_text(encoding="utf-8")

expected_thresholds = [5000, 10000, 15000, 25000, 35000]
actual_thresholds = [
    config["badMaxValuePerSlot"],
    config["poorMaxValuePerSlot"],
    config["fairMaxValuePerSlot"],
    config["goodMaxValuePerSlot"],
    config["veryGoodMaxValuePerSlot"],
]
if actual_thresholds != expected_thresholds:
    fail(f"default tier thresholds drifted: {actual_thresholds}")

expected_colors = ["#404040", "#a3a3a3", "#0c3b08", "#08083b", "#590b5e", "#5e470b"]
actual_colors = [
    config["badColor"],
    config["poorColor"],
    config["fairColor"],
    config["goodColor"],
    config["veryGoodColor"],
    config["exceptionalColor"],
]
if actual_colors != expected_colors:
    fail(f"default tier colors drifted: {actual_colors}")

required_fragments = [
    "OnLoadOrder.PostLoad",
    "templateTable.Prices.TryGetValue",
    "templateTable.Handbook.Items",
    "properties.BackgroundColor = ValueTierClassifier.GetColor",
    "Math.Round(price / slots, MidpointRounding.AwayFromZero)",
    '"com.acidphantasm.itemvaluation"',
]
for fragment in required_fragments:
    if fragment not in source:
        fail(f"required background-only contract fragment missing: {fragment}")

forbidden_patterns = {
    "Harmony/client patching": r"\bHarmony\b|ModulePatch|BepInPlugin",
    "per-frame Unity callbacks": r"\bUpdate\s*\(|\bLateUpdate\s*\(",
    "polling/timers": r"setInterval|System\.Threading\.Timer|PeriodicTimer",
    "locale/name mutation": r"LocaleTable|ShortName\s*=|Description\s*=|\.Name\s*=",
    "category-specific valuation": r"Penetration|ArmorClass|BaseClasses|Ragfair|Trader|Preset",
    "client ItemView hook": r"ItemView",
}
for label, pattern in forbidden_patterns.items():
    if re.search(pattern, source):
        fail(f"forbidden {label} code found: {pattern}")

assignments = re.findall(r"properties\.([A-Za-z0-9_]+)\s*=", source)
if assignments != ["BackgroundColor"]:
    fail(f"template mutation surface must be BackgroundColor only; found {assignments}")

if "<SptRuntimeTarget>4.1.3</SptRuntimeTarget>" not in project:
    fail("SPT 4.1.3 runtime target missing")
if "<SptPublishedApiBaseline>4.1.2</SptPublishedApiBaseline>" not in project:
    fail("nearest published SPT API baseline missing")

print("Item Valuation MOD SPT source contract OK")
