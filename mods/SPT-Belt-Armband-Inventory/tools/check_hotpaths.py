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
# add/replace a map entry. A compact ArmBand-template slot16 is projected only from
# the EquipmentTab.Show prefix, before the native enumerator exists.
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
    for token in (
        "BeforeEquipmentTabShow",
        "EquipmentTabPrefixFactory",
        "INSURANCE-SAFE SLOT PROOF",
        "BindHeadBandFromHeadwear",
        "PositionAboveHeadwear",
    ):
        if token not in presentation_text:
            violations.append(
                "DedicatedSlotPresentationPatches.cs: synchronous insurance-safe first-render contract is missing "
                f"({token})")

    bind_start = presentation_text.find("static void BindHeadBandFromHeadwear(")
    bind_end = presentation_text.find("static Component GetOrCreateHeadBandView(", bind_start)
    if bind_start < 0 or bind_end < 0:
        violations.append("DedicatedSlotPresentationPatches.cs: synchronous HeadBand bind region could not be located")
    else:
        bind_region = presentation_text[bind_start:bind_end]
        for required in ("ShowSlot(headBandView, headBandSlot", "PositionAboveHeadwear(headBandView, headwearView)"):
            if required not in bind_region:
                violations.append(
                    "DedicatedSlotPresentationPatches.cs: first-render bind is no longer synchronous "
                    f"({required})")
        for token in (
            "StartCoroutine",
            "RequestFlush",
            "EnsureDeferredRuntimePump",
            "Canvas.ForceUpdateCanvases",
            "preferredHeight",
            "equipmentTab.transform.position =",
            "gearRect.position =",
            "slotViews.Add(",
            "UnityEngine.Object.Instantiate(",
        ):
            if token in bind_region:
                violations.append(
                    "DedicatedSlotPresentationPatches.cs: first-render bind introduced deferred/host-panel/late mutation "
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

    position_start = presentation_text.find("static void PositionAboveHeadwear(")
    position_end = presentation_text.find("static void RelabelNumericCaptionTree(", position_start)
    if position_start < 0 or position_end < 0:
        violations.append("DedicatedSlotPresentationPatches.cs: accepted HeadBand pre-reflow placement region could not be located")
    else:
        position_region = presentation_text[position_start:position_end]
        for token in (
            "Canvas.ForceUpdateCanvases",
            "preferredHeight",
            "LayoutElement",
            "equipmentTab.transform.position =",
            "gearRect.position =",
            "StartCoroutine",
            "RequestFlush",
        ):
            if token in position_region:
                violations.append(
                    "DedicatedSlotPresentationPatches.cs: accepted HeadBand placement mutates/refreshes the host panel "
                    f"({token})")

# Freeze the exact physical layout accepted at 9cf023c: the mapped HeadBand occupies
# original Headwear local coordinates and native slot RectTransforms are translated by
# exactly one compact 44+4 row. This is deliberately NOT a host Gear Panel layout
# mutation: no LayoutElement/preferredHeight, Canvas rebuild, panel transform move,
# coroutine, retry or polling is allowed.
reflow_path = ROOT / "HeadBandRenderSettle.cs"
if not reflow_path.exists() or reflow_path.name in removed:
    violations.append("HeadBandRenderSettle.cs: accepted stabilization geometry owner is missing")
else:
    reflow_text = reflow_path.read_text(encoding="utf-8-sig")
    for token in (
        "const float HeadBandCompactHeight = 44f;",
        "const float HeadBandGap = 4f;",
        "const float StructuralOffset = HeadBandCompactHeight + HeadBandGap;",
        "rect.anchoredPosition = new Vector2(original.x, original.y - StructuralOffset);",
        "headBandRect.anchoredPosition = originalHeadwear;",
        "HEADBAND FIRST-RENDER PROOF",
        "panelLayoutMutation=False",
        "synchronous=True",
    ):
        if token not in reflow_text:
            violations.append(
                "HeadBandRenderSettle.cs: accepted stabilization geometry contract changed "
                f"({token})")
    for token in (
        "preferredHeight",
        "LayoutElement",
        "Canvas.ForceUpdateCanvases",
        "StartCoroutine",
        "RequestFlush",
        "EnsureDeferredRuntimePump",
        "equipmentTab.transform.position =",
        "gearRect.position =",
        "slotViews.Add(",
        "UnityEngine.Object.Instantiate(",
    ):
        if token in reflow_text:
            violations.append(
                "HeadBandRenderSettle.cs: frozen geometry introduced host-panel/deferred/slot-map mutation "
                f"({token})")

# The accepted reflow is triggered only from dedicated-slot localization on the same
# proven Headwear SlotView.Show event. No second production caller may appear.
reflow_callers = []
for source in sorted(ROOT.glob("*.cs")):
    if source.name in removed or source.name == "HeadBandRenderSettle.cs":
        continue
    text = source.read_text(encoding="utf-8-sig")
    if "HeadBandRenderSettle.OnHeadwearShown" in text:
        reflow_callers.append(source.name)
if reflow_callers != ["DedicatedSlotLocalizationPatches.cs"]:
    violations.append(
        "HeadBandRenderSettle: accepted synchronous caller set changed: " + repr(reflow_callers))

localization_path = ROOT / "DedicatedSlotLocalizationPatches.cs"
if localization_path.exists() and localization_path.name not in removed:
    localization_text = localization_path.read_text(encoding="utf-8-sig")
    for token in ("HeadBandRenderSettle.OnHeadwearShown(slotView as Component);", "HeadBandRenderSettle.Reset();"):
        if token not in localization_text:
            violations.append(
                "DedicatedSlotLocalizationPatches.cs: accepted HeadBand first-render lifecycle hook changed "
                f"({token})")
    for token in ("StartCoroutine", "Canvas.ForceUpdateCanvases", "preferredHeight", "LayoutElement"):
        if token in localization_text:
            violations.append(
                "DedicatedSlotLocalizationPatches.cs: localization/settle path introduced manual/deferred host refresh "
                f"({token})")

first_open_path = ROOT / "FirstOpenHeadBandLayoutPatches.cs"
if first_open_path.exists() and first_open_path.name not in removed:
    first_open_text = first_open_path.read_text(encoding="utf-8-sig")
    for token in ("HasPending => false", "void Flush() { }", "positioner disabled"):
        if token not in first_open_text:
            violations.append(
                "FirstOpenHeadBandLayoutPatches.cs: legacy first-open refresh path is no longer a strict no-op "
                f"({token})")
    for token in (
        "Type.GetType(\"HarmonyLib.Harmony",
        "DynamicMethod(",
        "patchMethod.Invoke(",
        "RequestFlush?.Invoke",
        "StartCoroutine",
        "Canvas.ForceUpdateCanvases",
    ):
        if token in first_open_text:
            violations.append(
                "FirstOpenHeadBandLayoutPatches.cs: legacy first-open shim must not install executable refresh/placement hooks "
                f"({token})")

protection_router_path = MODULE_ROOT / "server" / "WearableProtectionRuntime.cs"
if protection_router_path.exists():
    protection_router_text = protection_router_path.read_text(encoding="utf-8-sig")
    if "RouteAction<WearableProtectionRequest>" not in protection_router_text:
        violations.append("server/WearableProtectionRuntime.cs: protection route must declare its typed request body")
    if "info?.ToString()" in protection_router_text or "JsonSerializer.Deserialize<WearableProtectionRequest>" in protection_router_text:
        violations.append("server/WearableProtectionRuntime.cs: protection route must not parse EmptyRequestData.ToString()")

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

print("B&A&HB #2 hot-path guard: OK (accepted HeadBand geometry frozen at 44+4 structural row; host Gear Panel layout untouched; no manual/deferred first-open refresh; slot16 projection pre-enumeration only; interaction/lifecycle hot paths startup-bound; server patches bounded-unique)")
