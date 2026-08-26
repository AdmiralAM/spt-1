using System;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class PickupSlotPolicy
    {
        internal static bool ShouldTry(bool vanillaMissing, bool exactRuntimeCandidate, bool slotEmpty, bool slotDeleted, bool compatible)
        {
            return vanillaMissing
                && exactRuntimeCandidate
                && slotEmpty
                && !slotDeleted
                && compatible
                && AccessoryCapabilityPolicy.CanUse(
                    AccessoryCategory.ArmBand,
                    AccessoryCapability.PickupFallback,
                    true,
                    true);
        }

        // Compatibility shim for the pre-P2 regression harness. Production runtime
        // calls the five-argument exact-RC policy above.
        internal static bool ShouldTry(bool vanillaMissing, bool hasContainers, bool slotDeleted, bool compatible)
        {
            return ShouldTry(vanillaMissing, hasContainers, true, slotDeleted, compatible);
        }
    }

    internal static class PickupSlotRuntime
    {
        internal static Action<string> LogWarning;
        internal static object ArmBandValue;
        internal static Func<object, string> ReadTemplateId;
        internal static Func<object, object, object> GetSlot;
        internal static Func<object, bool> ReadSlotDeleted;
        internal static Func<object, object> ReadContainedItem;
        internal static Func<object, object, bool> CheckCompatibility;
        internal static Func<object, object, object> FindLocationForItem;
        static bool runtimeFailureLogged;

        internal static object Resolve(object current, object equipment, object item)
        {
            if (current != null || equipment == null || item == null) return current;
            if (ArmBandValue == null || ReadTemplateId == null || GetSlot == null || ReadSlotDeleted == null
                || ReadContainedItem == null || CheckCompatibility == null || FindLocationForItem == null) return current;

            try
            {
                bool exactRuntimeCandidate = string.Equals(ReadTemplateId(item), RuntimeIdentity.CandidateItemId, StringComparison.Ordinal);
                if (!exactRuntimeCandidate) return current;

                object slot = GetSlot(equipment, ArmBandValue);
                if (slot == null) return current;

                bool slotDeleted = ReadSlotDeleted(slot);
                bool slotEmpty = ReadContainedItem(slot) == null;
                bool compatible = slotEmpty && !slotDeleted && CheckCompatibility(slot, item);
                if (!PickupSlotPolicy.ShouldTry(true, exactRuntimeCandidate, slotEmpty, slotDeleted, compatible)) return current;

                object address = FindLocationForItem(slot, item);
                return address ?? current;
            }
            catch (Exception exception)
            {
                if (!runtimeFailureLogged)
                {
                    runtimeFailureLogged = true;
                    Exception root = Unwrap(exception);
                    LogWarning?.Invoke("B&A&HB ALT-PICKUP RUNTIME FAIL-CLOSED: " + root.GetType().FullName + ": " + root.Message
                        + (string.IsNullOrEmpty(root.StackTrace) ? "" : "\n" + root.StackTrace));
                }
                return current;
            }
        }

        internal static void Reset()
        {
            LogWarning = null;
            ArmBandValue = null;
            ReadTemplateId = null;
            GetSlot = null;
            ReadSlotDeleted = null;
            ReadContainedItem = null;
            CheckCompatibility = null;
            FindLocationForItem = null;
            runtimeFailureLogged = false;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }
    }

    internal sealed class PickupSlotPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.pickup";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal PickupSlotPatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall()
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type equipmentType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type slotType = ReflectionTools.FindType("EFT.InventoryLogic.Slot");
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                if (harmonyType == null || harmonyMethodType == null || equipmentType == null || equipmentSlotType == null || slotType == null || itemType == null)
                    return Fail("SPT 4.1 pickup types or Harmony were not found; automatic belt pickup is disabled.");

                MethodInfo target = FindTarget(equipmentType, itemType);
                MethodInfo getSlot = ReflectionTools.FindInstanceMethod(equipmentType, "GetSlot", slotType, equipmentSlotType);
                MethodInfo checkCompatibility = FindExactInstanceMethod(slotType, "CheckCompatibility", typeof(bool), itemType);
                MethodInfo findLocation = FindLocationForItem(slotType, itemType);
                MemberInfo deletedMember = FindReadableMember(slotType, "Deleted", typeof(bool));
                MemberInfo containedItemMember = FindReadableMember(slotType, "ContainedItem", itemType);
                MemberInfo templateIdMember = FindTemplateIdMember(itemType);

                if (target == null || target.ReturnType.IsValueType)
                    return Fail("SPT 4.1 FindSlotToPickUp(InventoryEquipment, Item) shape changed; automatic belt pickup is disabled.");
                if (getSlot == null || checkCompatibility == null || findLocation == null || deletedMember == null || containedItemMember == null || templateIdMember == null)
                    return Fail("SPT 4.1 exact Alt-pickup boundary is incomplete; automatic belt pickup is disabled."
                        + " getSlot=" + Describe(getSlot)
                        + ", compatibility=" + Describe(checkCompatibility)
                        + ", findLocation=" + Describe(findLocation)
                        + ", deleted=" + Describe(deletedMember)
                        + ", containedItem=" + Describe(containedItemMember)
                        + ", templateId=" + Describe(templateIdMember) + ".");

                object armBandValue;
                try { armBandValue = Enum.Parse(equipmentSlotType, BeltSlotPlan.ArmBand, false); }
                catch (Exception exception) { return Fail("SPT 4.1 EquipmentSlot.ArmBand was not found; automatic belt pickup is disabled: " + Unwrap(exception).Message); }

                Func<object, string> readTemplateId = BuildStringReader(itemType, templateIdMember);
                Func<object, object, object> getSlotDelegate = BuildBinaryObjectCall(equipmentType, equipmentSlotType, getSlot);
                Func<object, bool> readDeleted = BuildBoolReader(slotType, deletedMember);
                Func<object, object> readContained = BuildObjectReader(slotType, containedItemMember);
                Func<object, object, bool> checkCompatibilityDelegate = BuildBinaryBoolCall(slotType, itemType, checkCompatibility);
                Func<object, object, object> findLocationDelegate = BuildFindLocationCall(slotType, itemType, findLocation);
                if (readTemplateId == null || getSlotDelegate == null || readDeleted == null || readContained == null || checkCompatibilityDelegate == null || findLocationDelegate == null)
                    return Fail("SPT 4.1 Alt-pickup delegates could not be bound; automatic belt pickup is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, hmCtor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; automatic belt pickup is disabled.");
                unpatchSelf = rollback;

                PickupSlotRuntime.LogWarning = logWarning;
                PickupSlotRuntime.ArmBandValue = armBandValue;
                PickupSlotRuntime.ReadTemplateId = readTemplateId;
                PickupSlotRuntime.GetSlot = getSlotDelegate;
                PickupSlotRuntime.ReadSlotDeleted = readDeleted;
                PickupSlotRuntime.ReadContainedItem = readContained;
                PickupSlotRuntime.CheckCompatibility = checkCompatibilityDelegate;
                PickupSlotRuntime.FindLocationForItem = findLocationDelegate;

                object postfix = hmCtor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, target, postfix);
                logInfo?.Invoke("B&A&HB Alt-pickup installed with startup-bound exact RC/ArmBand delegates and Slot.FindLocationForItem(Item, out error).");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Automatic belt pickup compatibility installation failed safely: " + root.GetType().FullName + ": " + root.Message
                    + (string.IsNullOrEmpty(root.StackTrace) ? "" : "\n" + root.StackTrace));
            }
        }

        static MethodInfo FindTarget(Type equipmentType, Type itemType)
        {
            Type[] types = ReflectionTools.GetTypes(equipmentType.Assembly);
            for (int t = 0; t < types.Length; t++)
            {
                Type type = types[t];
                if (type == null) continue;
                MethodInfo[] methods;
                try { methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { continue; }
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "FindSlotToPickUp", StringComparison.Ordinal)) continue;
                    ParameterInfo[] p = method.GetParameters();
                    if (p.Length == 2 && p[0].ParameterType == equipmentType && p[1].ParameterType == itemType) return method;
                }
            }
            return null;
        }

        static MethodInfo FindExactInstanceMethod(Type type, string name, Type returnType, params Type[] parameters)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                MethodInfo[] methods;
                try { methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { continue; }
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, name, StringComparison.Ordinal) || method.ReturnType != returnType) continue;
                    ParameterInfo[] actual = method.GetParameters();
                    if (actual.Length != parameters.Length) continue;
                    bool match = true;
                    for (int p = 0; p < actual.Length; p++) if (actual[p].ParameterType != parameters[p]) { match = false; break; }
                    if (match) return method;
                }
            }
            return null;
        }

        static MethodInfo FindLocationForItem(Type slotType, Type itemType)
        {
            for (Type current = slotType; current != null; current = current.BaseType)
            {
                MethodInfo[] methods;
                try { methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { continue; }
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo method = methods[i];
                    if (!string.Equals(method.Name, "FindLocationForItem", StringComparison.Ordinal) || method.ReturnType == typeof(void)) continue;
                    ParameterInfo[] p = method.GetParameters();
                    if (p.Length != 2 || p[0].ParameterType != itemType) continue;
                    if (!p[1].ParameterType.IsByRef) continue;
                    return method;
                }
            }
            return null;
        }

        static MemberInfo FindReadableMember(Type type, string name, Type expectedType)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo[] properties;
                try { properties = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { properties = Array.Empty<PropertyInfo>(); }
                for (int i = 0; i < properties.Length; i++)
                    if (string.Equals(properties[i].Name, name, StringComparison.Ordinal) && properties[i].PropertyType == expectedType && properties[i].GetGetMethod(true) != null && properties[i].GetIndexParameters().Length == 0) return properties[i];

                FieldInfo[] fields;
                try { fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { fields = Array.Empty<FieldInfo>(); }
                for (int i = 0; i < fields.Length; i++)
                    if (string.Equals(fields[i].Name, name, StringComparison.Ordinal) && fields[i].FieldType == expectedType) return fields[i];
            }
            return null;
        }

        static MemberInfo FindTemplateIdMember(Type itemType)
        {
            MemberInfo stringId = FindReadableMember(itemType, "StringTemplateId", typeof(string));
            if (stringId != null) return stringId;
            for (Type current = itemType; current != null; current = current.BaseType)
            {
                PropertyInfo[] properties;
                try { properties = current.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { properties = Array.Empty<PropertyInfo>(); }
                for (int i = 0; i < properties.Length; i++)
                    if (string.Equals(properties[i].Name, "TemplateId", StringComparison.Ordinal) && properties[i].GetGetMethod(true) != null && properties[i].GetIndexParameters().Length == 0) return properties[i];
                FieldInfo[] fields;
                try { fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { fields = Array.Empty<FieldInfo>(); }
                for (int i = 0; i < fields.Length; i++) if (string.Equals(fields[i].Name, "TemplateId", StringComparison.Ordinal)) return fields[i];
            }
            return null;
        }

        static Func<object, string> BuildStringReader(Type ownerType, MemberInfo member)
        {
            Type valueType = MemberType(member);
            DynamicMethod dm = new DynamicMethod("BAndHB_ReadTemplateId", typeof(string), new[] { typeof(object) }, typeof(PickupSlotPatches), true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, ownerType);
            EmitReadMember(il, member);
            if (valueType != typeof(string))
            {
                if (valueType.IsValueType) il.Emit(OpCodes.Box, valueType);
                il.Emit(OpCodes.Callvirt, typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes));
            }
            il.Emit(OpCodes.Ret);
            return (Func<object, string>)dm.CreateDelegate(typeof(Func<object, string>));
        }

        static Func<object, bool> BuildBoolReader(Type ownerType, MemberInfo member)
        {
            DynamicMethod dm = new DynamicMethod("BAndHB_ReadSlotDeleted", typeof(bool), new[] { typeof(object) }, typeof(PickupSlotPatches), true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, ownerType);
            EmitReadMember(il, member);
            il.Emit(OpCodes.Ret);
            return (Func<object, bool>)dm.CreateDelegate(typeof(Func<object, bool>));
        }

        static Func<object, object> BuildObjectReader(Type ownerType, MemberInfo member)
        {
            Type valueType = MemberType(member);
            DynamicMethod dm = new DynamicMethod("BAndHB_ReadContainedItem", typeof(object), new[] { typeof(object) }, typeof(PickupSlotPatches), true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Castclass, ownerType);
            EmitReadMember(il, member);
            if (valueType.IsValueType) il.Emit(OpCodes.Box, valueType);
            il.Emit(OpCodes.Ret);
            return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
        }

        static Func<object, object, object> BuildBinaryObjectCall(Type ownerType, Type argumentType, MethodInfo method)
        {
            DynamicMethod dm = new DynamicMethod("BAndHB_GetArmBandSlot", typeof(object), new[] { typeof(object), typeof(object) }, typeof(PickupSlotPatches), true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, ownerType);
            il.Emit(OpCodes.Ldarg_1);
            if (argumentType.IsValueType) il.Emit(OpCodes.Unbox_Any, argumentType); else il.Emit(OpCodes.Castclass, argumentType);
            il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            if (method.ReturnType.IsValueType) il.Emit(OpCodes.Box, method.ReturnType);
            il.Emit(OpCodes.Ret);
            return (Func<object, object, object>)dm.CreateDelegate(typeof(Func<object, object, object>));
        }

        static Func<object, object, bool> BuildBinaryBoolCall(Type ownerType, Type argumentType, MethodInfo method)
        {
            DynamicMethod dm = new DynamicMethod("BAndHB_CheckArmBandCompatibility", typeof(bool), new[] { typeof(object), typeof(object) }, typeof(PickupSlotPatches), true);
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, ownerType);
            il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Castclass, argumentType);
            il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            il.Emit(OpCodes.Ret);
            return (Func<object, object, bool>)dm.CreateDelegate(typeof(Func<object, object, bool>));
        }

        static Func<object, object, object> BuildFindLocationCall(Type slotType, Type itemType, MethodInfo method)
        {
            ParameterInfo[] parameters = method.GetParameters();
            Type errorType = parameters[1].ParameterType.GetElementType();
            DynamicMethod dm = new DynamicMethod("BAndHB_FindArmBandLocation", typeof(object), new[] { typeof(object), typeof(object) }, typeof(PickupSlotPatches), true);
            ILGenerator il = dm.GetILGenerator();
            LocalBuilder error = il.DeclareLocal(errorType);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, slotType);
            il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Castclass, itemType);
            il.Emit(OpCodes.Ldloca_S, error);
            il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
            if (method.ReturnType.IsValueType) il.Emit(OpCodes.Box, method.ReturnType);
            il.Emit(OpCodes.Ret);
            return (Func<object, object, object>)dm.CreateDelegate(typeof(Func<object, object, object>));
        }

        static void EmitReadMember(ILGenerator il, MemberInfo member)
        {
            if (member is PropertyInfo property) il.Emit(property.GetGetMethod(true).IsVirtual ? OpCodes.Callvirt : OpCodes.Call, property.GetGetMethod(true));
            else il.Emit(OpCodes.Ldfld, (FieldInfo)member);
        }

        static Type MemberType(MemberInfo member) => member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;
        static string Describe(MemberInfo member) => member == null ? "<missing>" : member.DeclaringType?.FullName + "." + member.Name;

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo originalMethod = original as MethodInfo;
            if (originalMethod == null || originalMethod.ReturnType == typeof(void) || originalMethod.ReturnType.IsValueType) return null;
            ParameterInfo[] p = originalMethod.GetParameters();
            if (p.Length != 2) return null;
            return BuildPostfix(originalMethod.ReturnType, p[0].ParameterType, p[1].ParameterType);
        }

        internal static MethodInfo BuildPostfix(Type resultType, Type equipmentType, Type itemType)
        {
            DynamicMethod method = new DynamicMethod("BeltPickupPostfix", typeof(void), new[] { resultType.MakeByRefType(), equipmentType, itemType }, typeof(PickupSlotPatches), true);
            method.DefineParameter(1, ParameterAttributes.None, "__result");
            method.DefineParameter(2, ParameterAttributes.None, "__0");
            method.DefineParameter(3, ParameterAttributes.None, "__1");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            if (equipmentType.IsValueType) il.Emit(OpCodes.Box, equipmentType);
            il.Emit(OpCodes.Ldarg_2);
            if (itemType.IsValueType) il.Emit(OpCodes.Box, itemType);
            il.Emit(OpCodes.Call, typeof(PickupSlotRuntime).GetMethod(nameof(PickupSlotRuntime.Resolve), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Castclass, resultType);
            il.Emit(OpCodes.Stind_Ref);
            il.Emit(OpCodes.Ret);
            return method;
        }

        // Compatibility-only shape for the legacy regression in Program.cs. The
        // Harmony factory above never calls this overload.
        internal static MethodInfo BuildPostfix(Type resultType)
        {
            DynamicMethod method = new DynamicMethod("LegacyBeltPickupPostfixContract", typeof(void), new[] { resultType.MakeByRefType(), typeof(object[]) }, typeof(PickupSlotPatches), true);
            method.DefineParameter(1, ParameterAttributes.None, "__result");
            method.DefineParameter(2, ParameterAttributes.None, "__args");
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ret);
            return method;
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(PickupSlotPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++) if (string.Equals(methods[i].Name, name, StringComparison.Ordinal)) return methods[i];
            return null;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++) if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 0) return methods[i];
            return null;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int m = 0; m < methods.Length; m++)
            {
                MethodInfo method = methods[m];
                if (method.Name != "Patch") continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length < 2 || !typeof(MethodBase).IsAssignableFrom(p[0].ParameterType)) continue;
                for (int i = 1; i < p.Length; i++) if (p[i].ParameterType == harmonyMethodType && string.Equals(p[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] p = patchMethod.GetParameters();
            object[] args = new object[p.Length];
            args[0] = original;
            for (int i = 1; i < p.Length; i++) if (p[i].ParameterType == harmonyMethodType && string.Equals(p[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) args[i] = postfix;
            patchMethod.Invoke(harmony, args);
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        bool Fail(string message) { logWarning?.Invoke(message); return false; }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            PickupSlotRuntime.Reset();
        }
    }
}
