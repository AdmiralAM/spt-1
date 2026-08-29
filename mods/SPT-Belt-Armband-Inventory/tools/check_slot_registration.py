from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "server" / "DedicatedEquipmentSlotRegistration.cs"
text = SOURCE.read_text(encoding="utf-8-sig")
violations = []

required = [
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

belt_prepare = text.find("Slot? beltAddition = PrepareDedicatedSlot(")
head_prepare = text.find("Slot? headBandAddition = PrepareDedicatedSlot(")
first_add = text.find("slots.Add(")
if min(belt_prepare, head_prepare, first_add) < 0:
    pass
elif not (belt_prepare < first_add and head_prepare < first_add):
    violations.append("both dedicated slot contracts must be prepared/validated before the first slot-list mutation")

# The canonical inventory slot list must be assigned only after prepared additions
# have been committed; collision exceptions before that point leave the template unchanged.
assignment = text.find("inventory.Properties!.Slots = slots;")
if first_add >= 0 and assignment >= 0 and assignment < first_add:
    violations.append("canonical slot-list assignment occurs before prepared additions are committed")

if violations:
    raise SystemExit("B&A&HB dedicated-slot atomicity gate failed:\n" + "\n".join(violations))

print("B&A&HB dedicated-slot atomicity gate: OK (slot15/slot16 validated/prepared before any canonical slot mutation; partial install path forbidden)")
