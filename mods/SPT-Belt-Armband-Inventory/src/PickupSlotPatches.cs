using System;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class PickupSlotPolicy
    {
        internal static bool ShouldTry(bool vanillaMissing, bool hasContainers, bool slotDeleted, bool compatible)
        {
            return vanillaMissing
                && !slotDeleted
                && compatible
                && AccessoryCapabilityPolicy.CanUse(
                    AccessoryCategory.ArmBand,
                    AccessoryCapability.PickupFallback,
                    true,
                    hasContainers);
        }
    }

    internal static class PickupSlotRuntime
    {
        internal static Action<string> LogWarning;

        internal static object Resolve(object current, object equipment, object item)
        {
            if (current != null || equipment == null || item == null) return current;
            try
            {
                bool hasContainers = ReflectionTools.HasContainers(item);
                MethodInfo getSlot = FindGetSlot(equipment.GetType());
                if (!hasContainers || getSlot == null) return current;

                Type slotEnum = getSlot.GetParameters()[0].ParameterType;
                object armBand = Enum.Parse(slotEnum, BeltSlotPlan.ArmBand, false);
                object slot = getSlot.Invoke(equipment, new[] { armBand });
                if (slot == null) return current;

                bool deleted = ReadBool(slot, "Deleted");
                bool compatible = CheckCompatibility(slot, item);
                if (!PickupSlotPolicy.ShouldTry(true, hasContainers, deleted, compatible)) return current;

                MethodInfo find = FindLocationMethod(slot.GetType(), item.GetType());
                if (find == null) return current;
                object[] args = new object[find.GetParameters().Length];
                args[0] = item;
                object address = find.Invoke(slot, args);
                return address ?? current;
            }
            catch (Exception exception)
            {
                if (LogWarning != null) LogWarning("Could not resolve ArmBand pickup slot for container belt: " + Unwrap(exception).Message);
                return current;
            }
        }

        static MethodInfo FindGetSlot(Type type)
        {
            foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "GetSlot") continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length == 1 && p[0].ParameterType.IsEnum) return method;
            }
            return null;
        }

        static bool ReadBool(object target, string name)
        {
            object value = ReflectionTools.ReadMember(target, name);
            return value is bool && (bool)value;
        }

        static bool CheckCompatibility(object slot, object item)
        {
            foreach (MethodInfo method in slot.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "CheckCompatibility" || method.ReturnType != typeof(bool)) continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length == 1 && p[0].ParameterType.IsInstanceOfType(item)) return (bool)method.Invoke(slot, new[] { item });
            }
            return false;
        }

        static MethodInfo FindLocationMethod(Type slotType, Type itemType)
        {
            foreach (MethodInfo method in slotType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "FindLocationForItem") continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length >= 1 && p[0].ParameterType.IsAssignableFrom(itemType)) return method;
            }
            return null;
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
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
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                if (harmonyType == null || harmonyMethodType == null || equipmentType == null || itemType == null)
                    return Fail("SPT 4.1 pickup types or Harmony were not found; automatic belt pickup is disabled.");

                MethodInfo target = FindTarget(equipmentType, itemType);
                if (target == null || target.ReturnType.IsValueType)
                    return Fail("SPT 4.1 FindSlotToPickUp(InventoryEquipment, Item) shape changed; automatic belt pickup is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, hmCtor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; automatic belt pickup is disabled.");
                unpatchSelf = rollback;

                PickupSlotRuntime.LogWarning = logWarning;
                object postfix = hmCtor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, target, postfix);
                if (logInfo != null) logInfo("Belt/Armband Inventory automatic pickup compatibility installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Automatic belt pickup compatibility installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo FindTarget(Type equipmentType, Type itemType)
        {
            foreach (Type type in ReflectionTools.GetTypes(equipmentType.Assembly))
            {
                if (type == null) continue;
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (method.Name != "FindSlotToPickUp") continue;
                    ParameterInfo[] p = method.GetParameters();
                    if (p.Length == 2 && p[0].ParameterType == equipmentType && p[1].ParameterType == itemType) return method;
                }
            }
            return null;
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo originalMethod = original as MethodInfo;
            if (originalMethod == null || originalMethod.ReturnType == typeof(void) || originalMethod.ReturnType.IsValueType) return null;
            return BuildPostfix(originalMethod.ReturnType);
        }

        internal static MethodInfo BuildPostfix(Type resultType)
        {
            DynamicMethod method = new DynamicMethod("BeltPickupPostfix", typeof(void), new[] { resultType.MakeByRefType(), typeof(object[]) }, typeof(PickupSlotPatches), true);
            method.DefineParameter(1, ParameterAttributes.None, "__result");
            method.DefineParameter(2, ParameterAttributes.None, "__args");
            ILGenerator il = method.GetILGenerator();
            Label end = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Brfalse_S, end);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldlen);
            il.Emit(OpCodes.Conv_I4);
            il.Emit(OpCodes.Ldc_I4_2);
            il.Emit(OpCodes.Blt_S, end);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ldelem_Ref);
            il.Emit(OpCodes.Call, typeof(PickupSlotRuntime).GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Castclass, resultType);
            il.Emit(OpCodes.Stind_Ref);
            il.MarkLabel(end);
            il.Emit(OpCodes.Ret);
            return method;
        }

        static MethodInfo Method(string name)
        {
            return typeof(PickupSlotPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Patch") continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length < 2 || !typeof(MethodBase).IsAssignableFrom(p[0].ParameterType)) continue;
                for (int i = 1; i < p.Length; i++)
                    if (p[i].ParameterType == harmonyMethodType && string.Equals(p[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] p = patchMethod.GetParameters();
            object[] args = new object[p.Length];
            args[0] = original;
            for (int i = 1; i < p.Length; i++)
                if (p[i].ParameterType == harmonyMethodType && string.Equals(p[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) args[i] = postfix;
            patchMethod.Invoke(harmony, args);
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        bool Fail(string message) { if (logWarning != null) logWarning(message); return false; }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            PickupSlotRuntime.LogWarning = null;
        }
    }
}
