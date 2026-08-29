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

# Physical RC1 proved that cloning Headwear from EquipmentTab.Awake creates broken
# geometry. Insurance evidence later proved the opposite late boundary unsafe too:
# SlotView.Show runs inside EquipmentTab.Show's slot-map enumeration and must never
# add/replace a map entry. A compact ArmBand-template slot16 is now projected only
# from the EquipmentTab.Show prefix, before the native enumerator exists.
equipment_slot_path = ROOT / "DedicatedEquipmentSlotPatches.cs"
if equipment_slot_path.exists() and equipment_slot_path.name not in removed:
    equipment_slot_text = equipment_slot_path.read_text(encoding="utf-8-sig")
    for token in ("EquipmentTabAwakePostfix", "InstallHeadBandView", "Instantiate(headwear"):
        if token in equipment_slot_text:
            violations.append(
                "DedicatedEquipmentSlotPatches.cs: provisional HeadBand Awake clone is forbidden after physical first-entry layout regression "
                f"({token})")

presentation_path = ROOT / "DedicatedSlotPresentationPatches.cs"
if presentation_path.exists() and presentation_path.name not in removed:
    presentation_text = presentation_path.read_text(encoding="utf-8-sig")
    for token in ("BeforeEquipmentTabShow", "EquipmentTabPrefixFactory", "INSURANCE-SAFE SLOT PROOF"):
        if token not in presentation_text:
            violations.append(
                "DedicatedSlotPresentationPatches.cs: insurance-safe pre-enumeration projection contract is missing "
                f"({token})")
    late_start = presentation_text.find("static Component GetOrCreateHeadBandView(")
    late_end = presentation_text.find("static void PositionAboveHeadwear(", late_start)
    if late_start < 0 or late_end < 0:
        violations.append("DedicatedSlotPresentationPatches.cs: late SlotView.Show binding region could not be located")
    else:
        late_region = presentation_text[late_start:late_end]
        for token in ("slotViews.Add(", "UnityEngine.Object.Instantiate("):
            if token in late_region:
                violations.append(
                    "DedicatedSlotPresentationPatches.cs: SlotView.Show path mutates the EquipmentTab map during native enumeration "
                    f"({token})")

protection_router_path = MODULE_ROOT / "server" / "WearableProtectionRuntime.cs"
if protection_router_path.exists():
    protection_router_text = protection_router_path.read_text(encoding="utf-8-sig")
    if "RouteAction<WearableProtectionRequest>" not in protection_router_text:
        violations.append("server/WearableProtectionRuntime.cs: protection route must declare its typed request body")
    if "info?.ToString()" in protection_router_text or "JsonSerializer.Deserialize<WearableProtectionRequest>" in protection_router_text:
        violations.append("server/WearableProtectionRuntime.cs: protection route must not parse EmptyRequestData.ToString()")

first_render_path = ROOT / "HeadBandRenderSettle.cs"
if first_render_path.exists() and first_render_path.name not in removed:
    first_render_text = first_render_path.read_text(encoding="utf-8-sig")
    for token in ("HEADBAND FIRST-RENDER PROOF", "panelLayoutMutation=False", "synchronous=True"):
        if token not in first_render_text:
            violations.append(f"HeadBandRenderSettle.cs: first-render invariant is missing ({token})")
    for token in ("Canvas.ForceUpdateCanvases", "preferredHeight.SetValue", "GetProperty(\"preferredHeight\"", "equipmentTab.transform.position =", "gearRect.position"):
        if token in first_render_text:
            violations.append(
                "HeadBandRenderSettle.cs: first-render path must not mutate/force the host panel layout "
                f"({token})")

# Server lifecycle patches previously caused profile-load failures through name-only
# reflection. Production patches must enumerate candidates and prove a unique bounded
# runtime target rather than using Type.GetMethod(name).
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

print("B&A&HB #2 hot-path guard: OK (no idle polling/global scans; slot16 pre-enumeration projection is insurance-safe; interaction/lifecycle hot paths startup-bound; server patches bounded-unique)")
