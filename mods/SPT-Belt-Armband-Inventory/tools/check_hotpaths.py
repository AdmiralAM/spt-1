from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "src"
FORBIDDEN = {
    "void Update(": "Unity per-frame Update loop",
    "ItemView.Update": "global item-view hot-path patch",
    "ReplaceInventory": "inventory-controller replacement",
    "FindObjectsOfType": "scene-wide object scan",
    "GetComponentsInChildren": "hierarchy-wide polling/scan",
}

violations = []
for source in sorted(ROOT.glob("*.cs")):
    text = source.read_text(encoding="utf-8-sig")
    for token, reason in FORBIDDEN.items():
        if token in text:
            violations.append(f"{source.name}: {reason} ({token})")

if violations:
    raise SystemExit("Hot-path guard failed:\n" + "\n".join(violations))

print("SPT Belt/Armband Inventory hot-path guard: OK")
