using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class UnloadPriorityRuntime
    {
        static MethodInfo getSlot;
        static object armBandValue;
        static object dedicatedBeltValue;
        static Type gridType;
        static Action<string> logWarning;

        internal static bool TryInstall(object harmony, MethodInfo patchMethod, Type harmonyMethodType, ConstructorInfo harmonyMethodConstructor, Type equipmentType, Type slotEnumType, Action<string> warning)
        {
            logWarning = warning;
            MethodInfo target = FindTarget(equipmentType);
            getSlot = ReflectionTools.FindInstanceMethod(equipmentType, "GetSlot", null, slotEnumType);
            if (target == null || getSlot == null || !target.ReturnType.IsGenericType)
                return Fail("SPT 4.1 GetPrioritizedGridsForUnloadedObject shape was not found; wearable unload priority was not patched.");

            Type[] genericArguments = target.ReturnType.GetGenericArguments();
            if (genericArguments.Length != 1) return Fail("SPT 4.1 unload-grid return type changed; wearable unload priority was not patched.");

            gridType = genericArguments[0];
            armBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand);
            dedicatedBeltValue = Enum.ToObject(slotEnumType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue);
            MethodInfo postfixMethod = typeof(UnloadPriorityRuntime).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic);
            object postfix = harmonyMethodConstructor.Invoke(new object[] { postfixMethod });
            Patch(harmony, patchMethod, harmonyMethodType, target, postfix);
            return true;
        }

        internal static void Reset()
        {
            getSlot = null;
            armBandValue = null;
            dedicatedBeltValue = null;
            gridType = null;
            logWarning = null;
        }

        static void Postfix(object[] __args, ref object __result)
        {
            try
            {
                if (__args == null || __args.Length == 0 || __args[0] == null || __result == null) return;
                object equipment = __args[0];
                List<object> wearableGrids = ReadCapabilityGrids(equipment, armBandValue);
                AppendUnique(wearableGrids, ReadCapabilityGrids(equipment, dedicatedBeltValue));
                if (wearableGrids.Count == 0) return;

                object rebuilt = Activator.CreateInstance(typeof(List<>).MakeGenericType(gridType));
                IList list = rebuilt as IList;
                if (list == null) return;

                if (__result is IEnumerable vanilla)
                    foreach (object entry in vanilla)
                        if (entry != null && gridType.IsInstanceOfType(entry)) list.Add(entry);

                for (int i = 0; i < wearableGrids.Count; i++) if (!list.Contains(wearableGrids[i])) list.Add(wearableGrids[i]);
                __result = rebuilt;
            }
            catch (Exception exception)
            {
                logWarning?.Invoke("Could not extend unload-grid priority with wearable grids: " + exception.Message);
            }
        }

        static List<object> ReadCapabilityGrids(object equipment, object slotValue)
        {
            var result = new List<object>();
            if (equipment == null || slotValue == null) return result;
            object slot = getSlot.Invoke(equipment, new[] { slotValue });
            object item = ReflectionTools.ReadMember(slot, "ContainedItem");
            bool hasContainers = ReflectionTools.HasContainers(item);
            string templateId = GetTemplateId(item);
            if (!hasContainers || !WearableItemDescriptorRegistry.HasCapability(templateId, AccessoryCapability.UnloadPriority)) return result;
            IEnumerable grids = ReflectionTools.ReadMember(item, "Grids") as IEnumerable;
            if (grids == null) return result;
            foreach (object grid in grids) if (grid != null && gridType.IsInstanceOfType(grid)) result.Add(grid);
            return result;
        }

        static void AppendUnique(List<object> target, List<object> source)
        {
            for (int i = 0; i < source.Count; i++) if (!target.Contains(source[i])) target.Add(source[i]);
        }

        static string GetTemplateId(object item)
        {
            if (item == null) return null;
            object direct = ReflectionTools.ReadMember(item, "StringTemplateId");
            if (direct is string value && !string.IsNullOrEmpty(value)) return value;
            return ReflectionTools.ReadMember(item, "TemplateId")?.ToString();
        }

        static MethodInfo FindTarget(Type equipmentType)
        {
            Type[] types = ReflectionTools.GetTypes(equipmentType.Assembly);
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null) continue;
                MethodInfo[] methods;
                try { methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); } catch { continue; }
                for (int p = 0; p < methods.Length; p++)
                {
                    MethodInfo method = methods[p];
                    if (!string.Equals(method.Name, "GetPrioritizedGridsForUnloadedObject", StringComparison.Ordinal)) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 2 && parameters[0].ParameterType == equipmentType) return method;
                }
            }
            return null;
        }

        static void Patch(object harmony, MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
            patchMethod.Invoke(harmony, arguments);
        }

        static bool Fail(string message)
        {
            logWarning?.Invoke(message);
            Reset();
            return false;
        }
    }
}
