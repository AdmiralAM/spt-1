using System;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class ScavBeltPolicy
    {
        internal static bool ShouldRestore(bool deleted, bool hasItem, bool hasContainers)
        {
            return deleted
                && AccessoryCapabilityPolicy.CanUse(
                    AccessoryCategory.ArmBand,
                    AccessoryCapability.ScavHostRestoration,
                    hasItem,
                    hasContainers);
        }
    }

    internal static class ScavBeltRuntime
    {
        internal static Action<string> LogWarning;

        internal static void RestoreContainerBeltSlot(object inventoryController)
        {
            if (inventoryController == null) return;

            try
            {
                object inventory = ReflectionTools.ReadMember(inventoryController, "Inventory");
                object equipment = ReflectionTools.ReadMember(inventory, "Equipment");
                if (equipment == null) return;

                MethodInfo getSlot = FindGetSlot(equipment.GetType());
                if (getSlot == null) return;

                Type slotEnumType = getSlot.GetParameters()[0].ParameterType;
                object armBand = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);
                object slot = getSlot.Invoke(equipment, new[] { armBand });
                if (slot == null) return;

                object item = ReflectionTools.ReadMember(slot, "ContainedItem");
                bool deleted = ReflectionTools.ReadBoolean(slot, "Deleted");
                if (!ScavBeltPolicy.ShouldRestore(deleted, item != null, ReflectionTools.HasContainers(item))) return;

                if (!WriteBoolean(slot, "Deleted", false))
                {
                    if (LogWarning != null) LogWarning("Scav container belt detected, but ArmBand.Deleted could not be restored.");
                }
            }
            catch (Exception exception)
            {
                if (LogWarning != null) LogWarning("Could not restore Scav container belt slot: " + Unwrap(exception).Message);
            }
        }

        static MethodInfo FindGetSlot(Type equipmentType)
        {
            MethodInfo[] methods = equipmentType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "GetSlot") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType.IsEnum) return method;
            }
            return null;
        }

        static bool WriteBoolean(object instance, string name, bool value)
        {
            if (instance == null) return false;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.CanWrite && property.PropertyType == typeof(bool))
            {
                property.SetValue(instance, value, null);
                return true;
            }

            FieldInfo field = type.GetField(name, flags);
            if (field != null && field.FieldType == typeof(bool))
            {
                field.SetValue(instance, value);
                return true;
            }
            return false;
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
        }
    }

    internal sealed class ScavBeltPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.scav";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal ScavBeltPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type controllerType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryController");
                if (harmonyType == null || harmonyMethodType == null || controllerType == null)
                    return Fail("SPT 4.1 InventoryController or Harmony was not found; Scav belt compatibility is disabled.");

                MethodInfo replaceInventory = FindReplaceInventory(controllerType);
                if (replaceInventory == null)
                    return Fail("SPT 4.1 InventoryController.ReplaceInventory boundary was not found; Scav belt compatibility is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; Scav belt compatibility is disabled.");
                unpatchSelf = rollback;

                ScavBeltRuntime.LogWarning = logWarning;
                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(ReplaceInventoryPostfix)) });
                Patch(patchMethod, harmonyMethodType, replaceInventory, postfix);

                if (logInfo != null) logInfo("Belt/Armband Inventory conditional Scav belt compatibility installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Scav belt compatibility installation failed safely: " + exception.Message);
            }
        }

        static MethodInfo FindReplaceInventory(Type controllerType)
        {
            MethodInfo[] methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "ReplaceInventory", StringComparison.Ordinal)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1) continue;
                Type parameterType = parameters[0].ParameterType;
                if (string.Equals(parameterType.Name, "Inventory", StringComparison.Ordinal)
                    || string.Equals(parameterType.FullName, "EFT.InventoryLogic.Inventory", StringComparison.Ordinal))
                    return method;
            }
            return null;
        }

        static void ReplaceInventoryPostfix(object __instance)
        {
            ScavBeltRuntime.RestoreContainerBeltSlot(__instance);
        }

        static MethodInfo Method(string name)
        {
            return typeof(ScavBeltPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
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
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                        return method;
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
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase))
                    arguments[i] = postfix;
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
            ScavBeltRuntime.LogWarning = null;
        }
    }
}
