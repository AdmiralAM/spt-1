using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class PaymentSlotPolicy
    {
        internal static bool ShouldIncludeWearable(string templateId, bool hasContainers)
        {
            return hasContainers
                && WearableItemDescriptorRegistry.HasCapability(templateId, AccessoryCapability.PaymentSource);
        }

        internal static bool ShouldIncludeBelt(bool hasItem, bool hasContainers)
        {
            return hasItem && ShouldIncludeWearable(RuntimeIdentity.CandidateItemId, hasContainers);
        }
    }

    internal static class PaymentSlotRuntime
    {
        internal static Action<string> LogWarning;
        internal static Func<object, object, object> GetSlot;
        internal static Func<object, object> ReadContainedItem;
        internal static Func<object, string> ReadTemplateId;
        internal static object ArmBandValue;
        static readonly RuntimeListOwnership Ownership = new RuntimeListOwnership();
        static bool runtimeFailureLogged;

        internal static void Normalize(object equipment, object result)
        {
            if (equipment == null || result == null || GetSlot == null || ReadContainedItem == null || ReadTemplateId == null || ArmBandValue == null) return;

            try
            {
                IList list = result as IList;
                if (list == null || list.IsReadOnly || list.IsFixedSize) return;

                object armBandSlot = GetSlot(equipment, ArmBandValue);
                if (armBandSlot == null) return;

                object item = ReadContainedItem(armBandSlot);
                string templateId = item == null ? null : ReadTemplateId(item);
                bool include = PaymentSlotPolicy.ShouldIncludeWearable(templateId, item != null && ReflectionTools.HasContainers(item));
                int existing = IndexOfReference(list, armBandSlot);
                bool owned = Ownership.Owns(equipment, list, armBandSlot);

                if (include && existing < 0)
                {
                    list.Add(armBandSlot);
                    Ownership.Mark(equipment, list, armBandSlot);
                }
                else if (include && existing >= 0 && !owned)
                {
                    Ownership.Forget(equipment);
                }
                else if (!include && existing >= 0 && owned)
                {
                    list.RemoveAt(existing);
                    Ownership.Forget(equipment);
                }
                else if (!include)
                {
                    Ownership.Forget(equipment);
                }
            }
            catch (Exception exception)
            {
                if (!runtimeFailureLogged)
                {
                    runtimeFailureLogged = true;
                    Exception root = Unwrap(exception);
                    LogWarning?.Invoke("B&A&HB PAYMENT RUNTIME FAIL-CLOSED: " + root.GetType().FullName + ": " + root.Message);
                }
            }
        }

        static int IndexOfReference(IList list, object target)
        {
            for (int i = 0; i < list.Count; i++)
                if (ReferenceEquals(list[i], target)) return i;
            return -1;
        }

        internal static void Reset()
        {
            Ownership.Reset();
            LogWarning = null;
            GetSlot = null;
            ReadContainedItem = null;
            ReadTemplateId = null;
            ArmBandValue = null;
            runtimeFailureLogged = false;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }
    }

    internal sealed class PaymentSlotPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.payments";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal PaymentSlotPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type slotEnumType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type slotType = ReflectionTools.FindType("EFT.InventoryLogic.Slot");
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                if (harmonyType == null || harmonyMethodType == null || equipmentType == null || slotEnumType == null || slotType == null || itemType == null)
                    return Fail("SPT 4.1 payment types or Harmony were not found; wearable payment compatibility is disabled.");

                PropertyInfo property = ReflectionTools.FindInstanceProperty(equipmentType, "PaymentSlots");
                MethodInfo getter = property == null ? null : property.GetGetMethod(true);
                MethodInfo getSlot = ReflectionTools.FindInstanceMethod(equipmentType, "GetSlot", slotType, slotEnumType);
                PropertyInfo containedItem = ReflectionTools.FindInstanceProperty(slotType, "ContainedItem", itemType);
                PropertyInfo stringTemplateId = ReflectionTools.FindInstanceProperty(itemType, "StringTemplateId", typeof(string));
                if (getter == null || getSlot == null || containedItem == null || stringTemplateId == null)
                    return Fail("SPT 4.1 payment-slot inventory shape changed; wearable payment compatibility is disabled.");

                Func<object, object, object> getSlotDelegate = BuildBinaryObjectCall(equipmentType, slotEnumType, getSlot);
                Func<object, object> containedItemReader = BuildObjectReader(slotType, containedItem);
                Func<object, string> templateIdReader = BuildStringReader(itemType, stringTemplateId);
                if (getSlotDelegate == null || containedItemReader == null || templateIdReader == null)
                    return Fail("SPT 4.1 payment delegates could not be bound; wearable payment compatibility is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; wearable payment compatibility is disabled.");
                unpatchSelf = rollback;

                PaymentSlotRuntime.LogWarning = logWarning;
                PaymentSlotRuntime.GetSlot = getSlotDelegate;
                PaymentSlotRuntime.ReadContainedItem = containedItemReader;
                PaymentSlotRuntime.ReadTemplateId = templateIdReader;
                PaymentSlotRuntime.ArmBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);

                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(Postfix)) });
                Patch(patchMethod, harmonyMethodType, getter, postfix);

                logInfo?.Invoke("B&A&HB wearable payment-source compatibility installed with startup-bound item-descriptor delegates.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Wearable payment compatibility installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static Func<object, object, object> BuildBinaryObjectCall(Type declaringType, Type argumentType, MethodInfo method)
        {
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBPaymentGetSlot", typeof(object), new[] { typeof(object), typeof(object) }, typeof(PaymentSlotPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                il.Emit(OpCodes.Ldarg_1);
                if (argumentType.IsValueType) il.Emit(OpCodes.Unbox_Any, argumentType);
                else il.Emit(OpCodes.Castclass, argumentType);
                il.Emit(method.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, method);
                if (method.ReturnType.IsValueType) il.Emit(OpCodes.Box, method.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object, object, object>)dm.CreateDelegate(typeof(Func<object, object, object>));
            }
            catch { return null; }
        }

        static Func<object, object> BuildObjectReader(Type declaringType, PropertyInfo property)
        {
            MethodInfo getter = property?.GetGetMethod(true);
            if (getter == null) return null;
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBPaymentContainedItem", typeof(object), new[] { typeof(object) }, typeof(PaymentSlotPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
                if (getter.ReturnType.IsValueType) il.Emit(OpCodes.Box, getter.ReturnType);
                il.Emit(OpCodes.Ret);
                return (Func<object, object>)dm.CreateDelegate(typeof(Func<object, object>));
            }
            catch { return null; }
        }

        static Func<object, string> BuildStringReader(Type declaringType, PropertyInfo property)
        {
            MethodInfo getter = property?.GetGetMethod(true);
            if (getter == null || getter.ReturnType != typeof(string)) return null;
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBPaymentTemplateId", typeof(string), new[] { typeof(object) }, typeof(PaymentSlotPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
                il.Emit(OpCodes.Ret);
                return (Func<object, string>)dm.CreateDelegate(typeof(Func<object, string>));
            }
            catch { return null; }
        }

        static void Postfix(object __instance, object __result)
        {
            PaymentSlotRuntime.Normalize(__instance, __result);
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(PaymentSlotPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal)) return methods[i];
            return null;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 0) return methods[i];
            return null;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                for (int p = 1; p < parameters.Length; p++)
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
            patchMethod.Invoke(harmony, arguments);
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        bool Fail(string message)
        {
            logWarning?.Invoke(message);
            return false;
        }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); }
            catch { }
            harmony = null;
            unpatchSelf = null;
            PaymentSlotRuntime.Reset();
        }
    }
}
