using System;
using System.Collections;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class GrenadeSlotPolicy
    {
        internal static bool ShouldIncludeBelt(bool hasItem, bool hasContainers)
        {
            return hasItem && hasContainers;
        }

        internal static bool ShouldAppendGrenade(bool isGrenade, bool examined, bool alreadyPresent)
        {
            return isGrenade && examined && !alreadyPresent;
        }
    }

    internal static class GrenadeSlotRuntime
    {
        internal static Action<string> LogWarning;
        internal static MethodInfo GetSlotMethod;
        internal static object ArmBandValue;
        internal static MethodInfo GetTopLevelItemsMethod;
        internal static MethodInfo ExaminedMethod;
        internal static Type GrenadeType;
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
                bool include = GrenadeSlotPolicy.ShouldIncludeBelt(item != null, ReflectionTools.HasContainers(item));
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
                if (LogWarning != null) LogWarning("Could not normalize grenade throwing slots for belt: " + Unwrap(exception).Message);
            }
        }

        internal static void AppendBeltGrenades(object inventoryController, object result)
        {
            if (inventoryController == null || result == null || GetSlotMethod == null || ArmBandValue == null || GetTopLevelItemsMethod == null || ExaminedMethod == null || GrenadeType == null) return;

            try
            {
                IList list = result as IList;
                if (list == null || list.IsReadOnly || list.IsFixedSize) return;

                object inventory = ReflectionTools.ReadMember(inventoryController, "Inventory");
                object equipment = ReflectionTools.ReadMember(inventory, "Equipment");
                if (equipment == null) return;

                object armBandSlot = GetSlotMethod.Invoke(equipment, new[] { ArmBandValue });
                object beltItem = ReflectionTools.ReadMember(armBandSlot, "ContainedItem");
                if (!ReflectionTools.HasContainers(beltItem)) return;

                IEnumerable items = GetTopLevelItemsMethod.Invoke(null, new[] { beltItem }) as IEnumerable;
                if (items == null) return;

                foreach (object item in items)
                {
                    bool isGrenade = item != null && GrenadeType.IsInstanceOfType(item);
                    bool examined = isGrenade && IsExamined(inventoryController, item);
                    bool existing = isGrenade && IndexOfReference(list, item) >= 0;
                    if (GrenadeSlotPolicy.ShouldAppendGrenade(isGrenade, examined, existing)) list.Add(item);
                }
            }
            catch (Exception exception)
            {
                if (LogWarning != null) LogWarning("Could not include belt grenades in fast-access enumeration: " + Unwrap(exception).Message);
            }
        }

        static bool IsExamined(object inventoryController, object item)
        {
            object value = ExaminedMethod.Invoke(inventoryController, new[] { item });
            return value is bool && (bool)value;
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
            GetTopLevelItemsMethod = null;
            ExaminedMethod = null;
            GrenadeType = null;
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
        }
    }

    internal sealed class GrenadeSlotPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.grenades";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal GrenadeSlotPatches(Action<string> logInfo, Action<string> logWarning)
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
                    return Fail("SPT 4.1 InventoryEquipment/EquipmentSlot or Harmony was not found; belt grenade fast-access compatibility is disabled.");

                PropertyInfo property = equipmentType.GetProperty("GrenadeThrowingSlots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo getter = property == null ? null : property.GetGetMethod(true);
                MethodInfo getSlot = equipmentType.GetMethod("GetSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { slotEnumType }, null);
                MethodInfo grenadeList = FindStaticMethod(equipmentType.Assembly, "GetThrowablePriorityGrenadesList");
                if (getter == null || getSlot == null || grenadeList == null || !grenadeList.ReturnType.IsGenericType)
                    return Fail("SPT 4.1 grenade inventory shape changed; belt grenade fast-access compatibility is disabled.");

                Type[] listArguments = grenadeList.ReturnType.GetGenericArguments();
                if (listArguments.Length != 1)
                    return Fail("SPT 4.1 grenade-list return type changed; belt grenade fast-access compatibility is disabled.");

                Type grenadeType = listArguments[0];
                Type controllerType = grenadeList.GetParameters()[0].ParameterType;
                MethodInfo topLevelItems = FindTopLevelItemsMethod(equipmentType.Assembly);
                MethodInfo examined = FindExaminedMethod(controllerType, grenadeType);
                if (topLevelItems == null || examined == null)
                    return Fail("SPT 4.1 grenade enumeration/examination shape changed; belt grenade enumeration is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; belt grenade fast-access compatibility is disabled.");
                unpatchSelf = rollback;

                GrenadeSlotRuntime.LogWarning = logWarning;
                GrenadeSlotRuntime.GetSlotMethod = getSlot;
                GrenadeSlotRuntime.ArmBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);
                GrenadeSlotRuntime.GetTopLevelItemsMethod = topLevelItems;
                GrenadeSlotRuntime.ExaminedMethod = examined;
                GrenadeSlotRuntime.GrenadeType = grenadeType;

                object slotPostfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(SlotPostfix)) });
                object listPostfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(GrenadeListPostfix)) });
                Patch(patchMethod, harmonyMethodType, getter, slotPostfix);
                Patch(patchMethod, harmonyMethodType, grenadeList, listPostfix);

                if (logInfo != null) logInfo("Belt/Armband Inventory grenade fast-access compatibility installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Belt grenade fast-access compatibility installation failed safely: " + exception.Message);
            }
        }

        static void SlotPostfix(object __instance, object __result)
        {
            GrenadeSlotRuntime.Normalize(__instance, __result);
        }

        static void GrenadeListPostfix(object[] __args, object __result)
        {
            if (__args == null || __args.Length == 0) return;
            GrenadeSlotRuntime.AppendBeltGrenades(__args[0], __result);
        }

        static MethodInfo FindStaticMethod(Assembly assembly, string name)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types; }

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null) continue;
                MethodInfo[] methods;
                try { methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
                catch { continue; }
                for (int p = 0; p < methods.Length; p++)
                {
                    MethodInfo method = methods[p];
                    if (!string.Equals(method.Name, name, StringComparison.Ordinal)) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1) continue;
                    PropertyInfo inventoryProperty = parameters[0].ParameterType.GetProperty("Inventory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (inventoryProperty != null) return method;
                }
            }
            return null;
        }

        static MethodInfo FindTopLevelItemsMethod(Assembly assembly)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException exception) { types = exception.Types; }

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null) continue;
                MethodInfo[] methods;
                try { methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
                catch { continue; }
                for (int p = 0; p < methods.Length; p++)
                {
                    MethodInfo method = methods[p];
                    if (!string.Equals(method.Name, "GetTopLevelItemsFromCollection", StringComparison.Ordinal)) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1 || !typeof(IEnumerable).IsAssignableFrom(method.ReturnType)) continue;
                    return method;
                }
            }
            return null;
        }

        static MethodInfo FindExaminedMethod(Type controllerType, Type grenadeType)
        {
            MethodInfo[] methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Examined", StringComparison.Ordinal) || method.ReturnType != typeof(bool)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(grenadeType)) return method;
            }
            return null;
        }

        static MethodInfo Method(string name)
        {
            return typeof(GrenadeSlotPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
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
            GrenadeSlotRuntime.Reset();
        }
    }
}