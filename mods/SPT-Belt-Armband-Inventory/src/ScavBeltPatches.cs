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
        internal static object[] WearableSlotValues;
        static bool runtimeFailureLogged;

        internal static void RestoreContainerBeltSlot(object inventoryController)
        {
            if (inventoryController == null || ReadInventory == null || ReadEquipment == null || GetSlot == null
                || ReadContainedItem == null || ReadDeleted == null || WriteDeleted == null || ReadTemplateId == null
                || WearableSlotValues == null || WearableSlotValues.Length == 0) return;

            try
            {
                object inventory = ReadInventory(inventoryController);
                object equipment = inventory == null ? null : ReadEquipment(inventory);
                if (equipment == null) return;

                for (int i = 0; i < WearableSlotValues.Length; i++)
                {
                    object slotValue = WearableSlotValues[i];
                    if (slotValue == null) continue;
                    object slot = GetSlot(equipment, slotValue);
                    if (slot == null) continue;

                    object item = ReadContainedItem(slot);
                    bool deleted = ReadDeleted(slot);
                    string templateId = item == null ? null : ReadTemplateId(item);
                    if (!ScavBeltPolicy.ShouldRestore(templateId, deleted, ReflectionTools.HasContainers(item))) continue;
                    WriteDeleted(slot, false);
                }
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
            WearableSlotValues = null;
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
                MemberInfo inventoryMember = FindReadableMember(controllerType, "Inventory", inventoryType);
                MemberInfo equipmentMember = FindReadableMember(inventoryType, "Equipment", equipmentType);
                MethodInfo getSlot = ReflectionTools.FindInstanceMethod(equipmentType, "GetSlot", slotType, equipmentSlotType);
                MemberInfo containedItemMember = FindReadableMember(slotType, "ContainedItem", itemType);
                MemberInfo deletedMember = FindWritableMember(slotType, "Deleted", typeof(bool));
                MemberInfo templateIdMember = FindReadableMember(itemType, "StringTemplateId", typeof(string));
                if (replaceInventory == null || inventoryMember == null || equipmentMember == null || getSlot == null
                    || containedItemMember == null || deletedMember == null || templateIdMember == null)
                    return Fail("SPT 4.1 Scav wearable boundary is incomplete after bounded property/field discovery; compatibility is disabled.");

                Func<object, object> readInventory = BuildObjectReader(controllerType, inventoryMember, "BAndHBScavInventory");
                Func<object, object> readEquipment = BuildObjectReader(inventoryType, equipmentMember, "BAndHBScavEquipment");
                Func<object, object, object> getSlotDelegate = BuildBinaryObjectCall(equipmentType, equipmentSlotType, getSlot);
                Func<object, object> readContainedItem = BuildObjectReader(slotType, containedItemMember, "BAndHBScavContainedItem");
                Func<object, bool> readDeleted = BuildBoolReader(slotType, deletedMember);
                Action<object, bool> writeDeleted = BuildBoolWriter(slotType, deletedMember);
                Func<object, string> readTemplateId = BuildStringReader(itemType, templateIdMember);
                if (readInventory == null || readEquipment == null || getSlotDelegate == null || readContainedItem == null
                    || readDeleted == null || writeDeleted == null || readTemplateId == null)
                    return Fail("SPT 4.1 Scav wearable delegates could not be bound; compatibility is disabled.");

                object[] wearableSlots;
                try
                {
                    wearableSlots = new[]
                    {
                        Enum.Parse(equipmentSlotType, BeltSlotPlan.ArmBand, false),
                        Enum.ToObject(equipmentSlotType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue),
                        Enum.ToObject(equipmentSlotType, RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue)
                    };
                }
                catch (Exception exception)
                {
                    return Fail("SPT 4.1 wearable slot identities could not be bound for ReplaceInventory: " + Unwrap(exception).Message);
                }

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
                ScavBeltRuntime.WearableSlotValues = wearableSlots;

                object postfix = harmonyMethodConstructor.Invoke(new object[] { FindOwnDeclaredMethod(nameof(ReplaceInventoryPostfix)) });
                Patch(patchMethod, harmonyMethodType, replaceInventory, postfix);

                logInfo?.Invoke("B&A&HB exact-item Scav ReplaceInventory compatibility installed for ArmBand, Belt15 and HeadBand16 with startup-bound property/field delegates.");
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
            MethodInfo selected = null;
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "ReplaceInventory", StringComparison.Ordinal) || method.ReturnType != typeof(void)) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != inventoryType) continue;
                if (selected != null) return null;
                selected = method;
            }
            return selected;
        }

        static MemberInfo FindReadableMember(Type type, string name, Type expectedType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.GetGetMethod(true) != null && expectedType.IsAssignableFrom(property.PropertyType)) return property;
            FieldInfo field = type.GetField(name, flags);
            if (field != null && expectedType.IsAssignableFrom(field.FieldType)) return field;
            return null;
        }

        static MemberInfo FindWritableMember(Type type, string name, Type expectedType)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            PropertyInfo property = type.GetProperty(name, flags);
            if (property != null && property.GetGetMethod(true) != null && property.GetSetMethod(true) != null && property.PropertyType == expectedType) return property;
            FieldInfo field = type.GetField(name, flags);
            if (field != null && !field.IsInitOnly && field.FieldType == expectedType) return field;
            return null;
        }

        static Func<object, object> BuildObjectReader(Type declaringType, MemberInfo member, string name)
        {
            try
            {
                DynamicMethod dm = new DynamicMethod(name, typeof(object), new[] { typeof(object) }, typeof(ScavBeltPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                Type valueType;
                if (member is PropertyInfo property)
                {
                    MethodInfo getter = property.GetGetMethod(true);
                    if (getter == null) return null;
                    il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
                    valueType = property.PropertyType;
                }
                else if (member is FieldInfo field)
                {
                    il.Emit(OpCodes.Ldfld, field);
                    valueType = field.FieldType;
                }
                else return null;
                if (valueType.IsValueType) il.Emit(OpCodes.Box, valueType);
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

        static Func<object, bool> BuildBoolReader(Type declaringType, MemberInfo member)
        {
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBScavDeletedRead", typeof(bool), new[] { typeof(object) }, typeof(ScavBeltPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                if (member is PropertyInfo property)
                {
                    MethodInfo getter = property.GetGetMethod(true);
                    if (getter == null || property.PropertyType != typeof(bool)) return null;
                    il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter);
                }
                else if (member is FieldInfo field)
                {
                    if (field.FieldType != typeof(bool)) return null;
                    il.Emit(OpCodes.Ldfld, field);
                }
                else return null;
                il.Emit(OpCodes.Ret);
                return (Func<object, bool>)dm.CreateDelegate(typeof(Func<object, bool>));
            }
            catch { return null; }
        }

        static Action<object, bool> BuildBoolWriter(Type declaringType, MemberInfo member)
        {
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBScavDeletedWrite", typeof(void), new[] { typeof(object), typeof(bool) }, typeof(ScavBeltPatches), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, declaringType);
                il.Emit(OpCodes.Ldarg_1);
                if (member is PropertyInfo property)
                {
                    MethodInfo setter = property.GetSetMethod(true);
                    if (setter == null || property.PropertyType != typeof(bool)) return null;
                    il.Emit(setter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, setter);
                }
                else if (member is FieldInfo field)
                {
                    if (field.FieldType != typeof(bool) || field.IsInitOnly) return null;
                    il.Emit(OpCodes.Stfld, field);
                }
                else return null;
                il.Emit(OpCodes.Ret);
                return (Action<object, bool>)dm.CreateDelegate(typeof(Action<object, bool>));
            }
            catch { return null; }
        }

        static Func<object, string> BuildStringReader(Type declaringType, MemberInfo member)
        {
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBScavTemplateId", typeof(string), new[] { typeof(object) }, typeof(ScavBeltPatches), true);
                ILGenerator il = dm.GetILGenerator();
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
