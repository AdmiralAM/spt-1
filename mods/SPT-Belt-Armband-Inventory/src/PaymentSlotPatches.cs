using System;
using System.Collections;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class PaymentSlotPolicy
    {
        internal static bool ShouldIncludeBelt(bool hasItem, bool hasContainers)
        {
            return hasItem && hasContainers;
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
                bool include = PaymentSlotPolicy.ShouldIncludeBelt(item != null, ReflectionTools.HasContainers(item));
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
                if (LogWarning != null) LogWarning("Could not normalize payment slots for belt: " + Unwrap(exception).Message);
            }
        }

        static int IndexOfReference(IList list, object target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (ReferenceEquals(list[i], target)) return i;
            }
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
                    return Fail("SPT 4.1 InventoryEquipment/EquipmentSlot or Harmony was not found; belt payment compatibility is disabled.");

                PropertyInfo property = equipmentType.GetProperty("PaymentSlots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo getter = property == null ? null : property.GetGetMethod(true);
                MethodInfo getSlot = equipmentType.GetMethod("GetSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { slotEnumType }, null);
                if (getter == null || getSlot == null)
                    return Fail("SPT 4.1 payment-slot inventory shape changed; belt payment compatibility is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmony == null || patchMethod == null || harmonyMethodConstructor == null)
                    return Fail("Harmony patch API is incompatible; belt payment compatibility is disabled.");

                PaymentSlotRuntime.LogWarning = logWarning;
                PaymentSlotRuntime.GetSlotMethod = getSlot;
                PaymentSlotRuntime.ArmBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);

                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(Postfix)) });
                Patch(patchMethod, harmonyMethodType, getter, postfix);

                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (logInfo != null) logInfo("Belt/Armband Inventory payment-slot compatibility installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Belt payment compatibility installation failed safely: " + exception.Message);
            }
        }

        static void Postfix(object __instance, object __result)
        {
            PaymentSlotRuntime.Normalize(__instance, __result);
        }

        static MethodInfo Method(string name)
        {
            return typeof(PaymentSlotPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
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
                {
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
                }
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
            }
            patchMethod.Invoke(harmony, arguments);
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