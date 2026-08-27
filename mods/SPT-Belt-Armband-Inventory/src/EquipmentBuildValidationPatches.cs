using System;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class EquipmentBuildContainerPolicy
    {
        internal static readonly string[] VanillaContainerSlots =
        {
            BeltSlotPlan.TacticalVest,
            BeltSlotPlan.Pockets,
            BeltSlotPlan.Backpack,
            BeltSlotPlan.SecuredContainer
        };

        internal static string[] Build(string[] current)
        {
            if (!IsVanillaContainerList(current)) return current;
            return new[]
            {
                BeltSlotPlan.TacticalVest,
                BeltSlotPlan.Pockets,
                RuntimeIdentity.DedicatedBeltWireSlotId,
                BeltSlotPlan.Backpack,
                BeltSlotPlan.SecuredContainer,
                BeltSlotPlan.ArmBand,
                RuntimeIdentity.DedicatedHeadBandWireSlotId
            };
        }

        internal static bool IsVanillaContainerList(string[] current)
        {
            if (current == null || current.Length != VanillaContainerSlots.Length) return false;
            for (int i = 0; i < current.Length; i++)
                if (!string.Equals(current[i], VanillaContainerSlots[i], StringComparison.Ordinal)) return false;
            return true;
        }
    }

    internal static class EquipmentBuildValidationRuntime
    {
        internal static Action<string> LogWarning;
        internal static FieldInfo[] CandidateFields;
        internal static Type SlotEnumType;
        internal static object ArmBandValue;
        internal static object DedicatedBeltValue;
        internal static object DedicatedHeadBandValue;

        internal static void Normalize(object screen)
        {
            if (screen == null || CandidateFields == null || SlotEnumType == null || ArmBandValue == null || DedicatedBeltValue == null || DedicatedHeadBandValue == null) return;
            try
            {
                for (int i = 0; i < CandidateFields.Length; i++)
                {
                    FieldInfo field = CandidateFields[i];
                    Array current = field.GetValue(screen) as Array;
                    if (current == null || current.Length != EquipmentBuildContainerPolicy.VanillaContainerSlots.Length) continue;

                    string[] names = new string[current.Length];
                    for (int p = 0; p < current.Length; p++) names[p] = current.GetValue(p)?.ToString();
                    if (!EquipmentBuildContainerPolicy.IsVanillaContainerList(names)) continue;

                    Array replacement = Array.CreateInstance(SlotEnumType, 7);
                    replacement.SetValue(current.GetValue(0), 0); // TacticalVest
                    replacement.SetValue(current.GetValue(1), 1); // Pockets
                    replacement.SetValue(DedicatedBeltValue, 2);
                    replacement.SetValue(current.GetValue(2), 3); // Backpack
                    replacement.SetValue(current.GetValue(3), 4); // SecuredContainer
                    replacement.SetValue(ArmBandValue, 5);
                    replacement.SetValue(DedicatedHeadBandValue, 6);
                    field.SetValue(screen, replacement);
                    return;
                }
            }
            catch (Exception exception)
            {
                LogFail("Could not include wearable equipment locations in equipment-build container validation", exception);
            }
        }

        static void LogFail(string message, Exception exception)
        {
            if (LogWarning == null) return;
            Exception root = Unwrap(exception);
            LogWarning(message + ": " + root.GetType().FullName + ": " + root.Message);
        }

        internal static void Reset()
        {
            LogWarning = null;
            CandidateFields = null;
            SlotEnumType = null;
            ArmBandValue = null;
            DedicatedBeltValue = null;
            DedicatedHeadBandValue = null;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }
    }

    internal sealed class EquipmentBuildValidationPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.equipment-builds";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal EquipmentBuildValidationPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type screenType = ReflectionTools.FindType("EFT.UI.EquipmentBuildsScreen");
                Type slotEnumType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                if (harmonyType == null || harmonyMethodType == null || screenType == null || slotEnumType == null)
                    return Fail("SPT 4.1 EquipmentBuildsScreen/EquipmentSlot or Harmony was not found; wearable build-container validation is disabled.");

                MethodInfo awake = ReflectionTools.FindInstanceMethod(screenType, "Awake", typeof(void));
                if (awake == null) return Fail("SPT 4.1 EquipmentBuildsScreen.Awake() shape changed; wearable build-container validation is disabled.");

                FieldInfo[] fields = screenType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                int count = 0;
                for (int i = 0; i < fields.Length; i++) if (fields[i].FieldType.IsArray && fields[i].FieldType.GetElementType() == slotEnumType) count++;
                if (count == 0) return Fail("SPT 4.1 equipment-build slot arrays were not found; wearable build-container validation is disabled.");

                FieldInfo[] candidates = new FieldInfo[count];
                int index = 0;
                for (int i = 0; i < fields.Length; i++) if (fields[i].FieldType.IsArray && fields[i].FieldType.GetElementType() == slotEnumType) candidates[index++] = fields[i];

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; wearable build-container validation is disabled.");
                unpatchSelf = rollback;

                EquipmentBuildValidationRuntime.LogWarning = logWarning;
                EquipmentBuildValidationRuntime.CandidateFields = candidates;
                EquipmentBuildValidationRuntime.SlotEnumType = slotEnumType;
                EquipmentBuildValidationRuntime.ArmBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);
                EquipmentBuildValidationRuntime.DedicatedBeltValue = Enum.ToObject(slotEnumType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue);
                EquipmentBuildValidationRuntime.DedicatedHeadBandValue = Enum.ToObject(slotEnumType, RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);

                object postfix = harmonyMethodConstructor.Invoke(new object[] { FindOwnDeclaredMethod(nameof(Postfix)) });
                Patch(patchMethod, harmonyMethodType, awake, postfix);

                logInfo?.Invoke("B&A&HB equipment-build validation includes ArmBand plus dedicated Belt/HeadBand pseudo-slots.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Wearable equipment-build validation installation failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static void Postfix(object __instance) { EquipmentBuildValidationRuntime.Normalize(__instance); }

        static MethodInfo FindOwnDeclaredMethod(string name)
        {
            MethodInfo[] methods = typeof(EquipmentBuildValidationPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < methods.Length; i++) if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 1) return methods[i];
            return null;
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++) if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 0) return methods[i];
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
                for (int p = 1; p < parameters.Length; p++) if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++) if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
            patchMethod.Invoke(harmony, arguments);
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
            EquipmentBuildValidationRuntime.Reset();
        }
    }
}
