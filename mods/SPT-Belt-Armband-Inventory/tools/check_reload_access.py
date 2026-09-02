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
reference_pin_tests = (ROOT / "tests" / "ReloadPseudoSlotReferencePinRegression.cs").read_text(encoding="utf-8-sig")
captured_state_tests = (ROOT / "tests" / "ReloadCapturedExecutionStateRegression.cs").read_text(encoding="utf-8-sig")

violations = []

def require(text, token, where):
    if token not in text:
        violations.append(f"{where}: missing {token}")

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
    'MethodInfo getItemsInSlots = GetItemsInSlots;',
    'object beltSlotsArgument = BeltSlotsArgument;',
    'Type itemType = ItemType;',
    'Type magazineType = MagazineType;',
    'Type returnType = ReturnType;',
    'Func<object, IEnumerable> getAllParentItems = GetAllParentItems;',
    'Func<object, string> readTemplateId = ReadTemplateId;',
    'getItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })',
    'ShouldReuseVanillaReloadCandidates(merged != null)',
    'return vanillaResult;',
):
    require(source, token, "FastAccessSlotPatches.cs")

for token in (
    'Type itemEnumerableType = typeof(IEnumerable<>).MakeGenericType(itemType);',
    'Type slotEnumerableType = typeof(IEnumerable<>).MakeGenericType(slotEnumType);',
    'getItemsInSlots.ReturnType != itemEnumerableType',
    'getItemsParameters[0].ParameterType != slotEnumerableType',
    'method.ReturnType != itemEnumerableType',
    'parameters[0].ParameterType != slotEnumerableType',
    'static bool HasExactExecutionContract(',
    'ReferenceEquals(GetItemsInSlots, getItemsInSlots)',
    'ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)',
    'ReferenceEquals(ItemType, itemType)',
    'ReferenceEquals(MagazineType, magazineType)',
    'ReferenceEquals(ReturnType, returnType)',
    'ReferenceEquals(GetAllParentItems, getAllParentItems)',
    'ReferenceEquals(ReadTemplateId, readTemplateId)',
    'static bool HasExactFallbackQueryContract(MethodInfo getItems, object beltArgument, Type itemType, Type declaredReturn)',
    'Type exactReturn = typeof(IEnumerable<>).MakeGenericType(itemType);',
    'declaredReturn != exactReturn || getItems.ReturnType != exactReturn',
    'Type slotElementType = GetEnumerableElementType(beltArgument.GetType());',
    'Type exactSlotEnumerable = typeof(IEnumerable<>).MakeGenericType(slotElementType);',
    'parameters[0].ParameterType != exactSlotEnumerable',
    '!exactSlotEnumerable.IsInstanceOfType(beltArgument)',
    'static Type GetEnumerableElementType(Type runtimeType)',
    'returnType.IsInstanceOfType(vanillaResult)',
    'vanillaResult is IEnumerable vanillaSequence',
    'returnType.IsInstanceOfType(beltResult)',
    'beltResult is IEnumerable beltItems',
    'itemType.IsInstanceOfType(item)',
    'magazineType.IsInstanceOfType(item)',
    'HasExactMagazineBeltAncestor(item, getAllParentItems, readTemplateId)',
    'Array result = Array.CreateInstance(itemType, merged.Count);',
    'static bool HasExactBeltSlotsArgument(object beltArgument)',
    'return HasExactBeltSlotsArgument(beltArgument);',
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

for token in (
    'HasPinnedFastAccessArrayContentForRegression(slots)',
    'var vanillaItems = new List<object>();',
    'foreach (object item in vanillaSequence)',
    'foreach (object item in beltItems)',
    'merged = new List<object>(vanillaItems);',
    'ContainsReference(vanillaItems, item)',
    'object beltSlotsArgument = BeltSlotsArgument;',
    'HasExactExecutionContract(getItemsInSlots, beltSlotsArgument, itemType, magazineType, returnType, getAllParentItems, readTemplateId)',
):
    require(source, token, "FastAccessSlotPatches.cs")

bridge_start = source.find("internal static object AppendCandidates(")
bridge_end = source.find("internal static void Reset()", bridge_start)
if bridge_start < 0 or bridge_end < 0:
    violations.append("FastAccessSlotPatches.cs: scoped candidate runtime region not found")
else:
    runtime = source[bridge_start:bridge_end]
    execution_proof = 'HasExactExecutionContract(getItemsInSlots, beltSlotsArgument, itemType, magazineType, returnType, getAllParentItems, readTemplateId)'
    if runtime.count('getItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })') != 1:
        violations.append("FastAccessSlotPatches.cs: scoped bridge must execute exactly one transaction-local MethodInfo/pseudo-slot15 query")
    if runtime.count('HasPinnedFastAccessArrayContentForRegression(slots)') < 4:
        violations.append("FastAccessSlotPatches.cs: slot-array pin must be proved before contract work, immediately pre-query, post-query/pre-enumeration, and post-enumeration/pre-publication")
    if runtime.count(execution_proof) < 4:
        violations.append("FastAccessSlotPatches.cs: complete MethodInfo/pseudo-slot/type/delegate snapshot must be re-proved at entry, pre-query, post-query/pre-enumeration, and pre-publication")
    if runtime.count('HasExactFallbackQueryContract(getItemsInSlots, beltSlotsArgument, itemType, returnType)') < 1:
        violations.append("FastAccessSlotPatches.cs: contract-entry proof must consume transaction-local MethodInfo, pseudo-slot, item type and declared return")
    if runtime.count('HasExactBeltSlotsArgument(beltSlotsArgument)') < 3:
        violations.append("FastAccessSlotPatches.cs: transaction-local pseudo-slot argument value must be re-proved immediately pre-query, post-query/pre-enumeration, and post-enumeration/pre-publication")
    for token in (
        'ReferenceEquals(GetItemsInSlots, getItemsInSlots)',
        'ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)',
        'ReferenceEquals(ItemType, itemType)',
        'ReferenceEquals(MagazineType, magazineType)',
        'ReferenceEquals(ReturnType, returnType)',
        'ReferenceEquals(GetAllParentItems, getAllParentItems)',
        'ReferenceEquals(ReadTemplateId, readTemplateId)',
    ):
        if runtime.count(token) != 1:
            violations.append(f"FastAccessSlotPatches.cs: captured execution helper must prove static identity exactly once per helper evaluation: {token}")
    for forbidden in (
        'GetItemsInSlots.Invoke(inventory',
        'new[] { BeltSlotsArgument }',
        'ItemType.IsInstanceOfType(item)',
        'MagazineType.IsInstanceOfType(item)',
        'ReturnType.IsInstanceOfType(vanillaResult)',
        'ReturnType.IsInstanceOfType(beltResult)',
        'Array.CreateInstance(ItemType',
        'HasExactMagazineBeltAncestor(item)',
    ):
        if forbidden in runtime:
            violations.append(f"FastAccessSlotPatches.cs: scoped bridge re-read mutable execution state instead of captured locals: {forbidden}")
    for forbidden in (
        "AppDomain.CurrentDomain.GetAssemblies", "ReflectionTools.GetTypes", "GetMethods(",
        "GetProperty(", "GetField(", "FindObjectsOfType", "GetComponentsInChildren", "new StackTrace",
    ):
        if forbidden in runtime:
            violations.append(f"FastAccessSlotPatches.cs: scoped hot path performs discovery/scan: {forbidden}")

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
    'primary AppendCandidates must preserve exact vanilla result identity on slot-parameter drift',
    'primary AppendCandidates must reject slot-parameter drift before any fallback query',
    'exact slot parameter contract must recover after rejected drift',
    'IEnumerable<long> slots',
):
    require(slot_parameter_tests, token, "ReloadSlotParameterContractRegression.cs")

for token in (
    'same-reference slot-array drift during lazy Belt enumeration must preserve the exact vanilla enumerable object',
    'same-reference pseudo-slot argument drift during lazy vanilla enumeration must preserve exact vanilla identity',
    'pseudo-slot argument drift during vanilla enumeration must fail closed before any fallback query',
    'same-reference pseudo-slot argument drift during lazy Belt enumeration must preserve exact vanilla identity',
    'lazy Belt pseudo-slot drift must not trigger a retry or second query',
    'MethodInfo replacement during lazy vanilla enumeration must preserve exact vanilla identity',
    'MethodInfo replacement during vanilla enumeration must fail closed before any fallback query',
    'MethodInfo replacement during lazy Belt enumeration must preserve exact vanilla identity',
    'lazy Belt MethodInfo drift must retain the captured one-query boundary and never redirect or retry',
    'restoring the exact captured content must restore the recognized retained-array pin',
    'healthy lazy Belt enumeration with unchanged pinned inputs must publish a replacement sequence',
    'healthy lazy Belt enumeration must preserve vanilla prefix and append the exact Belt descendant',
    'duringEnumeration?.Invoke();',
):
    require(lazy_pin_tests, token, "ReloadLazyEnumerationPinRegression.cs")

for token in (
    'MethodInfo getItemsInSlots = GetItemsInSlots;',
    'object beltSlotsArgument = BeltSlotsArgument;',
    'Type itemType = ItemType;',
    'Type magazineType = MagazineType;',
    'Type returnType = ReturnType;',
    'Func<object, IEnumerable> getAllParentItems = GetAllParentItems;',
    'Func<object, string> readTemplateId = ReadTemplateId;',
    'getItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument })',
    'static bool HasExactBeltSlotsArgument(object beltArgument)',
    'complete execution snapshot must be re-proven at all four bounded stages',
):
    require(reference_pin_tests, token, "ReloadPseudoSlotReferencePinRegression.cs")

for token in (
    'RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.ItemType = typeof(object), "ItemType")',
    'RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.MagazineType = typeof(FakeItem), "MagazineType")',
    'RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.ReturnType = typeof(IEnumerable<object>), "ReturnType")',
    'RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.GetAllParentItems = _ => Array.Empty<object>(), "GetAllParentItems")',
    'RunPreQueryDrift(slots, magazine, () => ReloadCandidateBridgeRuntime.ReadTemplateId = _ => "replacement", "ReadTemplateId")',
    'execution-state drift during lazy Belt enumeration must preserve exact vanilla identity',
    'post-query execution-state drift must not retry or redirect the single slot15 query',
    'restored exact execution state must retain one-query vanilla-prefix Belt append behavior',
):
    require(captured_state_tests, token, "ReloadCapturedExecutionStateRegression.cs")

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

print("Reload-access guard passed: exact pinned IEnumerable<Item>/IEnumerable<EquipmentSlot> bridge, transaction-local MethodInfo + pseudo-slot15 + type/delegate execution snapshot across both lazy windows, four-array content pin, one slot15 query, vanilla-first fail-closed semantics, lifecycle/rollback and regression authority verified.")
