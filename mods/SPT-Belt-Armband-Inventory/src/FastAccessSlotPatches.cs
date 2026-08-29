using System;
using System.Collections;
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
    }

    internal static class FastAccessReloadRuntime
    {
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
                Exception root = exception;
                while (root is TargetInvocationException invocation && invocation.InnerException != null) root = invocation.InnerException;
                LogWarning?.Invoke("B&A&HB reload reachability failed closed: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        internal static void Reset()
        {
            MagazineType = null;
            GetAllParentItems = null;
            ReadTemplateId = null;
            LogWarning = null;
            failureLogged = false;
        }
    }

    internal sealed class FastAccessSlotPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.fast-access";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        FieldInfo fastAccessSlotsField;
        FieldInfo bindAvailableSlotsField;
        object originalFastAccessSlots;
        object originalBindAvailableSlots;
        object installedFastAccessSlots;
        object installedBindAvailableSlots;
        object harmony;
        MethodInfo unpatchSelf;
        bool wroteFastAccessSlots;
        bool wroteBindAvailableSlots;
        bool reloadPatchInstalled;
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

                if (TryInstallReloadReachability())
                    logInfo?.Invoke("B&A&HB fast-access installed: vanilla ArmBand/Belt arrays extended and exact Magazine Armband/Magazine Belt descendants are reload-reachable without changing vanilla reload ordering.");
                else
                    logWarning?.Invoke("B&A&HB fast-access slot arrays remain active, but exact reload reachability could not bind; wearable magazines remain reserve-only for this session.");
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
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (reachable == null || parentsMethod == null || templateIdMember == null
                    || harmonyMethodCtor == null || patchMethod == null || unpatchSelf == null)
                    return false;

                FastAccessReloadRuntime.MagazineType = magazineType;
                FastAccessReloadRuntime.GetAllParentItems = BuildParentEnumerator(parentsMethod, itemType);
                FastAccessReloadRuntime.ReadTemplateId = BuildStringReader(itemType, templateIdMember);
                FastAccessReloadRuntime.LogWarning = logWarning;
                if (FastAccessReloadRuntime.GetAllParentItems == null || FastAccessReloadRuntime.ReadTemplateId == null)
                    return false;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo postfixMethod = BuildReachabilityPostfix(itemType);
                object postfix = harmonyMethodCtor.Invoke(new object[] { postfixMethod });
                Patch(patchMethod, harmonyMethodType, reachable, postfix);
                reloadPatchInstalled = true;
                return true;
            }
            catch (Exception exception)
            {
                logWarning?.Invoke("B&A&HB reload reachability discovery failed closed: " + Unwrap(exception).Message);
                UnpatchReload();
                return false;
            }
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
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Patch", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                for (int p = 1; p < parameters.Length; p++)
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                        return method;
            }
            return null;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 0)
                    return methods[i];
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                    args[i] = postfix;
            patchMethod.Invoke(harmony, args);
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
                if (wroteBindAvailableSlots && bindAvailableSlotsField != null && originalBindAvailableSlots != null &&
                    FastAccessSlotPolicy.ShouldRestoreReference(bindAvailableSlotsField.GetValue(null), installedBindAvailableSlots))
                    bindAvailableSlotsField.SetValue(null, originalBindAvailableSlots);
            }
            catch { }

            try
            {
                if (wroteFastAccessSlots && fastAccessSlotsField != null && originalFastAccessSlots != null &&
                    FastAccessSlotPolicy.ShouldRestoreReference(fastAccessSlotsField.GetValue(null), installedFastAccessSlots))
                    fastAccessSlotsField.SetValue(null, originalFastAccessSlots);
            }
            catch { }
        }

        void UnpatchReload()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); }
            catch { }
            harmony = null;
            unpatchSelf = null;
            reloadPatchInstalled = false;
            FastAccessReloadRuntime.Reset();
        }

        bool Fail(string message) { logWarning?.Invoke(message); return false; }

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
            fastAccessSlotsField = null;
            bindAvailableSlotsField = null;
            originalFastAccessSlots = null;
            originalBindAvailableSlots = null;
            installedFastAccessSlots = null;
            installedBindAvailableSlots = null;
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
        }
    }
}
