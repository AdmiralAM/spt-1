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
    'if (ContainsReference(vanillaItems, item) || (merged != null && ContainsReference(merged, item))) continue;',
    'List<object> merged = null;',
    'merged = new List<object>(vanillaItems.Length + 1);',
    'ShouldReuseVanillaReloadCandidates(merged != null)',
    'return vanillaResult;',
    'ReachabilityHarmonyId = "com.admiralam.spt.belt-armband-inventory.fast-access.reachability"',
    'CandidateBridgeHarmonyId = "com.admiralam.spt.belt-armband-inventory.fast-access.reload-candidate"',
    'PatchNamed(reachabilityHarmony, patchMethod, harmonyMethodType, reachable, "postfix", postfix)',
    'PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, reload, "prefix", prefix)',
    'PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, quickReload, "prefix", prefix)',
    'PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, getItemsInSlots, "postfix", candidatesPostfix)',
    'UnpatchCandidateBridge();',
    'UnpatchReachability();',
):
    if token not in source:
        violations.append(f"FastAccessSlotPatches.cs: reload contract token missing: {token}")

if source.count('Activator.CreateInstance(harmonyType, new object[] { ReachabilityHarmonyId })') != 1:
    violations.append("FastAccessSlotPatches.cs: reachability Harmony owner must be created exactly once")
if source.count('Activator.CreateInstance(harmonyType, new object[] { CandidateBridgeHarmonyId })') != 1:
    violations.append("FastAccessSlotPatches.cs: candidate bridge Harmony owner must be created exactly once")

candidate_start = source.find("bool TryInstallReloadCandidateBridge(")
candidate_end = source.find("static MethodInfo FindExactZeroArgVoidMethod", candidate_start)
if candidate_start < 0 or candidate_end < 0:
    violations.append("FastAccessSlotPatches.cs: candidate bridge installer region not found")
else:
    installer = source[candidate_start:candidate_end]
    if "UnpatchReachability();" in installer:
        violations.append("FastAccessSlotPatches.cs: candidate bridge failure must not unpatch valid reachability owner")
    if "UnpatchCandidateBridge();" not in installer:
        violations.append("FastAccessSlotPatches.cs: partial candidate bridge install lacks owner-scoped rollback")

unpatch_start = source.find("void UnpatchCandidateBridge()")
unpatch_end = source.find("void UnpatchReachability()", unpatch_start)
if unpatch_start < 0 or unpatch_end < 0:
    violations.append("FastAccessSlotPatches.cs: candidate owner rollback region not found")
else:
    rollback = source[unpatch_start:unpatch_end]
    if "candidateBridgeUnpatchSelf.Invoke(candidateBridgeHarmony, null)" not in rollback:
        violations.append("FastAccessSlotPatches.cs: candidate rollback does not unpatch its own Harmony owner")
    if "FastAccessReloadRuntime.Reset()" in rollback:
        violations.append("FastAccessSlotPatches.cs: candidate rollback must preserve reachability runtime state")

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

    lazy_merge = runtime.find("List<object> merged = null;")
    belt_loop = runtime.find("foreach (object item in beltItems)")
    allocation = runtime.find("merged = new List<object>(vanillaItems.Length + 1);")
    reuse = runtime.find("ShouldReuseVanillaReloadCandidates(merged != null)")
    if min(lazy_merge, belt_loop, allocation, reuse) < 0 or not (lazy_merge < belt_loop < allocation < reuse):
        violations.append("FastAccessSlotPatches.cs: no-op Belt path must stay allocation-free and reuse vanilla result until first exact fallback")

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
    'ShouldReuseVanillaReloadCandidates(false)',
    'ShouldReuseVanillaReloadCandidates(true)',
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

print("B&A&HB reload-access guard: OK (vanilla-first exact Belt bridge; no-op Belt path preserves vanilla result identity without merge allocation; reachability/candidate Harmony owners isolated; partial bridge installs roll back atomically; startup-bound discovery; fail-closed/no polling)")
