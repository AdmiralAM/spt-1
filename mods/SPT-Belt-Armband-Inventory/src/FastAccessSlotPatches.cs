using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class FastAccessSlotPolicy
    {
        internal static string[] Extend(string[] source)
        {
            if (source == null) return null;
            string[] result = CopyAppendUnique(source, BeltSlotPlan.ArmBand);
            result = CopyAppendUnique(result, RuntimeIdentity.DedicatedBeltWireSlotId);
            return result;
        }

        static string[] CopyAppendUnique(string[] source, string value)
        {
            for (int i = 0; i < source.Length; i++)
                if (string.Equals(source[i], value, StringComparison.Ordinal))
                {
                    string[] copy = new string[source.Length];
                    Array.Copy(source, copy, source.Length);
                    return copy;
                }

            string[] result = new string[source.Length + 1];
            Array.Copy(source, result, source.Length);
            result[result.Length - 1] = value;
            return result;
        }

        internal static bool ShouldRestoreReference(object currentValue, object installedValue)
        {
            return installedValue != null && ReferenceEquals(currentValue, installedValue);
        }

        internal static bool ShouldPromoteReloadReachability(bool vanillaResult, bool isMagazine, bool hasFastAccessWearableAncestor)
        {
            return !vanillaResult && isMagazine && hasFastAccessWearableAncestor;
        }

        internal static bool ShouldBridgeReloadCandidates(bool reloadScopeActive, bool reentrant, bool isFastAccessSlotArray)
        {
            return reloadScopeActive && !reentrant && isFastAccessSlotArray;
        }

        internal static bool ShouldReuseVanillaReloadCandidates(bool appendedExactBeltCandidate)
        {
            return !appendedExactBeltCandidate;
        }
    }

    internal static class ReloadDiagnosticLog
    {
        internal static void TryInfo(Action<string> sink, string message)
        {
            try { sink?.Invoke(message); }
            catch { }
        }

        internal static void TryWarning(Action<string> sink, string message)
        {
            try { sink?.Invoke(message); }
            catch { }
        }
    }

    internal static class FastAccessReloadRuntime
    {
        internal static Type ItemType;
        internal static Type MagazineType;
        internal static Func<object, IEnumerable> GetAllParentItems;
        internal static Func<object, string> ReadTemplateId;
        internal static Action<string> LogWarning;
        static bool failureLogged;

        internal static void PromoteReachability(object item, ref bool result)
        {
            bool isMagazine = item != null && MagazineType != null && MagazineType.IsInstanceOfType(item);
            if (!FastAccessSlotPolicy.ShouldPromoteReloadReachability(result, isMagazine, true)
                || GetAllParentItems == null || ReadTemplateId == null)
                return;

            try
            {
                IEnumerable parents = GetAllParentItems(item);
                if (parents == null) return;
                foreach (object parent in parents)
                {
                    string templateId = parent == null ? null : ReadTemplateId(parent);
                    bool fastAccessRoot = WearableItemDescriptorRegistry.HasCapability(templateId, AccessoryCapability.FastAccess);
                    if (FastAccessSlotPolicy.ShouldPromoteReloadReachability(result, isMagazine, fastAccessRoot))
                    {
                        result = true;
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                if (failureLogged) return;
                failureLogged = true;
                Exception root = Unwrap(exception);
                ReloadDiagnosticLog.TryWarning(LogWarning,
                    "B&A&HB reload reachability failed closed: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        internal static void Reset()
        {
            ItemType = null;
            MagazineType = null;
            GetAllParentItems = null;
            ReadTemplateId = null;
            LogWarning = null;
            failureLogged = false;
        }

        static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException invocation && invocation.InnerException != null)
                exception = invocation.InnerException;
            return exception;
        }
    }

    internal static class ReloadCandidateBridgeRuntime
    {
        internal static MethodInfo GetItemsInSlots;
        internal static object BeltSlotsArgument;
        internal static object OriginalFastAccessSlots;
        internal static object InstalledFastAccessSlots;
        internal static object OriginalBindAvailableSlots;
        internal static object InstalledBindAvailableSlots;
        internal static Type ItemType;
        internal static Type MagazineType;
        internal static Type ReturnType;
        internal static Func<object, IEnumerable> GetAllParentItems;
        internal static Func<object, string> ReadTemplateId;
        internal static Action<string> LogWarning;

        [ThreadStatic] static int reloadDepth;
        [ThreadStatic] static bool reentrant;
        static bool failureLogged;

        internal static void EnterReloadScope()
        {
            reloadDepth++;
        }

        internal static Exception ExitReloadScope(Exception exception)
        {
            if (reloadDepth > 0) reloadDepth--;
            return exception;
        }

        internal static object AppendCandidates(object inventory, object slots, object vanillaResult)
        {
            bool fastAccessArray = slots != null
                && (ReferenceEquals(slots, OriginalFastAccessSlots)
                    || ReferenceEquals(slots, InstalledFastAccessSlots)
                    || ReferenceEquals(slots, OriginalBindAvailableSlots)
                    || ReferenceEquals(slots, InstalledBindAvailableSlots));
            MethodInfo getItemsInSlots = GetItemsInSlots;
            object beltSlotsArgument = BeltSlotsArgument;
            if (!FastAccessSlotPolicy.ShouldBridgeReloadCandidates(reloadDepth > 0, reentrant, fastAccessArray)
                || inventory == null || vanillaResult == null || getItemsInSlots == null || beltSlotsArgument == null
                || ItemType == null || MagazineType == null || ReturnType == null
                || GetAllParentItems == null || ReadTemplateId == null)
                return vanillaResult;

            try
            {
                if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots))
                    return vanillaResult;

                if (!ReferenceEquals(GetItemsInSlots, getItemsInSlots)
                    || !ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)
                    || !HasExactFallbackQueryContract(getItemsInSlots, beltSlotsArgument)
                    || !ReturnType.IsInstanceOfType(vanillaResult)
                    || !(vanillaResult is IEnumerable vanillaSequence))
                    return vanillaResult;

                var vanillaItems = new List<object>();
                foreach (object item in vanillaSequence)
                {
                    if (item != null && !ItemType.IsInstanceOfType(item))
                        return vanillaResult;
                    vanillaItems.Add(item);
                }

                // Every mutable input to the bounded fallback query must still match its
                // transaction-local/install-time contract immediately before invoke. The
                // pseudo-slot argument and exact MethodInfo are both captured once at bridge
                // entry, so replacement during lazy vanilla enumeration cannot redirect this query.
                if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots)
                    || !ReferenceEquals(GetItemsInSlots, getItemsInSlots)
                    || !ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)
                    || !HasExactBeltSlotsArgument(beltSlotsArgument))
                    return vanillaResult;

                object beltResult;
                reentrant = true;
                try
                {
                    beltResult = getItemsInSlots.Invoke(inventory, new[] { beltSlotsArgument });
                }
                finally
                {
                    reentrant = false;
                }

                if (beltResult == null || !ReturnType.IsInstanceOfType(beltResult) || !(beltResult is IEnumerable beltItems)
                    || !ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots)
                    || !ReferenceEquals(GetItemsInSlots, getItemsInSlots)
                    || !ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)
                    || !HasExactBeltSlotsArgument(beltSlotsArgument))
                    return vanillaResult;

                List<object> merged = null;
                foreach (object item in beltItems)
                {
                    if (item == null) continue;
                    if (!ItemType.IsInstanceOfType(item)) return vanillaResult;
                    if (!MagazineType.IsInstanceOfType(item) || !HasExactMagazineBeltAncestor(item)) continue;
                    if (ContainsReference(vanillaItems, item) || (merged != null && ContainsReference(merged, item))) continue;

                    if (merged == null)
                        merged = new List<object>(vanillaItems);
                    merged.Add(item);
                }

                // Re-prove all mutable transaction inputs after Belt enumeration and
                // immediately before publication. Replacement never retries and cannot
                // make a second query with a different method or argument instance.
                if (!ReloadScopeEpochGuard.HasPinnedFastAccessArrayContentForRegression(slots)
                    || !ReferenceEquals(GetItemsInSlots, getItemsInSlots)
                    || !ReferenceEquals(BeltSlotsArgument, beltSlotsArgument)
                    || !HasExactBeltSlotsArgument(beltSlotsArgument))
                    return vanillaResult;

                if (FastAccessSlotPolicy.ShouldReuseVanillaReloadCandidates(merged != null))
                    return vanillaResult;

                Array result = Array.CreateInstance(ItemType, merged.Count);
                for (int i = 0; i < merged.Count; i++) result.SetValue(merged[i], i);
                return ReturnType.IsInstanceOfType(result) ? result : vanillaResult;
            }
            catch (Exception exception)
            {
                reentrant = false;
                if (!failureLogged)
                {
                    failureLogged = true;
                    Exception root = Unwrap(exception);
                    ReloadDiagnosticLog.TryWarning(LogWarning,
                        "B&A&HB scoped reload candidate bridge failed closed: " + root.GetType().FullName + ": " + root.Message);
                }
                return vanillaResult;
            }
        }

        static bool HasExactFallbackQueryContract(MethodInfo getItems, object beltArgument)
        {
            try
            {
                Type itemType = ItemType;
                Type declaredReturn = ReturnType;
                if (itemType == null || declaredReturn == null || getItems == null || beltArgument == null)
                    return false;

                Type exactReturn = typeof(IEnumerable<>).MakeGenericType(itemType);
                if (declaredReturn != exactReturn || getItems.ReturnType != exactReturn)
                    return false;

                Type slotElementType = GetEnumerableElementType(beltArgument.GetType());
                if (slotElementType == null)
                    return false;
                Type exactSlotEnumerable = typeof(IEnumerable<>).MakeGenericType(slotElementType);
                ParameterInfo[] parameters = getItems.GetParameters();
                if (parameters.Length != 1
                    || parameters[0].ParameterType != exactSlotEnumerable
                    || !exactSlotEnumerable.IsInstanceOfType(beltArgument))
                    return false;

                return HasExactBeltSlotsArgument(beltArgument);
            }
            catch
            {
                return false;
            }
        }

        static bool HasExactBeltSlotsArgument(object beltArgument)
        {
            try
            {
                if (!(beltArgument is IEnumerable values))
                    return false;

                int count = 0;
                foreach (object value in values)
                {
                    if (value == null || Convert.ToInt32(value) != RuntimeIdentity.DedicatedBeltEquipmentSlotValue)
                        return false;
                    count++;
                    if (count > 1)
                        return false;
                }
                return count == 1;
            }
            catch
            {
                return false;
            }
        }

        static Type GetEnumerableElementType(Type runtimeType)
        {
            if (runtimeType == null) return null;
            if (runtimeType.IsArray) return runtimeType.GetElementType();

            Type selected = null;
            Type[] interfaces = runtimeType.GetInterfaces();
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type candidate = interfaces[i];
                if (!candidate.IsGenericType || candidate.GetGenericTypeDefinition() != typeof(IEnumerable<>)) continue;
                Type element = candidate.GetGenericArguments()[0];
                if (selected != null && selected != element) return null;
                selected = element;
            }
            return selected;
        }

        static bool ContainsReference(List<object> items, object candidate)
        {
            for (int i = 0; i < items.Count; i++)
                if (ReferenceEquals(items[i], candidate)) return true;
            return false;
        }

        static bool HasExactMagazineBeltAncestor(object item)
        {
            IEnumerable parents = GetAllParentItems(item);
            if (parents == null) return false;
            foreach (object parent in parents)
            {
                string templateId = parent == null ? null : ReadTemplateId(parent);
                if (string.Equals(templateId, RuntimeIdentity.DedicatedMagazineBeltItemId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal static void Reset()
        {
            GetItemsInSlots = null;
            BeltSlotsArgument = null;
            OriginalFastAccessSlots = null;
            InstalledFastAccessSlots = null;
            OriginalBindAvailableSlots = null;
            InstalledBindAvailableSlots = null;
            ItemType = null;
            MagazineType = null;
            ReturnType = null;
            GetAllParentItems = null;
            ReadTemplateId = null;
            LogWarning = null;
            reloadDepth = 0;
            reentrant = false;
            failureLogged = false;
        }

        static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException invocation && invocation.InnerException != null)
                exception = invocation.InnerException;
            return exception;
        }
    }

    internal sealed class FastAccessSlotPatches : IDisposable
    {
        const string ReachabilityHarmonyId = "com.admiralam.spt.belt-armband-inventory.fast-access.reachability";
        const string CandidateBridgeHarmonyId = "com.admiralam.spt.belt-armband-inventory.fast-access.reload-candidate";

        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        FieldInfo fastAccessSlotsField;
        FieldInfo bindAvailableSlotsField;
        object originalFastAccessSlots;
        object originalBindAvailableSlots;
        object installedFastAccessSlots;
        object installedBindAvailableSlots;
        object reachabilityHarmony;
        MethodInfo reachabilityUnpatchSelf;
        object candidateBridgeHarmony;
        MethodInfo candidateBridgeUnpatchSelf;
        bool wroteFastAccessSlots;
        bool wroteBindAvailableSlots;
        bool reloadPatchInstalled;
        bool reloadCandidateBridgeInstalled;
        bool installed;

        internal FastAccessSlotPatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                Type inventoryType = ReflectionTools.FindType("EFT.InventoryLogic.Inventory");
                Type slotEnumType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                if (inventoryType == null || slotEnumType == null)
                    return Fail("SPT 4.1 Inventory/EquipmentSlot was not found; wearable fast-access slot compatibility is disabled.");

                fastAccessSlotsField = inventoryType.GetField("FastAccessSlots", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                bindAvailableSlotsField = inventoryType.GetField("BindAvailableSlotsExtended", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (!IsSlotArray(fastAccessSlotsField, slotEnumType) || !IsSlotArray(bindAvailableSlotsField, slotEnumType))
                    return Fail("SPT 4.1 fast-access slot arrays changed shape; wearable fast-access compatibility is disabled.");

                object armBand = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);
                object dedicatedBelt = Enum.ToObject(slotEnumType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue);
                originalFastAccessSlots = fastAccessSlotsField.GetValue(null);
                originalBindAvailableSlots = bindAvailableSlotsField.GetValue(null);
                installedFastAccessSlots = AppendSlots(originalFastAccessSlots as Array, slotEnumType, armBand, dedicatedBelt);
                installedBindAvailableSlots = AppendSlots(originalBindAvailableSlots as Array, slotEnumType, armBand, dedicatedBelt);
                if (installedFastAccessSlots == null || installedBindAvailableSlots == null)
                    return Fail("SPT 4.1 fast-access slot arrays could not be extended safely; wearable fast-access compatibility is disabled.");

                fastAccessSlotsField.SetValue(null, installedFastAccessSlots);
                wroteFastAccessSlots = true;
                bindAvailableSlotsField.SetValue(null, installedBindAvailableSlots);
                wroteBindAvailableSlots = true;
                installed = true;

                bool reachability = TryInstallReloadReachability();
                bool candidateBridge = reachability && TryInstallReloadCandidateBridge(inventoryType, slotEnumType, dedicatedBelt);
                if (reachability && candidateBridge)
                    ReloadDiagnosticLog.TryInfo(logInfo,
                        "B&A&HB fast-access installed: vanilla ArmBand/Belt arrays extended; reload reachability is exact; Reload/QuickReload preserve vanilla candidates and append exact Magazine Belt descendants as scoped fallback.");
                else if (reachability)
                    ReloadDiagnosticLog.TryWarning(logWarning,
                        "B&A&HB fast-access arrays/reachability remain active, but the atomic Reload/QuickReload candidate bridge could not bind; Magazine Armband remains reachable and Magazine Belt remains reserve-only for this session.");
                else
                    ReloadDiagnosticLog.TryWarning(logWarning,
                        "B&A&HB fast-access slot arrays remain active, but exact reload reachability could not bind; wearable magazines remain reserve-only for this session.");
                return true;
            }
            catch (Exception exception)
            {
                RestoreOwnedWrites();
                UnpatchReload();
                ClearState();
                return Fail("Wearable fast-access slot compatibility installation failed safely: " + Unwrap(exception).Message);
            }
        }

        bool TryInstallReloadReachability()
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type controllerType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryController");
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                Type magazineType = ReflectionTools.FindType("EFT.InventoryLogic.Magazine");
                if (harmonyType == null || harmonyMethodType == null || controllerType == null || itemType == null || magazineType == null)
                    return false;

                MethodInfo reachable = ReflectionTools.FindInstanceMethod(controllerType, "IsAtReachablePlace", typeof(bool), itemType);
                MethodInfo parentsMethod = FindGetAllParentItems(itemType);
                MemberInfo templateIdMember = FindReadableMember(itemType, "StringTemplateId", typeof(string));
                ConstructorInfo harmonyMethodCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                reachabilityUnpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (reachable == null || parentsMethod == null || templateIdMember == null
                    || harmonyMethodCtor == null || patchMethod == null || reachabilityUnpatchSelf == null)
                    return false;

                FastAccessReloadRuntime.ItemType = itemType;
                FastAccessReloadRuntime.MagazineType = magazineType;
                FastAccessReloadRuntime.GetAllParentItems = BuildParentEnumerator(parentsMethod, itemType);
                FastAccessReloadRuntime.ReadTemplateId = BuildStringReader(itemType, templateIdMember);
                FastAccessReloadRuntime.LogWarning = logWarning;
                if (FastAccessReloadRuntime.GetAllParentItems == null || FastAccessReloadRuntime.ReadTemplateId == null)
                    return false;

                reachabilityHarmony = Activator.CreateInstance(harmonyType, new object[] { ReachabilityHarmonyId });
                if (reachabilityHarmony == null) return false;
                MethodInfo postfixMethod = BuildReachabilityPostfix(itemType);
                object postfix = harmonyMethodCtor.Invoke(new object[] { postfixMethod });
                PatchNamed(reachabilityHarmony, patchMethod, harmonyMethodType, reachable, "postfix", postfix);
                reloadPatchInstalled = true;
                return true;
            }
            catch (Exception exception)
            {
                ReloadDiagnosticLog.TryWarning(logWarning,
                    "B&A&HB reload reachability discovery failed closed: " + Unwrap(exception).Message);
                UnpatchReachability();
                return false;
            }
        }

        bool TryInstallReloadCandidateBridge(Type inventoryType, Type slotEnumType, object dedicatedBelt)
        {
            try
            {
                if (!reloadPatchInstalled || FastAccessReloadRuntime.ItemType == null || FastAccessReloadRuntime.MagazineType == null
                    || FastAccessReloadRuntime.GetAllParentItems == null || FastAccessReloadRuntime.ReadTemplateId == null)
                    return false;

                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type translatorType = ReflectionTools.FindType("EFT.FirearmHandsInputTranslator");
                if (harmonyType == null || harmonyMethodType == null || translatorType == null) return false;

                Type itemType = FastAccessReloadRuntime.ItemType;
                if (!itemType.IsAssignableFrom(FastAccessReloadRuntime.MagazineType)) return false;

                MethodInfo reload = FindExactZeroArgVoidMethod(translatorType, "Reload");
                MethodInfo quickReload = FindExactZeroArgVoidMethod(translatorType, "QuickReload");
                MethodInfo getItemsInSlots = FindGetItemsInSlots(inventoryType, slotEnumType, itemType);
                ConstructorInfo harmonyMethodCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                candidateBridgeUnpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (reload == null || quickReload == null || getItemsInSlots == null || harmonyMethodCtor == null
                    || patchMethod == null || candidateBridgeUnpatchSelf == null)
                    return false;

                ParameterInfo[] getItemsParameters = getItemsInSlots.GetParameters();
                Type itemEnumerableType = typeof(IEnumerable<>).MakeGenericType(itemType);
                Type slotEnumerableType = typeof(IEnumerable<>).MakeGenericType(slotEnumType);
                if (getItemsParameters.Length != 1
                    || getItemsInSlots.ReturnType != itemEnumerableType
                    || getItemsParameters[0].ParameterType != slotEnumerableType)
                    return false;
                object beltSlotsArgument = CreateSingleSlotArgument(getItemsParameters[0].ParameterType, slotEnumType, dedicatedBelt);
                if (beltSlotsArgument == null)
                    return false;

                ReloadCandidateBridgeRuntime.GetItemsInSlots = getItemsInSlots;
                ReloadCandidateBridgeRuntime.BeltSlotsArgument = beltSlotsArgument;
                ReloadCandidateBridgeRuntime.OriginalFastAccessSlots = originalFastAccessSlots;
                ReloadCandidateBridgeRuntime.InstalledFastAccessSlots = installedFastAccessSlots;
                ReloadCandidateBridgeRuntime.OriginalBindAvailableSlots = originalBindAvailableSlots;
                ReloadCandidateBridgeRuntime.InstalledBindAvailableSlots = installedBindAvailableSlots;
                ReloadCandidateBridgeRuntime.ItemType = itemType;
                ReloadCandidateBridgeRuntime.MagazineType = FastAccessReloadRuntime.MagazineType;
                ReloadCandidateBridgeRuntime.ReturnType = getItemsInSlots.ReturnType;
                ReloadCandidateBridgeRuntime.GetAllParentItems = FastAccessReloadRuntime.GetAllParentItems;
                ReloadCandidateBridgeRuntime.ReadTemplateId = FastAccessReloadRuntime.ReadTemplateId;
                ReloadCandidateBridgeRuntime.LogWarning = logWarning;

                candidateBridgeHarmony = Activator.CreateInstance(harmonyType, new object[] { CandidateBridgeHarmonyId });
                if (candidateBridgeHarmony == null) return false;

                object prefix = harmonyMethodCtor.Invoke(new object[] { BuildReloadScopePrefix() });
                object finalizer = harmonyMethodCtor.Invoke(new object[] { BuildReloadScopeFinalizer() });
                object candidatesPostfix = harmonyMethodCtor.Invoke(new object[] { BuildCandidatePostfix(inventoryType, getItemsParameters[0].ParameterType, getItemsInSlots.ReturnType) });

                PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, reload, "prefix", prefix);
                PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, reload, "finalizer", finalizer);
                PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, quickReload, "prefix", prefix);
                PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, quickReload, "finalizer", finalizer);
                PatchNamed(candidateBridgeHarmony, patchMethod, harmonyMethodType, getItemsInSlots, "postfix", candidatesPostfix);
                reloadCandidateBridgeInstalled = true;
                return true;
            }
            catch (Exception exception)
            {
                ReloadDiagnosticLog.TryWarning(logWarning,
                    "B&A&HB scoped reload candidate discovery failed closed and rolled back atomically: " + Unwrap(exception).Message);
                UnpatchCandidateBridge();
                return false;
            }
        }

        static MethodInfo FindExactZeroArgVoidMethod(Type type, string name)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal) || method.ReturnType != typeof(void) || method.GetParameters().Length != 0) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static MethodInfo FindGetItemsInSlots(Type inventoryType, Type slotEnumType, Type itemType)
        {
            if (inventoryType == null || slotEnumType == null || itemType == null) return null;
            MethodInfo selected = null;
            Type itemEnumerableType = typeof(IEnumerable<>).MakeGenericType(itemType);
            Type slotEnumerableType = typeof(IEnumerable<>).MakeGenericType(slotEnumType);
            MethodInfo[] methods = inventoryType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "GetItemsInSlots", StringComparison.Ordinal) || method.ContainsGenericParameters
                    || method.ReturnType != itemEnumerableType) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != slotEnumerableType) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static bool CanCarrySlotValues(Type parameterType, Type slotEnumType)
        {
            if (parameterType.IsArray) return parameterType.GetElementType() == slotEnumType;
            Type listType = typeof(List<>).MakeGenericType(slotEnumType);
            return parameterType.IsAssignableFrom(listType);
        }

        static object CreateSingleSlotArgument(Type parameterType, Type slotEnumType, object slot)
        {
            if (parameterType.IsArray && parameterType.GetElementType() == slotEnumType)
            {
                Array array = Array.CreateInstance(slotEnumType, 1);
                array.SetValue(slot, 0);
                return array;
            }

            Type listType = typeof(List<>).MakeGenericType(slotEnumType);
            if (!parameterType.IsAssignableFrom(listType)) return null;
            object list = Activator.CreateInstance(listType);
            listType.GetMethod("Add", new[] { slotEnumType })?.Invoke(list, new[] { slot });
            return list;
        }

        static MethodInfo BuildReloadScopePrefix()
        {
            DynamicMethod dynamic = new DynamicMethod("BAndHBReloadCandidateScopePrefix", typeof(void), Type.EmptyTypes, typeof(FastAccessSlotPatches), true);
            ILGenerator il = dynamic.GetILGenerator();
            il.Emit(OpCodes.Call, typeof(ReloadCandidateBridgeRuntime).GetMethod(nameof(ReloadCandidateBridgeRuntime.EnterReloadScope), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return dynamic;
        }

        static MethodInfo BuildReloadScopeFinalizer()
        {
            DynamicMethod dynamic = new DynamicMethod("BAndHBReloadCandidateScopeFinalizer", typeof(Exception), new[] { typeof(Exception) }, typeof(FastAccessSlotPatches), true);
            dynamic.DefineParameter(1, ParameterAttributes.None, "__exception");
            ILGenerator il = dynamic.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(ReloadCandidateBridgeRuntime).GetMethod(nameof(ReloadCandidateBridgeRuntime.ExitReloadScope), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return dynamic;
        }

        static MethodInfo BuildCandidatePostfix(Type inventoryType, Type slotsParameterType, Type returnType)
        {
            DynamicMethod dynamic = new DynamicMethod(
                "BAndHBReloadCandidatePostfix",
                typeof(void),
                new[] { inventoryType, slotsParameterType, returnType.MakeByRefType() },
                typeof(FastAccessSlotPatches),
                true);
            dynamic.DefineParameter(1, ParameterAttributes.None, "__instance");
            dynamic.DefineParameter(2, ParameterAttributes.None, "__0");
            dynamic.DefineParameter(3, ParameterAttributes.Out, "__result");
            ILGenerator il = dynamic.GetILGenerator();
            LocalBuilder merged = il.DeclareLocal(typeof(object));
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Call, typeof(ReloadCandidateBridgeRuntime).GetMethod(nameof(ReloadCandidateBridgeRuntime.AppendCandidates), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Stloc, merged);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldloc, merged);
            il.Emit(OpCodes.Castclass, returnType);
            il.Emit(OpCodes.Stind_Ref);
            il.Emit(OpCodes.Ret);
            return dynamic;
        }

        static MethodInfo FindGetAllParentItems(Type itemType)
        {
            MethodInfo selected = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types = ReflectionTools.GetTypes(assemblies[a]);
                for (int t = 0; t < types.Length; t++)
                {
                    Type type = types[t];
                    if (type == null || !(type.IsAbstract && type.IsSealed)) continue;
                    MethodInfo[] methods;
                    try { methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
                    catch { continue; }
                    for (int m = 0; m < methods.Length; m++)
                    {
                        MethodInfo method = methods[m];
                        if (!string.Equals(method.Name, "GetAllParentItems", StringComparison.Ordinal) || method.ContainsGenericParameters) continue;
                        ParameterInfo[] parameters = method.GetParameters();
                        if (parameters.Length != 1 || parameters[0].ParameterType != itemType) continue;
                        if (!typeof(IEnumerable).IsAssignableFrom(method.ReturnType)) continue;
                        if (selected != null) return null;
                        selected = method;
                    }
                }
            }
            return selected;
        }

        static MemberInfo FindReadableMember(Type type, string name, Type expectedType)
        {
            MemberInfo selected = null;
            int matches = 0;
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo[] properties;
                try { properties = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { properties = Array.Empty<PropertyInfo>(); }
                for (int i = 0; i < properties.Length; i++)
                {
                    PropertyInfo property = properties[i];
                    if (!string.Equals(property.Name, name, StringComparison.Ordinal) || property.GetIndexParameters().Length != 0
                        || property.GetGetMethod(true) == null || property.PropertyType != expectedType) continue;
                    matches++;
                    if (selected == null) selected = property;
                }

                FieldInfo[] fields;
                try { fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { fields = Array.Empty<FieldInfo>(); }
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!string.Equals(field.Name, name, StringComparison.Ordinal) || field.FieldType != expectedType) continue;
                    matches++;
                    if (selected == null) selected = field;
                }
            }
            return matches == 1 ? selected : null;
        }

        static Func<object, IEnumerable> BuildParentEnumerator(MethodInfo method, Type itemType)
        {
            try
            {
                DynamicMethod dynamic = new DynamicMethod("BAndHBGetAllParentItems", typeof(IEnumerable), new[] { typeof(object) }, typeof(FastAccessSlotPatches), true);
                ILGenerator il = dynamic.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, itemType);
                il.Emit(OpCodes.Call, method);
                if (method.ReturnType != typeof(IEnumerable)) il.Emit(OpCodes.Castclass, typeof(IEnumerable));
                il.Emit(OpCodes.Ret);
                return (Func<object, IEnumerable>)dynamic.CreateDelegate(typeof(Func<object, IEnumerable>));
            }
            catch { return null; }
        }

        static Func<object, string> BuildStringReader(Type declaringType, MemberInfo member)
        {
            try
            {
                DynamicMethod dynamic = new DynamicMethod("BAndHBReloadTemplateId", typeof(string), new[] { typeof(object) }, typeof(FastAccessSlotPatches), true);
                ILGenerator il = dynamic.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                if (member is PropertyInfo property)
                {
                    MethodInfo getter = property.GetGetMethod(true);
                    if (getter == null || property.PropertyType != typeof(string)) return null;
                    il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
                }
                else if (member is FieldInfo field)
                {
                    if (field.FieldType != typeof(string)) return null;
                    il.Emit(OpCodes.Ldfld, field);
                }
                else return null;
                il.Emit(OpCodes.Ret);
                return (Func<object, string>)dynamic.CreateDelegate(typeof(Func<object, string>));
            }
            catch { return null; }
        }

        static MethodInfo BuildReachabilityPostfix(Type itemType)
        {
            DynamicMethod dynamic = new DynamicMethod(
                "BAndHBReloadReachabilityPostfix",
                typeof(void),
                new[] { itemType, typeof(bool).MakeByRefType() },
                typeof(FastAccessSlotPatches),
                true);
            dynamic.DefineParameter(1, ParameterAttributes.None, "__0");
            dynamic.DefineParameter(2, ParameterAttributes.Out, "__result");
            ILGenerator il = dynamic.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(FastAccessReloadRuntime).GetMethod(nameof(FastAccessReloadRuntime.PromoteReachability), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return dynamic;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!IsCompatiblePatchMethod(method, harmonyMethodType)) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static bool IsCompatiblePatchMethod(MethodInfo method, Type harmonyMethodType)
        {
            if (method == null || harmonyMethodType == null || !string.Equals(method.Name, "Patch", StringComparison.Ordinal)) return false;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length < 4 || parameters[0].ParameterType != typeof(MethodBase)) return false;

            bool prefix = false;
            bool postfix = false;
            bool finalizer = false;
            for (int i = 1; i < parameters.Length; i++)
            {
                ParameterInfo parameter = parameters[i];
                if (parameter.ParameterType != harmonyMethodType) continue;
                if (string.Equals(parameter.Name, "prefix", StringComparison.OrdinalIgnoreCase))
                {
                    if (prefix) return false;
                    prefix = true;
                }
                else if (string.Equals(parameter.Name, "postfix", StringComparison.OrdinalIgnoreCase))
                {
                    if (postfix) return false;
                    postfix = true;
                }
                else if (string.Equals(parameter.Name, "finalizer", StringComparison.OrdinalIgnoreCase))
                {
                    if (finalizer) return false;
                    finalizer = true;
                }
            }
            return prefix && postfix && finalizer;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo selected = null;
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, name, StringComparison.Ordinal) || method.GetParameters().Length != 0) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static void PatchNamed(object owner, MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, string patchKind, object patch)
        {
            if (owner == null) throw new InvalidOperationException("Harmony owner is not initialized for " + patchKind);
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            bool assigned = false;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, patchKind, StringComparison.OrdinalIgnoreCase))
                {
                    args[i] = patch;
                    assigned = true;
                }
            }
            if (!assigned) throw new MissingMethodException("Harmony.Patch parameter not found: " + patchKind);
            patchMethod.Invoke(owner, args);
        }

        static bool IsSlotArray(FieldInfo field, Type slotEnumType)
        {
            return field != null && field.FieldType.IsArray && field.FieldType.GetElementType() == slotEnumType;
        }

        static Array AppendSlots(Array source, Type slotEnumType, params object[] additions)
        {
            if (source == null || slotEnumType == null || additions == null) return null;
            int unique = 0;
            for (int a = 0; a < additions.Length; a++)
            {
                bool exists = false;
                for (int i = 0; i < source.Length; i++) if (Equals(source.GetValue(i), additions[a])) { exists = true; break; }
                if (!exists) unique++;
            }

            Array result = Array.CreateInstance(slotEnumType, source.Length + unique);
            Array.Copy(source, result, source.Length);
            int write = source.Length;
            for (int a = 0; a < additions.Length; a++)
            {
                bool exists = false;
                for (int i = 0; i < write; i++) if (Equals(result.GetValue(i), additions[a])) { exists = true; break; }
                if (!exists) result.SetValue(additions[a], write++);
            }
            return result;
        }

        void RestoreOwnedWrites()
        {
            try
            {
                if (wroteBindAvailableSlots && bindAvailableSlotsField != null && originalBindAvailableSlots != null
                    && FastAccessSlotPolicy.ShouldRestoreReference(bindAvailableSlotsField.GetValue(null), installedBindAvailableSlots))
                    bindAvailableSlotsField.SetValue(null, originalBindAvailableSlots);
            }
            catch { }

            try
            {
                if (wroteFastAccessSlots && fastAccessSlotsField != null && originalFastAccessSlots != null
                    && FastAccessSlotPolicy.ShouldRestoreReference(fastAccessSlotsField.GetValue(null), installedFastAccessSlots))
                    fastAccessSlotsField.SetValue(null, originalFastAccessSlots);
            }
            catch { }
        }

        void UnpatchCandidateBridge()
        {
            try
            {
                if (candidateBridgeHarmony != null && candidateBridgeUnpatchSelf != null)
                    candidateBridgeUnpatchSelf.Invoke(candidateBridgeHarmony, null);
            }
            catch { }
            candidateBridgeHarmony = null;
            candidateBridgeUnpatchSelf = null;
            reloadCandidateBridgeInstalled = false;
            ReloadCandidateBridgeRuntime.Reset();
        }

        void UnpatchReachability()
        {
            try
            {
                if (reachabilityHarmony != null && reachabilityUnpatchSelf != null)
                    reachabilityUnpatchSelf.Invoke(reachabilityHarmony, null);
            }
            catch { }
            reachabilityHarmony = null;
            reachabilityUnpatchSelf = null;
            reloadPatchInstalled = false;
            FastAccessReloadRuntime.Reset();
        }

        void UnpatchReload()
        {
            UnpatchCandidateBridge();
            UnpatchReachability();
        }

        bool Fail(string message)
        {
            ReloadDiagnosticLog.TryWarning(logWarning, message);
            return false;
        }

        public void Dispose()
        {
            RestoreOwnedWrites();
            UnpatchReload();
            ClearState();
        }

        void ClearState()
        {
            installed = false;
            wroteFastAccessSlots = false;
            wroteBindAvailableSlots = false;
            reloadPatchInstalled = false;
            reloadCandidateBridgeInstalled = false;
            fastAccessSlotsField = null;
            bindAvailableSlotsField = null;
            originalFastAccessSlots = null;
            originalBindAvailableSlots = null;
            installedFastAccessSlots = null;
            installedBindAvailableSlots = null;
            reachabilityHarmony = null;
            reachabilityUnpatchSelf = null;
            candidateBridgeHarmony = null;
            candidateBridgeUnpatchSelf = null;
        }

        static Exception Unwrap(Exception exception)
        {
            while (exception is TargetInvocationException invocation && invocation.InnerException != null)
                exception = invocation.InnerException;
            return exception;
        }
    }
}