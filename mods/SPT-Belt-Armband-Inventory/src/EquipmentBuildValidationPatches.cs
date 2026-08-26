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
            if (!AccessoryCapabilityPolicy.Has(AccessoryCategory.ArmBand, AccessoryCapability.BuildValidation)) return current;
            if (!IsVanillaContainerList(current)) return current;

            string[] result = new string[current.Length + 1];
            Array.Copy(current, result, current.Length);
            result[result.Length - 1] = BeltSlotPlan.ArmBand;
            return result;
        }

        internal static bool IsVanillaContainerList(string[] current)
        {
            if (current == null || current.Length != VanillaContainerSlots.Length) return false;
            for (int i = 0; i < current.Length; i++)
            {
                if (!string.Equals(current[i], VanillaContainerSlots[i], StringComparison.Ordinal)) return false;
            }
            return true;
        }
    }

    internal static class EquipmentBuildValidationRuntime
    {
        internal static Action<string> LogWarning;
        internal static FieldInfo[] CandidateFields;
        internal static Type SlotEnumType;
        internal static object ArmBandValue;

        internal static void Normalize(object screen)
        {
            if (screen == null || CandidateFields == null || SlotEnumType == null || ArmBandValue == null) return;

            try
            {
                for (int i = 0; i < CandidateFields.Length; i++)
                {
                    FieldInfo field = CandidateFields[i];
                    Array current = field.GetValue(screen) as Array;
                    if (current == null || current.Length != EquipmentBuildContainerPolicy.VanillaContainerSlots.Length) continue;

                    string[] names = new string[current.Length];
                    for (int p = 0; p < current.Length; p++)
                    {
                        object value = current.GetValue(p);
                        names[p] = value == null ? null : value.ToString();
                    }
                    if (!EquipmentBuildContainerPolicy.IsVanillaContainerList(names)) continue;

                    Array replacement = Array.CreateInstance(SlotEnumType, current.Length + 1);
                    Array.Copy(current, replacement, current.Length);
                    replacement.SetValue(ArmBandValue, current.Length);
                    field.SetValue(screen, replacement);
                    return;
                }
            }
            catch (Exception exception)
            {
                if (LogWarning != null) LogWarning("Could not include belt in equipment-build container validation: " + Unwrap(exception).Message);
            }
        }

        internal static void Reset()
        {
            LogWarning = null;
            CandidateFields = null;
            SlotEnumType = null;
            ArmBandValue = null;
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
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
                    return Fail("SPT 4.1 EquipmentBuildsScreen/EquipmentSlot or Harmony was not found; belt build-container validation is disabled.");

                MethodInfo awake = screenType.GetMethod("Awake", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (awake == null)
                    return Fail("SPT 4.1 EquipmentBuildsScreen.Awake shape changed; belt build-container validation is disabled.");

                FieldInfo[] fields = screenType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                int count = 0;
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].FieldType.IsArray && fields[i].FieldType.GetElementType() == slotEnumType) count++;
                }
                if (count == 0)
                    return Fail("SPT 4.1 equipment-build slot arrays were not found; belt build-container validation is disabled.");

                FieldInfo[] candidates = new FieldInfo[count];
                int index = 0;
                for (int i = 0; i < fields.Length; i++)
                {
                    if (fields[i].FieldType.IsArray && fields[i].FieldType.GetElementType() == slotEnumType) candidates[index++] = fields[i];
                }

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmony == null || patchMethod == null || harmonyMethodConstructor == null)
                    return Fail("Harmony patch API is incompatible; belt build-container validation is disabled.");

                EquipmentBuildValidationRuntime.LogWarning = logWarning;
                EquipmentBuildValidationRuntime.CandidateFields = candidates;
                EquipmentBuildValidationRuntime.SlotEnumType = slotEnumType;
                EquipmentBuildValidationRuntime.ArmBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand, false);

                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(Postfix)) });
                Patch(patchMethod, harmonyMethodType, awake, postfix);

                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (logInfo != null) logInfo("Belt/Armband Inventory equipment-build container validation installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Belt equipment-build validation installation failed safely: " + exception.Message);
            }
        }

        static void Postfix(object __instance)
        {
            EquipmentBuildValidationRuntime.Normalize(__instance);
        }

        static MethodInfo Method(string name)
        {
            return typeof(EquipmentBuildValidationPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
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
            EquipmentBuildValidationRuntime.Reset();
        }
    }
}
