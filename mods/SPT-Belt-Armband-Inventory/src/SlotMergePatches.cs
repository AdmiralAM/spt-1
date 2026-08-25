using System;
using System.Linq;
using System.Reflection;

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
        internal static Action<string> LogWarning;

        internal static void Prepare(object slot)
        {
            if (slot == null) return;
            try
            {
                string id = ReflectionTools.ReadMember(slot, "ID") as string;
                if (!SlotMergePolicy.ShouldForce(id)) return;

                Type type = slot.GetType();
                FieldInfo field = type.GetField("<MergeContainerWithChildren>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null || !field.FieldType.IsEnum) return;

                object inherit = Enum.Parse(field.FieldType, "InheritFromItem", false);
                if (!Equals(field.GetValue(slot), inherit)) field.SetValue(slot, inherit);
            }
            catch (Exception exception)
            {
                if (LogWarning != null) LogWarning("Could not normalize ArmBand merge behavior: " + exception.Message);
            }
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
                if (harmonyType == null || harmonyMethodType == null || slotType == null)
                    return Fail("SPT 4.1 Slot or Harmony was not found; ArmBand merge compatibility is disabled.");

                PropertyInfo property = slotType.GetProperty("MergeContainerWithChildren", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo getter = property == null ? null : property.GetGetMethod(true);
                FieldInfo backing = slotType.GetField("<MergeContainerWithChildren>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

                bool getterFound = getter != null;
                bool backingFound = backing != null;
                bool backingIsEnum = backingFound && backing.FieldType.IsEnum;
                bool inheritFromItemFound = backingIsEnum && Enum.GetNames(backing.FieldType).Contains("InheritFromItem");

                LogShapeCondition(1, getterFound,
                    getterFound
                        ? "MergeContainerWithChildren getter found: " + getter
                        : "MergeContainerWithChildren property/getter not found.");
                LogShapeCondition(2, backingFound,
                    backingFound
                        ? "backing field found: " + backing.FieldType.FullName
                        : "<MergeContainerWithChildren>k__BackingField not found.");
                LogShapeCondition(3, backingIsEnum,
                    backingFound
                        ? "backing field IsEnum=" + backing.FieldType.IsEnum + ", type=" + backing.FieldType.FullName
                        : "cannot be true because backing field is missing.");
                LogShapeCondition(4, inheritFromItemFound,
                    backingIsEnum
                        ? "enum values=[" + string.Join(",", Enum.GetNames(backing.FieldType)) + "]"
                        : "cannot be true because backing field is missing or is not an enum.");

                if (!getterFound || !backingFound || !backingIsEnum || !inheritFromItemFound)
                    return Fail("SPT 4.1 Slot.MergeContainerWithChildren shape diagnostics complete; one or more required conditions failed, so ArmBand merge compatibility remains safely disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmony == null || patchMethod == null || hmCtor == null)
                    return Fail("Harmony patch API is incompatible; ArmBand merge compatibility is disabled.");

                SlotMergeRuntime.LogWarning = logWarning;
                object prefix = hmCtor.Invoke(new object[] { Method(nameof(Prefix)) });
                Patch(patchMethod, harmonyMethodType, getter, prefix);
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (logInfo != null) logInfo("Belt/Armband Inventory slot merge compatibility installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("ArmBand merge compatibility installation failed safely: " + exception.Message);
            }
        }

        void LogShapeCondition(int condition, bool passed, string detail)
        {
            string message = "B&A&HB MERGE DIAG " + condition + "/4 " + (passed ? "PASS" : "FAIL") + ": " + detail;
            if (passed)
            {
                if (logInfo != null) logInfo(message);
            }
            else
            {
                if (logWarning != null) logWarning(message);
            }
        }

        static void Prefix(object __instance) { SlotMergeRuntime.Prepare(__instance); }

        static MethodInfo Method(string name) => typeof(SlotMergePatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                for (int i = 1; i < parameters.Length; i++)
                    if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "prefix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object prefix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "prefix", StringComparison.OrdinalIgnoreCase)) args[i] = prefix;
            patchMethod.Invoke(harmony, args);
        }

        bool Fail(string message) { if (logWarning != null) logWarning(message); return false; }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            SlotMergeRuntime.LogWarning = null;
        }
    }
}
