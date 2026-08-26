using System;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class ScavBeltPolicy
    {
        internal static bool ShouldRestore(string templateId, bool deleted, bool hasContainers)
        {
            return deleted
                && hasContainers
                && WearableItemDescriptorRegistry.HasCapability(templateId, AccessoryCapability.ScavHostRestoration);
        }

        internal static bool ShouldRestore(bool deleted, bool hasItem, bool hasContainers)
        {
            return hasItem && ShouldRestore(RuntimeIdentity.CandidateItemId, deleted, hasContainers);
        }
    }

    internal static class ScavBeltRuntime
    {
        internal static Action<string> LogWarning;
        internal static Func<object, object> ReadInventory;
        internal static Func<object, object> ReadEquipment;
        internal static Func<object, object, object> GetSlot;
        internal static Func<object, object> ReadContainedItem;
        internal static Func<object, bool> ReadDeleted;
        internal static Action<object, bool> WriteDeleted;
        internal static Func<object, string> ReadTemplateId;
        internal static object ArmBandValue;
        static bool runtimeFailureLogged;

        internal static void RestoreContainerBeltSlot(object inventoryController)
        {
            if (inventoryController == null || ReadInventory == null || ReadEquipment == null || GetSlot == null
                || ReadContainedItem == null || ReadDeleted == null || WriteDeleted == null || ReadTemplateId == null || ArmBandValue == null) return;

            try
            {
                object inventory = ReadInventory(inventoryController);
                object equipment = inventory == null ? null : ReadEquipment(inventory);
                if (equipment == null) return;

                object slot = GetSlot(equipment, ArmBandValue);
                if (slot == null) return;

                object item = ReadContainedItem(slot);
                bool deleted = ReadDeleted(slot);
                string templateId = item == null ? null : ReadTemplateId(item);
                if (!ScavBeltPolicy.ShouldRestore(templateId, deleted, ReflectionTools.HasContainers(item))) return;

                WriteDeleted(slot, false);
            }
            catch (Exception exception)
            {
                if (!runtimeFailureLogged)
                {
                    runtimeFailureLogged = true;
                    Exception root = Unwrap(exception);
                    LogWarning?.Invoke("B&A&HB SCAV RUNTIME FAIL-CLOSED: " + root.GetType().FullName + ": " + root.Message);
                }
            }
        }

        internal static void Reset()
        {
            LogWarning = null;
            ReadInventory = null;
            ReadEquipment = null;
            GetSlot = null;
            ReadContainedItem = null;
            ReadDeleted = null;
            WriteDeleted = null;
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
                Type inventoryType = ReflectionTools.FindType("EFT.InventoryLogic.Inventory");
                Type equipmentType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type slotType = ReflectionTools.FindType("EFT.InventoryLogic.Slot");
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                if (harmonyType == null || harmonyMethodType == null || controllerType == null || inventoryType == null
                    || equipmentType == null || equipmentSlotType == null || slotType == null || itemType == null)
                    return Fail("SPT 4.1 Scav inventory types or Harmony were not found; Scav wearable compatibility is disabled.");

                MethodInfo replaceInventory = FindReplaceInventory(controllerType, inventoryType);
                PropertyInfo inventoryProperty = ReflectionTools.FindInstanceProperty(controllerType, "Inventory", inventoryType);
                PropertyInfo equipmentProperty = ReflectionTools.FindInstanceProperty(inventoryType, "Equipment", equipmentType);
                MethodInfo getSlot = ReflectionTools.FindInstanceMethod(equipmentType, "GetSlot", slotType, equipmentSlotType);
                PropertyInfo containedItem = ReflectionTools.FindInstanceProperty(slotType, "ContainedItem", itemType);
                PropertyInfo deleted = ReflectionTools.FindInstanceProperty(slotType, "Deleted", typeof(bool));
                PropertyInfo templateId = ReflectionTools.FindInstanceProperty(itemType, "StringTemplateId", typeof(string));
                if (replaceInventory == null || inventoryProperty == null || equipmentProperty == null || getSlot == null
                    || containedItem == null || deleted == null || !deleted.CanWrite || templateId == null)
                    return Fail("SPT 4.1 Scav wearable boundary is incomplete; compatibility is disabled.");

                Func<object, object> readInventory = BuildObjectReader(controllerType, inventoryProperty, "BAndHBScavInventory");
                Func<object, object> readEquipment = BuildObjectReader(inventoryType, equipmentProperty, "BAndHBScavEquipment");
                Func<object, object, object> getSlotDelegate = BuildBinaryObjectCall(equipmentType, equipmentSlotType, getSlot);
                Func<object, object> readContainedItem = BuildObjectReader(slotType, containedItem, "BAndHBScavContainedItem");
                Func<object, bool> readDeleted = BuildBoolReader(slotType, deleted);
                Action<object, bool> writeDeleted = BuildBoolWriter(slotType, deleted);
                Func<object, string> readTemplateId = BuildStringReader(itemType, templateId);
                if (readInventory == null || readEquipment == null || getSlotDelegate == null || readContainedItem == null
                    || readDeleted == null || writeDeleted == null || readTemplateId == null)
                    return Fail("SPT 4.1 Scav wearable delegates could not be bound; compatibility is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; Scav wearable compatibility is disabled.");
                unpatchSelf = rollback;

                ScavBeltRuntime.LogWarning = logWarning;
                ScavBeltRuntime.ReadInventory = readInventory;
                ScavBeltRuntime.ReadEquipment = readEquipment;
                ScavBeltRuntime.GetSlot = getSlotDelegate;
                ScavBeltRuntime.ReadContainedItem = readContainedItem;
                ScavBeltRuntime.ReadDeleted = readDeleted;
                ScavBeltRuntime.WriteDeleted = writeDeleted;
                ScavBeltRuntime.ReadTemplateId = readTemplateId;
                ScavBeltRuntime.ArmBandValue = Enum.Parse(equipmentSlotType, BeltSlotPlan.ArmBand, false);

                object postfix = harmonyMethodConstructor.Invoke(new object[] { FindOwnDeclaredMethod(nameof(ReplaceInventoryPostfix)) });
                Patch(patchMethod, harmonyMethodType, replaceInventory, postfix);

                logInfo?.Invoke("B&A&HB item-descriptor scoped Scav ArmBand compatibility installed with startup-bound delegates.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Scav wearable compatibility installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo FindReplaceInventory(Type controllerType, Type inventoryType)
        {
            MethodInfo[] methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "ReplaceInventory", StringComparison.Ordinal) || method.ReturnType != typeof(void)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == inventoryType) return method;
            }
            return null;
        }

        static Func<object, object> BuildObjectReader(Type declaringType, PropertyInfo property, string name)
        {
            MethodInfo getter = property?.GetGetMethod(true);
            if (getter == null) return null;
            try
            {
                DynamicMethod dm = new DynamicMethod(name, typeof(object), new[] { typeof(object) }, typeof(ScavBeltPatches), true);
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

        static Func<object, object, object> BuildBinaryObjectCall(Type declaringType, Type argumentType, MethodInfo method)
        {
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBScavGetSlot", typeof(object), new[] { typeof(object), typeof(object) }, typeof(ScavBeltPatches), true);
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

        static Func<object, bool> BuildBoolReader(Type declaringType, PropertyInfo property)
        {
            MethodInfo getter = property?.GetGetMethod(true);
            if (getter == null || getter.ReturnType != typeof(bool)) return null;
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBScavDeletedRead", typeof(bool), new[] { typeof(object) }, typeof(ScavBeltPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
                il.Emit(OpCodes.Ret);
                return (Func<object, bool>)dm.CreateDelegate(typeof(Func<object, bool>));
            }
            catch { return null; }
        }

        static Action<object, bool> BuildBoolWriter(Type declaringType, PropertyInfo property)
        {
            MethodInfo setter = property?.GetSetMethod(true);
            if (setter == null || property.PropertyType != typeof(bool)) return null;
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBScavDeletedWrite", typeof(void), new[] { typeof(object), typeof(bool) }, typeof(ScavBeltPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(setter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, setter);
                il.Emit(OpCodes.Ret);
                return (Action<object, bool>)dm.CreateDelegate(typeof(Action<object, bool>));
            }
            catch { return null; }
        }

        static Func<object, string> BuildStringReader(Type declaringType, PropertyInfo property)
        {
            MethodInfo getter = property?.GetGetMethod(true);
            if (getter == null || getter.ReturnType != typeof(string)) return null;
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBScavTemplateId", typeof(string), new[] { typeof(object) }, typeof(ScavBeltPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
                il.Emit(OpCodes.Ret);
                return (Func<object, string>)dm.CreateDelegate(typeof(Func<object, string>));
            }
            catch { return null; }
        }

        static void ReplaceInventoryPostfix(object __instance)
        {
            ScavBeltRuntime.RestoreContainerBeltSlot(__instance);
        }

        static MethodInfo FindOwnDeclaredMethod(string name)
        {
            MethodInfo[] methods = typeof(ScavBeltPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 1) return methods[i];
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

        bool Fail(string message)
        {
            logWarning?.Invoke(message);
            return false;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); }
            catch { }
            harmony = null;
            unpatchSelf = null;
            ScavBeltRuntime.Reset();
        }
    }
}
