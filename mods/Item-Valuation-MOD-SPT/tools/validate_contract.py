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

expected_thresholds = [10000, 25000, 50000, 75000, 100000, 250000]
actual_thresholds = [
    config["tintStartValue"],
    config["lightGreenMaxValue"],
    config["greenMaxValue"],
    config["navyMaxValue"],
    config["violetMaxValue"],
    config["redMaxValue"],
]
if actual_thresholds != expected_thresholds:
    fail(f"default tier thresholds drifted: {actual_thresholds}")

expected_colors = ["#526B3F", "#294F31", "#253552", "#4A3854", "#5A2C31", "#5C4825"]
actual_colors = [
    config["lightGreenColor"],
    config["greenColor"],
    config["navyColor"],
    config["violetColor"],
    config["redColor"],
    config["goldColor"],
]
if actual_colors != expected_colors:
    fail(f"default tier colors drifted: {actual_colors}")

required_fragments = [
    "OnLoadOrder.PostLoad",
    "templateTable.Prices.TryGetValue",
    "templateTable.Handbook.Items",
    "BaseClasses.WEAPON",
    "BaseClasses.KEY",
    "BaseClasses.ARMORED_EQUIPMENT",
    "BaseClasses.VEST",
    "itemHelper.IsOfBaseclasses(templateId, TotalValueBaseClasses)",
    "Math.Round(price / slots, MidpointRounding.AwayFromZero)",
    "if (value < config.TintStartValue) return null",
    "if (color is null)",
    "properties.BackgroundColor = color",
    '"com.acidphantasm.itemvaluation"',
]
for fragment in required_fragments:
    if fragment not in source:
        fail(f"required background-only contract fragment missing: {fragment}")

# These legacy behaviours are specifically forbidden. BaseClasses/ItemHelper are allowed only
# for the four lightweight total-value category checks above.
forbidden_patterns = {
    "Harmony/client patching": r"\bHarmony\b|ModulePatch|BepInPlugin",
    "per-frame Unity callbacks": r"\bUpdate\s*\(|\bLateUpdate\s*\(",
    "polling/timers": r"setInterval|System\.Threading\.Timer|PeriodicTimer|Task\.Delay",
    "locale/name mutation": r"LocaleTable|ShortName\s*=|Description\s*=|\.Name\s*=",
    "legacy semantic valuation": r"PenetrationPower|ArmorClass|RagfairServerHelper|TradersTable|PresetHelper|ResolveBestTrader|GetHighestTrader",
    "client ItemView hook": r"ItemView",
}
for label, pattern in forbidden_patterns.items():
    if re.search(pattern, source):
        fail(f"forbidden {label} code found: {pattern}")

# Ammo must not regain a special penetration or category path; its monetary value follows the
# ordinary per-slot path (normally 1x1).
if "BaseClasses.AMMO" in source:
    fail("ammo must not have a special category valuation path")

assignments = re.findall(r"properties\.([A-Za-z0-9_]+)\s*=", source)
if assignments != ["BackgroundColor"]:
    fail(f"template mutation surface must be BackgroundColor only; found {assignments}")

if "<SptRuntimeTarget>4.1.3</SptRuntimeTarget>" not in project:
    fail("SPT 4.1.3 runtime target missing")
if "<SptPublishedApiBaseline>4.1.2</SptPublishedApiBaseline>" not in project:
    fail("nearest published SPT API baseline missing")

print("Item Valuation MOD SPT source contract OK")
