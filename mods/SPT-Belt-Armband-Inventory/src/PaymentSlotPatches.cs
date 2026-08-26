using System;
using System.Collections;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class PaymentSlotPolicy
    {
        internal static bool ShouldIncludeWearable(string templateId, bool hasContainers)
        {
            return hasContainers
                && WearableItemDescriptorRegistry.HasCapability(templateId, AccessoryCapability.PaymentSource);
        }
    }

    internal static class PaymentSlotRuntime
    {
        internal static Action<string> LogWarning;
        internal static MethodInfo GetSlotMethod;
        internal static object ArmBandValue;
        static readonly RuntimeListOwnership Ownership = new RuntimeListOwnership();

        internal static void Normalize(object equipment, object result)
        {
            if (equipment == null || result == null || GetSlotMethod == null || ArmBandValue == null) return;

            try
            {
                IList list = result as IList;
                if (list == null || list.IsReadOnly || list.IsFixedSize) return;

                object armBandSlot = GetSlotMethod.Invoke(equipment, new[] { ArmBandValue });
                if (armBandSlot == null) return;

                object item = ReflectionTools.ReadMember(armBandSlot, "ContainedItem");
                string templateId = GetTemplateId(item);
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
                if (LogWarning != null) LogWarning("Could not normalize payment slots for wearable: " + Unwrap(exception).Message);
            }
        }

        static string GetTemplateId(object item)
        {
            if (item == null) return null;
            object stringTemplateId = ReflectionTools.ReadMember(item, "StringTemplateId");
            if (stringTemplateId is string direct && !string.IsNullOrEmpty(direct)) return direct;
            object templateId = ReflectionTools.ReadMember(item, "TemplateId");
            return templateId?.ToString();
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
            GetSlotMethod = null;
            ArmBandValue = null;
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
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
                if (harmonyType == null || harmonyMethodType == null || equipmentType == null || slotEnumType == null)
                    return Fail("SPT 4.1 InventoryEquipment/EquipmentSlot or Harmony was not found; wearable payment compatibility is disabled.");

                PropertyInfo property = ReflectionTools.FindInstanceProperty(equipmentType, "PaymentSlots");
                MethodInfo getter = property == null ? null : property.GetGetMethod(true);
                MethodInfo getSlot = ReflectionTools.FindInstanceMethod(equipmentType, "GetSlot", null, slotEnumType);
                if (getter == null || getSlot == null)
                    return Fail("SPT 4.1 payment-slot inventory shape changed; wearable payment compatibility is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; wearable payment compatibility is disabled.");
                unpatchSelf = rollback;

                PaymentSlotRuntime.LogWarning = logWarning;
                PaymentSlotRuntime.GetSlotMethod = getSlot;
                PaymentSlotRuntime.ArmBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);

                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(Postfix)) });
                Patch(patchMethod, harmonyMethodType, getter, postfix);

                if (logInfo != null) logInfo("B&A&HB wearable payment-source compatibility installed (item-descriptor scoped).");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Wearable payment compatibility installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
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
            if (logWarning != null) logWarning(message);
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
