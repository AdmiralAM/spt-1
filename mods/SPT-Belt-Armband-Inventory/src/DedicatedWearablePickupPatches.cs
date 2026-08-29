using System;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class DedicatedWearablePickupRuntime
    {
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Type EquipmentSlotType;
        static bool beltProofLogged;
        static bool headBandProofLogged;
        static bool failureLogged;

        internal static object Resolve(object current, object equipment, object item)
        {
            if (current != null || equipment == null || item == null) return current;
            if (EquipmentSlotType == null
                || PickupSlotRuntime.ReadTemplateId == null
                || PickupSlotRuntime.GetSlot == null
                || PickupSlotRuntime.ReadSlotDeleted == null
                || PickupSlotRuntime.ReadContainedItem == null
                || PickupSlotRuntime.CheckCompatibility == null
                || PickupSlotRuntime.FindLocationForItem == null)
                return current;

            try
            {
                string tpl = PickupSlotRuntime.ReadTemplateId(item);
                int slotValue;
                bool belt;
                if (string.Equals(tpl, RuntimeIdentity.DedicatedMagazineBeltItemId, StringComparison.Ordinal))
                {
                    slotValue = RuntimeIdentity.DedicatedBeltEquipmentSlotValue;
                    belt = true;
                }
                else if (string.Equals(tpl, RuntimeIdentity.EmergencyHeadBandItemId, StringComparison.Ordinal))
                {
                    slotValue = RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue;
                    belt = false;
                }
                else
                {
                    return current;
                }

                object slotKey = Enum.ToObject(EquipmentSlotType, slotValue);
                object slot = PickupSlotRuntime.GetSlot(equipment, slotKey);
                if (slot == null) return current;

                bool deleted = PickupSlotRuntime.ReadSlotDeleted(slot);
                bool empty = PickupSlotRuntime.ReadContainedItem(slot) == null;
                if (deleted || !empty || !PickupSlotRuntime.CheckCompatibility(slot, item)) return current;

                object address = PickupSlotRuntime.FindLocationForItem(slot, item);
                if (address == null) return current;

                if (belt)
                {
                    if (!beltProofLogged)
                    {
                        beltProofLogged = true;
                        LogInfo?.Invoke("B&A&HB AUTO-PLACEMENT PROOF: exact Magazine Belt resolved to dedicated Belt pseudo-slot 15.");
                    }
                }
                else if (!headBandProofLogged)
                {
                    headBandProofLogged = true;
                    LogInfo?.Invoke("B&A&HB AUTO-PLACEMENT PROOF: exact Emergency HeadBand resolved to dedicated HeadBand pseudo-slot 16.");
                }

                return address;
            }
            catch (Exception exception)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    Exception root = Unwrap(exception);
                    LogWarning?.Invoke("B&A&HB dedicated wearable auto-placement failed closed: " + root.GetType().FullName + ": " + root.Message);
                }
                return current;
            }
        }

        internal static void Reset()
        {
            LogInfo = null;
            LogWarning = null;
            EquipmentSlotType = null;
            beltProofLogged = false;
            headBandProofLogged = false;
            failureLogged = false;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }
    }

    internal sealed class DedicatedWearablePickupPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.dedicated-pickup";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal DedicatedWearablePickupPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type itemType = ReflectionTools.FindType("EFT.InventoryLogic.Item");
                if (harmonyType == null || harmonyMethodType == null || equipmentType == null || equipmentSlotType == null || itemType == null)
                    return Fail("Dedicated wearable pickup boundary missing; Belt/HeadBand auto-placement disabled.");

                if (PickupSlotRuntime.GetSlot == null || PickupSlotRuntime.ReadTemplateId == null || PickupSlotRuntime.FindLocationForItem == null)
                    return Fail("Dedicated wearable pickup requires the already-bound core pickup delegates; integration disabled safely.");

                MethodInfo target = FindTarget(equipmentType, itemType);
                if (target == null || target.ReturnType == typeof(void) || target.ReturnType.IsValueType)
                    return Fail("FindSlotToPickUp exact runtime shape changed; dedicated wearable auto-placement disabled.");

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (patchMethod == null || hmCtor == null || unpatchSelf == null)
                    return Fail("Harmony postfix API incompatible with dedicated wearable auto-placement.");

                DedicatedWearablePickupRuntime.LogInfo = logInfo;
                DedicatedWearablePickupRuntime.LogWarning = logWarning;
                DedicatedWearablePickupRuntime.EquipmentSlotType = equipmentSlotType;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object postfix = hmCtor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, target, postfix);
                logInfo?.Invoke("B&A&HB dedicated Belt/HeadBand auto-placement installed on the existing startup-bound pickup boundary.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Dedicated wearable auto-placement installation failed safely: " + root.GetType().FullName + ": " + root.Message);
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

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo method = original as MethodInfo;
            if (method == null || method.ReturnType == typeof(void) || method.ReturnType.IsValueType) return null;
            ParameterInfo[] p = method.GetParameters();
            if (p.Length != 2) return null;

            DynamicMethod postfix = new DynamicMethod(
                "BAndHBDedicatedWearablePickupPostfix",
                typeof(void),
                new[] { method.ReturnType.MakeByRefType(), p[0].ParameterType, p[1].ParameterType },
                typeof(DedicatedWearablePickupPatches),
                true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__result");
            postfix.DefineParameter(2, ParameterAttributes.None, "__0");
            postfix.DefineParameter(3, ParameterAttributes.None, "__1");

            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            if (p[0].ParameterType.IsValueType) il.Emit(OpCodes.Box, p[0].ParameterType);
            il.Emit(OpCodes.Ldarg_2);
            if (p[1].ParameterType.IsValueType) il.Emit(OpCodes.Box, p[1].ParameterType);
            il.Emit(OpCodes.Call, typeof(DedicatedWearablePickupRuntime).GetMethod(nameof(DedicatedWearablePickupRuntime.Resolve), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Castclass, method.ReturnType);
            il.Emit(OpCodes.Stind_Ref);
            il.Emit(OpCodes.Ret);
            return postfix;
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
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) args[i] = postfix;
            patchMethod.Invoke(harmony, args);
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(DedicatedWearablePickupPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++) if (string.Equals(methods[i].Name, name, StringComparison.Ordinal)) return methods[i];
            return null;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 0) return methods[i];
            return null;
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
            DedicatedWearablePickupRuntime.Reset();
        }
    }
}
