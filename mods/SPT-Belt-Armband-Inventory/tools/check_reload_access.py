from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
source = (ROOT / "src" / "FastAccessSlotPatches.cs").read_text(encoding="utf-8-sig")
epoch = (ROOT / "src" / "ReloadScopeEpochGuard.cs").read_text(encoding="utf-8-sig")
tests = (ROOT / "tests" / "Program.cs").read_text(encoding="utf-8-sig")
bridge_tests = (ROOT / "tests" / "ReloadCandidateBridgeRegression.cs").read_text(encoding="utf-8-sig")
epoch_tests = (ROOT / "tests" / "ReloadScopeEpochRegression.cs").read_text(encoding="utf-8-sig")
diagnostic_tests = (ROOT / "tests" / "ReloadDiagnosticLoggingRegression.cs").read_text(encoding="utf-8-sig")

violations = []

for token in (
    'FindInstanceMethod(controllerType, "IsAtReachablePlace", typeof(bool), itemType)',
    'GetAllParentItems',
    'FindReadableMember(itemType, "StringTemplateId", typeof(string))',
    'internal static Type ItemType;',
    'FastAccessReloadRuntime.ItemType = itemType;',
    'FastAccessReloadRuntime.GetAllParentItems = BuildParentEnumerator',
    'FastAccessReloadRuntime.ReadTemplateId = BuildStringReader',
    'ShouldPromoteReloadReachability',
    'AccessoryCapability.FastAccess',
    'dynamic.DefineParameter(1, ParameterAttributes.None, "__0")',
    'dynamic.DefineParameter(2, ParameterAttributes.Out, "__result")',
    'ReflectionTools.FindType("EFT.FirearmHandsInputTranslator")',
    'FindExactZeroArgVoidMethod(translatorType, "Reload")',
    'FindExactZeroArgVoidMethod(translatorType, "QuickReload")',
    'Type itemType = FastAccessReloadRuntime.ItemType;',
    'if (!itemType.IsAssignableFrom(FastAccessReloadRuntime.MagazineType)) return false;',
    'string.Equals(method.Name, "GetItemsInSlots", StringComparison.Ordinal)',
    'if (!method.ReturnType.IsAssignableFrom(itemArrayType)) continue;',
    'ReloadCandidateBridgeRuntime.EnterReloadScope',
    'ReloadCandidateBridgeRuntime.ExitReloadScope',
    'ReferenceEquals(slots, OriginalFastAccessSlots)',
    'ReferenceEquals(slots, InstalledFastAccessSlots)',
    'ReferenceEquals(slots, OriginalBindAvailableSlots)',
    'ReferenceEquals(slots, InstalledBindAvailableSlots)',
    'ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBindAvailableSlots;',
    'ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBindAvailableSlots;',
    'RuntimeIdentity.DedicatedMagazineBeltItemId',
    'GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })',
    'if (item == null || !MagazineType.IsInstanceOfType(item) || !HasExactMagazineBeltAncestor(item)) continue;',
    'if (ContainsReference(vanillaItems, item) || (merged != null && ContainsReference(merged, item))) continue;',
    'List<object> merged = null;',
    'merged = new List<object>(vanillaItems.Length + 1);',
    'ShouldReuseVanillaReloadCandidates(merged != null)',
    'return vanillaResult;',
    'internal static class ReloadDiagnosticLog',
    'ReloadDiagnosticLog.TryWarning(LogWarning,',
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

for token in (
    '[ThreadStatic] static int threadGeneration;',
    '[ThreadStatic] static int threadDepth;',
    'Volatile.Read(ref generation)',
    'Interlocked.Increment(ref generation)',
    'runtime.GetMethod("EnterReloadScope"',
    'runtime.GetMethod("ExitReloadScope"',
    'runtime.GetMethod("AppendCandidates"',
    'runtime.GetMethod("Reset"',
    'object owner = null;',
    'PatchNamed(owner, patch, harmonyMethodType, reset, "postfix"',
    'TryRollbackOwner(owner);',
    'GetMethod("UnpatchSelf"',
    'harmonyOwner = null;',
    'installed = false;',
    'if (IsCurrentScope()) return true;',
    '__result = __2;',
    'return false;',
):
    if token not in epoch:
        violations.append(f"ReloadScopeEpochGuard.cs: lifecycle epoch contract token missing: {token}")

if 'ReloadScopeEpochRegression.Run();' not in tests:
    violations.append("Program.cs: reload epoch regression must run after module initialization")
if '[ModuleInitializer]' in epoch_tests:
    violations.append("ReloadScopeEpochRegression.cs: cross-thread epoch regression must not execute from module initialization")
for token in (
    'ReloadScopeEpochGuard.InvalidateForRegression();',
    'scope from superseded installation remained current on its owning thread',
    'new-generation reload scope did not become current',
    'same-thread nested stale scope survived generation invalidation',
):
    if token not in epoch_tests:
        violations.append(f"ReloadScopeEpochRegression.cs: epoch regression missing: {token}")

if source.count('ReloadDiagnosticLog.TryWarning(LogWarning,') < 2:
    violations.append("FastAccessSlotPatches.cs: both reload reachability and candidate runtime diagnostics must isolate throwing warning sinks")

if 'FastAccessReloadRuntime.MagazineType.BaseType' in source:
    violations.append("FastAccessSlotPatches.cs: candidate discovery must use exact resolved EFT Item, not infer Item from Magazine.BaseType")

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
    'OriginalBindAvailableSlots = originalBindAvailableSlots',
    'InstalledBindAvailableSlots = installedBindAvailableSlots',
    'originalBindAvailableSlots',
    'installedBindAvailableSlots',
):
    if token not in bridge_tests:
        violations.append(f"ReloadCandidateBridgeRegression.cs: scoped bridge regression missing: {token}")

for token in (
    'ThrowingReachabilityLoggerCannotEscape()',
    'ThrowingCandidateLoggerCannotEscape()',
    'FastAccessReloadRuntime.LogWarning = _ => throw new InvalidOperationException("synthetic logger failure")',
    'ReloadCandidateBridgeRuntime.LogWarning = _ => throw new InvalidOperationException("synthetic logger failure")',
    'ReferenceEquals(first, vanilla)',
    'ReferenceEquals(second, vanilla)',
    'candidate failure plus logger failure cannot leak reentrant state',
):
    if token not in diagnostic_tests:
        violations.append(f"ReloadDiagnosticLoggingRegression.cs: throwing diagnostic sink regression missing: {token}")

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

print("B&A&HB reload-access guard: OK (vanilla-first exact Belt bridge; exact FastAccess/BindAvailable reference identity; exact EFT Item contract; no-op Belt path preserves vanilla result identity without merge allocation; throwing diagnostics isolated; reachability/candidate owners isolated; stale ThreadStatic scopes generation-invalidated across reset/reinstall; epoch Harmony install is owner-atomic with rollback on partial failure; startup-bound discovery; fail-closed/no polling)")