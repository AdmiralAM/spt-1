from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = ROOT / "src" / "Plugin.cs"
text = PLUGIN.read_text(encoding="utf-8-sig")
violations = []

required = [
    "if (!TryDetectLegacyBeltSlot(out bool legacyBeltSlotDetected))",
    "failing closed for this session and installing no wearable runtime patches",
    "if (legacyBeltSlotDetected)",
    "bool TryDetectLegacyBeltSlot(out bool detected)",
    'Type.GetType("BepInEx.Bootstrap.Chainloader, BepInEx", false)',
    '"PluginInfos"',
    "BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic",
    "detected = dictionary.Contains(\"com.trenchfoot.beltslot\") || dictionary.Contains(\"BeltSlot\");",
]
for token in required:
    if token not in text:
        violations.append(f"missing legacy conflict fail-closed token: {token!r}")

if "LegacyBeltSlotDetected()" in text:
    violations.append("legacy bool-only detector is forbidden because unknown state can be confused with no conflict")

# Unknown Chainloader/PluginInfos/dictionary and exceptions must all return failure,
# while only the successful read path returns true after assigning `detected`.
method_start = text.find("bool TryDetectLegacyBeltSlot(out bool detected)")
method_end = text.find("void OnDestroy()", method_start)
method = text[method_start:method_end] if method_start >= 0 and method_end > method_start else ""
if method.count("return false;") < 4:
    violations.append("legacy conflict detector must fail closed for Chainloader, PluginInfos, dictionary and exception failures")
if method.count("return true;") != 1:
    violations.append("legacy conflict detector must have exactly one successful return path after PluginInfos inspection")

if violations:
    raise SystemExit("B&A&HB legacy-conflict gate failed:\n" + "\n".join(violations))

print("B&A&HB legacy-conflict gate: OK (BepInEx PluginInfos must be readable; unknown conflict state fails closed; confirmed BeltSlot blocks duplicate patching)")
