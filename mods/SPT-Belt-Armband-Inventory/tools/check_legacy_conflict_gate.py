from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PLUGIN = ROOT / "src" / "Plugin.cs"
text = PLUGIN.read_text(encoding="utf-8-sig")
violations = []

required = [
    'using BepInEx.Bootstrap;',
    '[BepInDependency("com.trenchfoot.beltslot", BepInDependency.DependencyFlags.SoftDependency)]',
    '[BepInDependency("BeltSlot", BepInDependency.DependencyFlags.SoftDependency)]',
    "if (!TryDetectLegacyBeltSlot(out bool legacyBeltSlotDetected))",
    "failing closed for this session and installing no wearable runtime patches",
    "if (legacyBeltSlotDetected)",
    "bool TryDetectLegacyBeltSlot(out bool detected)",
    "var pluginInfos = Chainloader.PluginInfos;",
    'pluginInfos.ContainsKey("com.trenchfoot.beltslot")',
    'pluginInfos.ContainsKey("BeltSlot")',
    'string.Equals(metadata.GUID, "com.trenchfoot.beltslot", StringComparison.Ordinal)',
    'string.Equals(metadata.GUID, "BeltSlot", StringComparison.Ordinal)',
    'string.Equals(metadata.Name, "BeltSlot", StringComparison.OrdinalIgnoreCase)',
]
for token in required:
    if token not in text:
        violations.append(f"missing legacy conflict/load-order token: {token!r}")

for forbidden in [
    "LegacyBeltSlotDetected()",
    'Type.GetType("BepInEx.Bootstrap.Chainloader, BepInEx"',
    'GetProperty(\n                    "PluginInfos"',
    "BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic",
]:
    if forbidden in text:
        violations.append(f"legacy BeltSlot discovery must use the public BepInEx API rather than the obsolete reflection path: {forbidden!r}")

method_start = text.find("bool TryDetectLegacyBeltSlot(out bool detected)")
method_end = text.find("void OnDestroy()", method_start)
method = text[method_start:method_end] if method_start >= 0 and method_end > method_start else ""
if "if (pluginInfos == null)" not in method or "return false;" not in method:
    violations.append("unreadable PluginInfos must fail closed")
if "catch (Exception exception)" not in method or method.rfind("return false;") < method.rfind("catch (Exception exception)"):
    violations.append("legacy discovery exception path must fail closed")
if method.count("detected = true;") < 2:
    violations.append("legacy discovery must detect both GUID-key and metadata fallback paths")

if violations:
    raise SystemExit("B&A&HB legacy-conflict gate failed:\n" + "\n".join(violations))

print("B&A&HB legacy-conflict gate: OK (soft dependencies order historical BeltSlot variants first; public Chainloader.PluginInfos is authoritative; unknown state fails closed)")
