from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1] / "src"
FORBIDDEN = {
    "ItemView.Update": "global item-view hot-path patch",
    "FindObjectsOfType": "scene-wide object scan",
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

if violations:
    raise SystemExit("Hot-path guard failed:\n" + "\n".join(violations))

print("SPT Belt/Armband Inventory hot-path guard: OK (no production Unity Update loop)")
