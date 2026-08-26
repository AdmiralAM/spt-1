using System;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class SlotMergePolicy
    {
        internal static bool ShouldForce(string slotId, bool hasContainedItem, bool containedItemIsRuntimeCandidate)
        {
            if (!string.Equals(slotId, BeltSlotPlan.ArmBand, StringComparison.Ordinal)) return false;
            return !hasContainedItem || containedItemIsRuntimeCandidate;
        }
    }

    internal static class SlotMergeRuntime
    {
        internal static object InheritFromItemValue;
        internal static Action<string> LogWarning;
        static bool runtimeFailureLogged;

        internal static void OverrideResult(object slot, ref object result)
        {
            if (slot == null || InheritFromItemValue == null) return;
            try
            {
                // MergeContainerWithChildren is queried for many EFT slots during profile
                // reconstruction. Exit before touching ContainedItem/template metadata unless
                // this is the actual ArmBand host; otherwise a single optional compatibility
                // patch becomes global reflection work during inventory construction.
                string id = ReflectionTools.ReadMember(slot, "ID") as string;
                if (!string.Equals(id, BeltSlotPlan.ArmBand, StringComparison.Ordinal)) return;

                object containedItem = ReflectionTools.ReadMember(slot, "ContainedItem");
                bool hasContainedItem = containedItem != null;
                bool containedItemIsRuntimeCandidate = hasContainedItem && IsRuntimeCandidate(containedItem);
                if (!SlotMergePolicy.ShouldForce(id, hasContainedItem, containedItemIsRuntimeCandidate)) return;
                result = InheritFromItemValue;
            }
            catch (Exception exception)
            {
                if (!runtimeFailureLogged)
                {
                    runtimeFailureLogged = true;
                    Exception root = Unwrap(exception);
                    LogWarning?.Invoke("B&A&HB MERGE RUNTIME FAIL-CLOSED: " + root.GetType().FullName + ": " + root.Message
                        + (string.IsNullOrEmpty(root.StackTrace) ? "" : "\n" + root.StackTrace));
                }
            }
        }

        static bool IsRuntimeCandidate(object item)
        {
            object stringTemplateId = ReflectionTools.ReadMember(item, "StringTemplateId");
            if (AccessoryGridPolicy.IsRuntimeCandidateTemplate(stringTemplateId as string)) return true;

            object templateId = ReflectionTools.ReadMember(item, "TemplateId");
            return templateId != null && AccessoryGridPolicy.IsRuntimeCandidateTemplate(templateId.ToString());
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        internal static void Reset()
        {
            InheritFromItemValue = null;
            LogWarning = null;
            runtimeFailureLogged = false;
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

                PropertyInfo property = ReflectionTools.FindInstanceProperty(slotType, "MergeContainerWithChildren", parentMergeType);
                MethodInfo getter = property == null ? null : property.GetGetMethod(true);
                if (getter == null)
                    return Fail("SPT 4.1 Slot.MergeContainerWithChildren getter shape changed; ArmBand merge compatibility is disabled.");

                object inheritFromItem;
                try { inheritFromItem = Enum.Parse(parentMergeType, "InheritFromItem", false); }
                catch (Exception exception)
                {
                    LogException("B&A&HB MERGE ENUM", exception);
                    return Fail("EParentMergeType.InheritFromItem was not found; ArmBand merge compatibility is disabled.");
                }

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, hmCtor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; ArmBand merge compatibility is disabled.");
                unpatchSelf = rollback;

                SlotMergeRuntime.InheritFromItemValue = inheritFromItem;
                SlotMergeRuntime.LogWarning = logWarning;
                object hmPostfix = hmCtor.Invoke(new object[] { Method(nameof(PostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, getter, hmPostfix);

                logInfo?.Invoke("ArmBand merge compatibility installed via RC-scoped MergeContainerWithChildren result override.");
                return true;
            }
            catch (Exception exception)
            {
                LogException("B&A&HB MERGE INSTALL", exception);
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("ArmBand merge compatibility installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo originalMethod = original as MethodInfo;
            if (originalMethod == null || originalMethod.DeclaringType == null || originalMethod.ReturnType == typeof(void)) return null;
            return BuildPostfix(originalMethod.DeclaringType, originalMethod.ReturnType);
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

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(SlotMergePatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        void LogException(string prefix, Exception exception)
        {
            Exception root = Unwrap(exception);
            logWarning?.Invoke(prefix + " exception type=" + root.GetType().FullName);
            logWarning?.Invoke(prefix + " message=" + root.Message);
            if (!string.IsNullOrEmpty(root.StackTrace)) logWarning?.Invoke(prefix + " stack=" + root.StackTrace);
        }

        bool Fail(string message) { logWarning?.Invoke(message); return false; }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            SlotMergeRuntime.Reset();
        }
    }
}
