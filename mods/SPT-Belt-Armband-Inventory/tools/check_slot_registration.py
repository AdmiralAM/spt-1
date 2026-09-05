from pathlib import Path
import runpy

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "server" / "DedicatedEquipmentSlotRegistration.cs"
text = SOURCE.read_text(encoding="utf-8-sig")
violations = []

required = [
    "[Injectable(TypePriority = OnLoadOrder.Preload + 4)]",
    "if (!templateTable.Items.ContainsKey(DedicatedMagazineBeltTpl)",
    "|| !templateTable.Items.ContainsKey(EmergencyHeadBandTpl))",
    "dedicated product templates were not both initialized",
    "Slot? beltAddition = PrepareDedicatedSlot(",
    "Slot? headBandAddition = PrepareDedicatedSlot(",
    "if (beltAddition != null) slots.Add(beltAddition);",
    "if (headBandAddition != null) slots.Add(headBandAddition);",
    "failed safely without partial slot mutation",
]
for token in required:
    if token not in text:
        violations.append(f"missing atomic slot-registration token: {token!r}")

if "UpsertDedicatedSlot(" in text:
    violations.append("mutating UpsertDedicatedSlot helper is forbidden; dedicated slots must prepare before commit")

product_check = text.find("if (!templateTable.Items.ContainsKey(DedicatedMagazineBeltTpl)")
belt_prepare = text.find("Slot? beltAddition = PrepareDedicatedSlot(")
head_prepare = text.find("Slot? headBandAddition = PrepareDedicatedSlot(")
first_add = text.find("slots.Add(")
if min(product_check, belt_prepare, head_prepare, first_add) < 0:
    pass
elif not (product_check < belt_prepare < first_add and product_check < head_prepare < first_add):
    violations.append("dedicated product existence and both slot contracts must be validated before the first slot-list mutation")

assignment = text.find("inventory.Properties!.Slots = slots;")
if first_add >= 0 and assignment >= 0 and assignment < first_add:
    violations.append("canonical slot-list assignment occurs before prepared additions are committed")

# DedicatedWearableItems is intentionally Preload+3; slots must be later.
items = (ROOT / "server" / "DedicatedWearableItems.cs").read_text(encoding="utf-8-sig")
if "[Injectable(TypePriority = OnLoadOrder.Preload + 3)]" not in items:
    violations.append("dedicated wearable item owner must remain Preload+3 before slot publication at Preload+4")

if violations:
    raise SystemExit("B&A&HB dedicated-slot atomicity gate failed:\n" + "\n".join(violations))

print("B&A&HB dedicated-slot atomicity gate: OK (dedicated items exist at Preload+3; slot15/slot16 publish at Preload+4 only after exact templates and both slot contracts validate; partial install path forbidden)")

# Keep server/client ownership, persistent identity, offer/template/host, transport and collision safety in one compatibility step.
runpy.run_path(str(ROOT / "tools" / "check_taxonomy_ownership.py"), run_name="__main__")
runpy.run_path(str(ROOT / "tools" / "check_identity_manifest.py"), run_name="__main__")
runpy.run_path(str(ROOT / "tools" / "check_offer_template_boundary.py"), run_name="__main__")
runpy.run_path(str(ROOT / "tools" / "check_offer_host_contract.py"), run_name="__main__")
runpy.run_path(str(ROOT / "tools" / "check_server_patch_registration.py"), run_name="__main__")
runpy.run_path(str(ROOT / "tools" / "check_legacy_conflict_gate.py"), run_name="__main__")
runpy.run_path(str(ROOT / "tools" / "check_protection_sync.py"), run_name="__main__")
