from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1] / "src"
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
        ".Invoke("
    ):
        if token in region:
            violations.append(f"{path_name}: {label} performs runtime reflection/discovery ({token})")

# Interaction/runtime hot paths must use startup-bound delegates and cached values.
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

if violations:
    raise SystemExit("Hot-path guard failed:\n" + "\n".join(violations))

print("B&A&HB #2 hot-path guard: OK (no idle polling/global scans; Alt-pickup and payment hot paths are startup-bound)")
