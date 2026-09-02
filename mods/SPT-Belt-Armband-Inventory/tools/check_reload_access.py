from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
source = (ROOT / "src" / "FastAccessSlotPatches.cs").read_text(encoding="utf-8-sig")
epoch = (ROOT / "src" / "ReloadScopeEpochGuard.cs").read_text(encoding="utf-8-sig")
tests = (ROOT / "tests" / "Program.cs").read_text(encoding="utf-8-sig")
bridge_tests = (ROOT / "tests" / "ReloadCandidateBridgeRegression.cs").read_text(encoding="utf-8-sig")
return_tests = (ROOT / "tests" / "ReloadCandidateReturnContractRegression.cs").read_text(encoding="utf-8-sig")
slot_parameter_tests = (ROOT / "tests" / "ReloadSlotParameterContractRegression.cs").read_text(encoding="utf-8-sig")
discovery_tests = (ROOT / "tests" / "ReloadDiscoveryExactReturnContractRegression.cs").read_text(encoding="utf-8-sig")
epoch_tests = (ROOT / "tests" / "ReloadScopeEpochRegression.cs").read_text(encoding="utf-8-sig")
epoch_install_tests = (ROOT / "tests" / "ReloadScopeEpochInstallRollbackRegression.cs").read_text(encoding="utf-8-sig")
diagnostic_tests = (ROOT / "tests" / "ReloadDiagnosticLoggingRegression.cs").read_text(encoding="utf-8-sig")
lazy_pin_tests = (ROOT / "tests" / "ReloadLazyEnumerationPinRegression.cs").read_text(encoding="utf-8-sig")

violations = []

def require(text, token, where):
    if token not in text:
        violations.append(f"{where}: missing {token}")

# Reachability remains exact and independently owned.
for token in (
    'FindInstanceMethod(controllerType, "IsAtReachablePlace", typeof(bool), itemType)',
    'FindReadableMember(itemType, "StringTemplateId", typeof(string))',
    'AccessoryCapability.FastAccess',
    'ReachabilityHarmonyId = "com.admiralam.spt.belt-armband-inventory.fast-access.reachability"',
    'CandidateBridgeHarmonyId = "com.admiralam.spt.belt-armband-inventory.fast-access.reload-candidate"',
    'PatchNamed(reachabilityHarmony, patchMethod, harmonyMethodType, reachable, "postfix", postfix)',
    'PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, reload, "prefix", prefix)',
    'PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, quickReload, "prefix", prefix)',
    'PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, getItemsInSlots, "postfix", candidatesPostfix)',
    'ReferenceEquals(slots, OriginalFastAccessSlots)',
    'ReferenceEquals(slots, InstalledFastAccessSlots)',
    'ReferenceEquals(slots, OriginalBindAvailableSlots)',
    'ReferenceEquals(slots, InstalledBindAvailableSlots)',
    'RuntimeIdentity.DedicatedMagazineBeltItemId',
    'GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })',
    'ShouldReuseVanillaReloadCandidates(merged != null)',
    'return vanillaResult;',
):
    require(source, token, "FastAccessSlotPatches.cs")

# Pinned SPT 4.x decomp contract: exact generic interfaces, not the concrete
# Item[] implementation detail that appears only inside Inventory.GetItemsInSlots.
for token in (
    'Type itemEnumerableType = typeof(IEnumerable<>).MakeGenericType(itemType);',
    'Type slotEnumerableType = typeof(IEnumerable<>).MakeGenericType(slotEnumType);',
    'getItemsInSlots.ReturnType != itemEnumerableType',
    'getItemsParameters[0].ParameterType != slotEnumerableType',
    'method.ReturnType != itemEnumerableType',
    'parameters[0].ParameterType != slotEnumerableType',
    'Type exactReturn = typeof(IEnumerable<>).MakeGenericType(itemType);',
    'declaredReturn != exactReturn || getItems.ReturnType != exactReturn',
    'ReturnType.IsInstanceOfType(vanillaResult)',
    'vanillaResult is IEnumerable vanillaSequence',
    'beltResult is IEnumerable beltItems',
):
    require(source, token, "FastAccessSlotPatches.cs")

for forbidden in (
    'Type itemArrayType = itemType.MakeArrayType();',
    'method.ReturnType != itemArrayType',
    'getItemsInSlots.ReturnType != itemArrayType',
    'declaredReturn != exactArray || getItems.ReturnType != exactArray',
):
    if forbidden in source:
        violations.append(f"FastAccessSlotPatches.cs: stale Item[] contract survived: {forbidden}")

# Exact vanilla-first/fail-closed mechanics: four-array content pin, one query,
# pre-query + post-query + post-lazy-enumeration reproof, reference dedup, no
# global scans in the hot bridge.
for token in (
    'HasPinnedFastAccessArrayContentForRegression(slots)',
    'var vanillaItems = new List<object>();',
    'foreach (object item in vanillaSequence)',
    'foreach (object item in beltItems)',
    'merged = new List<object>(vanillaItems);',
    'ContainsReference(vanillaItems, item)',
    'Array result = Array.CreateInstance(ItemType, merged.Count);',
    'Re-prove the exact retained/installed slot-array snapshot after enumeration',
):
    require(source, token, "FastAccessSlotPatches.cs")

bridge_start = source.find("internal static object AppendCandidates(")
bridge_end = source.find("internal static void Reset()", bridge_start)
if bridge_start < 0 or bridge_end < 0:
    violations.append("FastAccessSlotPatches.cs: scoped candidate runtime region not found")
else:
    runtime = source[bridge_start:bridge_end]
    if runtime.count('GetItemsInSlots.Invoke(inventory, new[] { BeltSlotsArgument })') != 1:
        violations.append("FastAccessSlotPatches.cs: scoped bridge must execute exactly one pseudo-slot15 query")
    if runtime.count('HasPinnedFastAccessArrayContentForRegression(slots)') < 4:
        violations.append("FastAccessSlotPatches.cs: slot-array pin must be proved before contract work, immediately pre-query, post-query/pre-enumeration, and post-enumeration/pre-publication")
    for forbidden in (
        "AppDomain.CurrentDomain.GetAssemblies", "ReflectionTools.GetTypes", "GetMethods(",
        "GetProperty(", "GetField(", "FindObjectsOfType", "GetComponentsInChildren", "new StackTrace",
    ):
        if forbidden in runtime:
            violations.append(f"FastAccessSlotPatches.cs: scoped hot path performs discovery/scan: {forbidden}")

# Epoch guard must enforce the same declared interface boundary, including an
# exact generic slot-parameter contract derived from the one-value belt argument.
for token in (
    '[ThreadStatic] static int threadGeneration;',
    '[ThreadStatic] static int threadDepth;',
    'Volatile.Read(ref generation)',
    'Interlocked.Increment(ref generation)',
    'static bool HasExactRuntimeReturnContract()',
    'Type exactReturn = typeof(IEnumerable<>).MakeGenericType(itemType);',
    'if (declaredReturn != exactReturn || getItems.ReturnType != exactReturn) return false;',
    'Type slotElementType = GetEnumerableElementType(beltArgument.GetType());',
    'Type exactSlotEnumerable = typeof(IEnumerable<>).MakeGenericType(slotElementType);',
    'parameters[0].ParameterType != exactSlotEnumerable',
    '!exactSlotEnumerable.IsInstanceOfType(beltArgument)',
    'static Type GetEnumerableElementType(Type runtimeType)',
    'Convert.ToInt32(value) != RuntimeIdentity.DedicatedBeltEquipmentSlotValue',
    'if (count > 1) return false;',
    '__result = __2;',
    'CaptureCurrentSlotArrays()',
    'HasPinnedFastAccessArrayContent(__1)',
    'bool rolledBack = TryRollbackOwner(owner, unpatchSelf);',
    'terminalFailure = owner != null && !rolledBack;',
):
    require(epoch, token, "ReloadScopeEpochGuard.cs")
if 'Type exactArray = itemType.MakeArrayType();' in epoch:
    violations.append("ReloadScopeEpochGuard.cs: stale Item[] return authority survived")

# Deterministic regressions must encode the actual decompiled signature and
# fail closed on return/query/slot-parameter drift while retaining recovery.
for token in (
    'exact.ReturnType != typeof(IEnumerable<FakeItem>)',
    'exact.GetParameters()[0].ParameterType != typeof(IEnumerable<FakeSlot>)',
    'public FakeItem[] GetItemsInSlots(IEnumerable<FakeSlot> slots)',
    'if (broadOnly != null)',
):
    require(discovery_tests, token, "ReloadDiscoveryExactReturnContractRegression.cs")
for token in (
    'exact IEnumerable<Item> GetItemsInSlots + one pseudo-slot15 query',
    'declared Item[] drift must be rejected',
    'method-declared Item[] drift must fail closed',
    'multi-slot fallback argument must fail closed before inventory enumeration',
    'wrong pseudo-slot fallback argument must fail closed before inventory enumeration',
    'exact one-slot query must recover after rejected query-state drift',
):
    require(return_tests, token, "ReloadCandidateReturnContractRegression.cs")
for token in (
    'exact IEnumerable<Item>(IEnumerable<slot>) contract must pass',
    'different slot element contract must fail closed',
    'exact slot parameter contract must recover after rejected drift',
    'IEnumerable<long> slots',
):
    require(slot_parameter_tests, token, "ReloadSlotParameterContractRegression.cs")

# The exact lazy-interface risk is executable, not merely structural: mutation
# during MoveNext must fail closed after the one query, while a restored healthy
# pin must recover normal vanilla-prefix + exact-Belt append semantics.
for token in (
    'same-reference slot-array drift during lazy Belt enumeration must preserve the exact vanilla enumerable object',
    'restoring the exact captured content must restore the recognized retained-array pin',
    'healthy lazy Belt enumeration with an unchanged pinned array must publish a replacement sequence',
    'healthy lazy Belt enumeration must preserve vanilla prefix and append the exact Belt descendant',
    'duringEnumeration?.Invoke();',
):
    require(lazy_pin_tests, token, "ReloadLazyEnumerationPinRegression.cs")

if 'ReloadScopeEpochRegression.Run();' not in tests:
    violations.append("Program.cs: reload epoch regression must run after module initialization")
if '[ModuleInitializer]' in epoch_tests:
    violations.append("ReloadScopeEpochRegression.cs: cross-thread epoch regression must not execute from module initialization")
for token in (
    'ReloadScopeEpochGuard.InvalidateForRegression();',
    'scope from superseded installation remained current on its owning thread',
    'new-generation reload scope did not become current',
):
    require(epoch_tests, token, "ReloadScopeEpochRegression.cs")
for token in (
    'TryRollbackOwner', 'FindZeroArgInstanceMethod',
    'partial Harmony owner was not unpatched exactly once',
    'throwing rollback was not reported as terminally unsafe',
    'missing rollback API was incorrectly treated as safe',
):
    require(epoch_install_tests, token, "ReloadScopeEpochInstallRollbackRegression.cs")

for token in (
    'ShouldBridgeReloadCandidates(true, false, true)',
    'ShouldReuseVanillaReloadCandidates(false)',
    'OriginalBindAvailableSlots = originalBindAvailableSlots',
    'InstalledBindAvailableSlots = installedBindAvailableSlots',
):
    require(bridge_tests, token, "ReloadCandidateBridgeRegression.cs")
for token in (
    'ThrowingReachabilityLoggerCannotEscape()',
    'ThrowingCandidateLoggerCannotEscape()',
    'ReferenceEquals(first, vanilla)',
    'ReferenceEquals(second, vanilla)',
):
    require(diagnostic_tests, token, "ReloadDiagnosticLoggingRegression.cs")

for forbidden in ("FindObjectsOfType", "Resources.FindObjectsOfTypeAll", "GetComponentsInChildren", "void Update("):
    if forbidden in source:
        violations.append(f"FastAccessSlotPatches.cs: forbidden reload implementation mechanism: {forbidden}")

if source.count('Activator.CreateInstance(harmonyType, new object[] { ReachabilityHarmonyId })') != 1:
    violations.append("FastAccessSlotPatches.cs: reachability Harmony owner must be created exactly once")
if source.count('Activator.CreateInstance(harmonyType, new object[] { CandidateBridgeHarmonyId })') != 1:
    violations.append("FastAccessSlotPatches.cs: candidate Harmony owner must be created exactly once")

if violations:
    raise SystemExit("Reload-access guard failed:\n" + "\n".join(violations))

print("Reload-access guard passed: exact pinned IEnumerable<Item>/IEnumerable<EquipmentSlot> bridge, exact slot-parameter execution guard, four-array content pin including post-lazy-enumeration reproof, one slot15 query, vanilla-first fail-closed semantics, lifecycle/rollback and regression authority verified.")
