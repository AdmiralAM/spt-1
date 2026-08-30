from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
source = (ROOT / "src" / "FastAccessSlotPatches.cs").read_text(encoding="utf-8-sig")
tests = (ROOT / "tests" / "Program.cs").read_text(encoding="utf-8-sig")
bridge_tests = (ROOT / "tests" / "ReloadCandidateBridgeRegression.cs").read_text(encoding="utf-8-sig")

violations = []

for token in (
    'FindInstanceMethod(controllerType, "IsAtReachablePlace", typeof(bool), itemType)',
    'GetAllParentItems',
    'FindReadableMember(itemType, "StringTemplateId", typeof(string))',
    'FastAccessReloadRuntime.GetAllParentItems = BuildParentEnumerator',
    'FastAccessReloadRuntime.ReadTemplateId = BuildStringReader',
    'ShouldPromoteReloadReachability',
    'AccessoryCapability.FastAccess',
    'dynamic.DefineParameter(1, ParameterAttributes.None, "__0")',
    'dynamic.DefineParameter(2, ParameterAttributes.Out, "__result")',
    'ReflectionTools.FindType("EFT.FirearmHandsInputTranslator")',
    'FindExactZeroArgVoidMethod(translatorType, "Reload")',
    'FindExactZeroArgVoidMethod(translatorType, "QuickReload")',
    'string.Equals(method.Name, "GetItemsInSlots", StringComparison.Ordinal)',
    'ReloadCandidateBridgeRuntime.EnterReloadScope',
    'ReloadCandidateBridgeRuntime.ExitReloadScope',
    'ReferenceEquals(slots, OriginalFastAccessSlots)',
    'ReferenceEquals(slots, InstalledFastAccessSlots)',
    'RuntimeIdentity.DedicatedMagazineBeltItemId',
    'GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })',
    'if (item == null || !MagazineType.IsInstanceOfType(item) || !HasExactMagazineBeltAncestor(item)) continue;',
    'if (ReferenceEquals(merged[i], item)) { duplicate = true; break; }',
):
    if token not in source:
        violations.append(f"FastAccessSlotPatches.cs: reload contract token missing: {token}")

start = source.find("internal static void PromoteReachability(")
end = source.find("internal static void Reset()", start)
if start < 0 or end < 0:
    violations.append("FastAccessSlotPatches.cs: reload runtime region not found")
else:
    runtime = source[start:end]
    for token in (
        "ReflectionTools.ReadMember",
        "AppDomain.CurrentDomain.GetAssemblies",
        "ReflectionTools.GetTypes",
        "GetMethods(",
        "GetMethod(",
        "GetProperty(",
        "GetField(",
        "FindObjectsOfType",
        "GetComponentsInChildren",
    ):
        if token in runtime:
            violations.append(f"FastAccessSlotPatches.cs: reload hot path performs discovery/scan: {token}")

bridge_start = source.find("internal static object AppendCandidates(")
bridge_end = source.find("internal static void Reset()", bridge_start)
if bridge_start < 0 or bridge_end < 0:
    violations.append("FastAccessSlotPatches.cs: scoped reload candidate bridge runtime region not found")
else:
    runtime = source[bridge_start:bridge_end]
    for token in (
        "AppDomain.CurrentDomain.GetAssemblies",
        "ReflectionTools.GetTypes",
        "GetMethods(",
        "GetProperty(",
        "GetField(",
        "FindObjectsOfType",
        "GetComponentsInChildren",
        "new StackTrace",
    ):
        if token in runtime:
            violations.append(f"FastAccessSlotPatches.cs: scoped candidate hot path performs discovery/scan: {token}")

for token in (
    '!FastAccessSlotPolicy.ShouldPromoteReloadReachability(true, true, true)',
    'FastAccessSlotPolicy.ShouldPromoteReloadReachability(false, true, true)',
    'extendedReloadSlots.Take(vanillaReloadSlots.Length).SequenceEqual(vanillaReloadSlots)',
    'extendedReloadSlots.Skip(vanillaReloadSlots.Length).SequenceEqual(new[] { BeltSlotPlan.ArmBand, RuntimeIdentity.DedicatedBeltWireSlotId })',
    'RuntimeIdentity.CandidateItemId, AccessoryCapability.FastAccess',
    'RuntimeIdentity.DedicatedMagazineBeltItemId, AccessoryCapability.FastAccess',
    '!WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.WristWalletItemId, AccessoryCapability.FastAccess)',
    '!WearableItemDescriptorRegistry.HasCapability(RuntimeIdentity.EmergencyHeadBandItemId, AccessoryCapability.FastAccess)',
):
    if token not in tests:
        violations.append(f"Program.cs: reload fallback regression missing: {token}")

for token in (
    'ShouldBridgeReloadCandidates(true, false, true)',
    'ShouldBridgeReloadCandidates(false, false, true)',
    'ShouldBridgeReloadCandidates(true, true, true)',
    'ShouldBridgeReloadCandidates(true, false, false)',
):
    if token not in bridge_tests:
        violations.append(f"ReloadCandidateBridgeRegression.cs: scoped bridge regression missing: {token}")

for token in (
    "FindObjectsOfType",
    "Resources.FindObjectsOfTypeAll",
    "GetComponentsInChildren",
    "void Update(",
):
    if token in source:
        violations.append(f"FastAccessSlotPatches.cs: forbidden reload implementation mechanism: {token}")

if violations:
    raise SystemExit("Reload-access guard failed:\n" + "\n".join(violations))

print("B&A&HB reload-access guard: OK (vanilla reachability/order preserved; Reload/QuickReload scoped bridge appends exact Magazine Belt magazine descendants only; startup-bound discovery; fail-closed/no polling)")
