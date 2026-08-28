from pathlib import Path
import xml.etree.ElementTree as ET

MODULE_ROOT = Path(__file__).resolve().parents[1]
ROOT = MODULE_ROOT / "src"
SERVER_PATCH_ROOT = MODULE_ROOT / "server" / "Patches"
FORBIDDEN = {
    "ItemView.Update": "global item-view hot-path patch",
    "FindObjectsOfType": "scene-wide object scan",
    "Resources.FindObjectsOfTypeAll": "global Unity resource scan",
    "GetComponentsInChildren": "hierarchy-wide polling/scan",
    "void Update(": "Unity per-frame Update loop",
}

project = ROOT / "SPT-Belt-Armband-Inventory.csproj"
removed = set()
if project.exists():
    tree = ET.parse(project)
    for item in tree.findall(".//Compile"):
        remove = item.attrib.get("Remove")
        if remove:
            removed.add(remove.replace("\\", "/"))

violations = []
for source in sorted(ROOT.glob("*.cs")):
    if source.name in removed:
        continue
    text = source.read_text(encoding="utf-8-sig")
    for token, reason in FORBIDDEN.items():
        if token in text:
            violations.append(f"{source.name}: {reason} ({token})")

# Server lifecycle patches previously caused profile-load failures through name-only
# reflection. Production patches must enumerate candidates and prove an exact signature.
for source in sorted(SERVER_PATCH_ROOT.glob("*.cs")):
    text = source.read_text(encoding="utf-8-sig")
    if ".GetMethod(" in text:
        violations.append(f"server/Patches/{source.name}: ambiguous name-only GetMethod is forbidden")

def guard_region(path_name, start_token, end_token, label):
    path = ROOT / path_name
    if not path.exists() or path.name in removed:
        return
    text = path.read_text(encoding="utf-8-sig")
    start = text.find(start_token)
    end = text.find(end_token, start)
    if start < 0 or end < 0:
        violations.append(f"{path_name}: {label} hot path could not be located")
        return
    region = text[start:end]
    for token in (
        "GetMethods(", "GetMethod(", "GetProperty(", "GetField(",
        "MethodInfo", "PropertyInfo", "FieldInfo", "Enum.Parse", "Activator.",
        "MethodBase.Invoke", "MethodInfo.Invoke", "PropertyInfo.GetValue", "FieldInfo.GetValue"
    ):
        if token in region:
            violations.append(f"{path_name}: {label} performs runtime reflection/discovery ({token})")

# Interaction/lifecycle hot paths must use startup-bound delegates and cached values.
# Delegate calls (including ?.Invoke on Action loggers) are allowed; reflection Invoke is not.
guard_region(
    "PickupSlotPatches.cs",
    "internal static object Resolve(",
    "internal static void Reset()",
    "Alt-pickup Resolve")
guard_region(
    "PaymentSlotPatches.cs",
    "internal static void Normalize(",
    "static int IndexOfReference",
    "payment Normalize")
guard_region(
    "ScavBeltPatches.cs",
    "internal static void RestoreContainerBeltSlot(",
    "internal static void Reset()",
    "Scav lifecycle restore")
guard_region(
    "LootPriorityRuntime.cs",
    "static void Postfix(",
    "static List<object> ReadCapabilityContainers",
    "loot priority Postfix")
guard_region(
    "UnloadPriorityRuntime.cs",
    "static void Postfix(",
    "static List<object> ReadCapabilityGrids",
    "unload priority Postfix")

if violations:
    raise SystemExit("Hot-path guard failed:\n" + "\n".join(violations))

print("B&A&HB #2 hot-path guard: OK (no idle polling/global scans; interaction/lifecycle hot paths startup-bound; server patches exact-signature only)")
