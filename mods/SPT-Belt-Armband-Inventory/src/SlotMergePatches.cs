using System;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class SlotMergePolicy
    {
        internal static bool ShouldForce(string slotId)
        {
            return string.Equals(slotId, BeltSlotPlan.ArmBand, StringComparison.Ordinal);
        }
    }

    internal static class SlotMergeRuntime
    {
        internal static object InheritFromItemValue;

        internal static void OverrideResult(object slot, ref object result)
        {
            if (slot == null || InheritFromItemValue == null) return;
            string id = ReflectionTools.ReadMember(slot, "ID") as string;
            if (!SlotMergePolicy.ShouldForce(id)) return;
            result = InheritFromItemValue;
        }

        internal static void Reset()
        {
            InheritFromItemValue = null;
        }
    }

    internal sealed class SlotMergePatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.slotmerge";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal SlotMergePatches(Action<string> logInfo, Action<string> logWarning)
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
                Type slotType = ReflectionTools.FindType("EFT.InventoryLogic.Slot");
                Type parentMergeType = ReflectionTools.FindType("EFT.InventoryLogic.EParentMergeType");
                if (harmonyType == null || harmonyMethodType == null || slotType == null || parentMergeType == null)
                    return Fail("SPT 4.1 Slot/EParentMergeType or Harmony was not found; ArmBand merge compatibility is disabled.");

                PropertyInfo property = slotType.GetProperty("MergeContainerWithChildren", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo getter = property == null ? null : property.GetGetMethod(true);
                if (getter == null || getter.ReturnType != parentMergeType)
                    return Fail("SPT 4.1 Slot.MergeContainerWithChildren getter shape changed; ArmBand merge compatibility is disabled.");

                object inheritFromItem;
                try
                {
                    inheritFromItem = Enum.Parse(parentMergeType, "InheritFromItem", false);
                }
                catch
                {
                    return Fail("EParentMergeType.InheritFromItem was not found; ArmBand merge compatibility is disabled.");
                }

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (harmony == null || patchMethod == null || hmCtor == null || unpatchSelf == null)
                    return Fail("Harmony patch API is incompatible; ArmBand merge compatibility is disabled.");

                SlotMergeRuntime.InheritFromItemValue = inheritFromItem;
                MethodInfo postfix = BuildPostfix(slotType, parentMergeType);
                object hmPostfix = hmCtor.Invoke(new object[] { postfix });
                Patch(patchMethod, harmonyMethodType, getter, hmPostfix);

                if (logInfo != null) logInfo("Belt/Armband Inventory slot merge compatibility installed via MergeContainerWithChildren postfix result override.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("ArmBand merge compatibility installation failed safely: " + exception.Message);
            }
        }

        static MethodInfo BuildPostfix(Type slotType, Type parentMergeType)
        {
            DynamicMethod method = new DynamicMethod(
                "ArmBandMergeContainerWithChildrenPostfix",
                typeof(void),
                new[] { slotType, parentMergeType.MakeByRefType() },
                typeof(SlotMergePatches),
                true);
            method.DefineParameter(1, ParameterAttributes.None, "__instance");
            method.DefineParameter(2, ParameterAttributes.None, "__result");

            ILGenerator il = method.GetILGenerator();
            LocalBuilder boxedResult = il.DeclareLocal(typeof(object));

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldobj, parentMergeType);
            il.Emit(OpCodes.Box, parentMergeType);
            il.Emit(OpCodes.Stloc, boxedResult);

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldloca_S, boxedResult);
            il.Emit(OpCodes.Call, typeof(SlotMergeRuntime).GetMethod(nameof(SlotMergeRuntime.OverrideResult), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));

            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldloc, boxedResult);
            il.Emit(OpCodes.Unbox_Any, parentMergeType);
            il.Emit(OpCodes.Stobj, parentMergeType);
            il.Emit(OpCodes.Ret);
            return method;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                for (int i = 1; i < parameters.Length; i++)
                    if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
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

        bool Fail(string message) { if (logWarning != null) logWarning(message); return false; }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            SlotMergeRuntime.Reset();
        }
    }
}
