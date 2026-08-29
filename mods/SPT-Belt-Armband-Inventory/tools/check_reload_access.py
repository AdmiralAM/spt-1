from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
source = (ROOT / "src" / "FastAccessSlotPatches.cs").read_text(encoding="utf-8-sig")
tests = (ROOT / "tests" / "Program.cs").read_text(encoding="utf-8-sig")

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
    "FindObjectsOfType",
    "Resources.FindObjectsOfTypeAll",
    "GetComponentsInChildren",
    "void Update(",
):
    if token in source:
        violations.append(f"FastAccessSlotPatches.cs: forbidden reload implementation mechanism: {token}")

if violations:
    raise SystemExit("Reload-access guard failed:\n" + "\n".join(violations))

print("B&A&HB reload-access guard: OK (vanilla reachability/order preserved; Magazine Armband/Belt appended fallback only; exact ancestor roots; startup-bound delegates; no runtime scans/reflection)")
