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

# Alt-pickup runs on an interaction path. Discovery is allowed during TryInstall,
# but Resolve itself must use only startup-bound delegates and cached values.
pickup = ROOT / "PickupSlotPatches.cs"
if pickup.exists() and pickup.name not in removed:
    text = pickup.read_text(encoding="utf-8-sig")
    start = text.find("internal static object Resolve(")
    end = text.find("internal static void Reset()", start)
    if start < 0 or end < 0:
        violations.append("PickupSlotPatches.cs: Alt-pickup Resolve hot path could not be located")
    else:
        resolve = text[start:end]
        for token in ("GetMethods(", "GetMethod(", "GetProperty(", "GetField(", "MethodInfo", "PropertyInfo", "FieldInfo", "Enum.Parse", "Activator."):
            if token in resolve:
                violations.append(f"PickupSlotPatches.cs: Alt-pickup Resolve performs runtime reflection/discovery ({token})")

if violations:
    raise SystemExit("Hot-path guard failed:\n" + "\n".join(violations))

print("B&A&HB #2 hot-path guard: OK (no idle polling/global scans; Alt-pickup Resolve is startup-bound)")
