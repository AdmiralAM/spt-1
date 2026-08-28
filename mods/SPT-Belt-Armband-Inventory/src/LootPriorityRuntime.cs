using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    internal static class LootPriorityRuntime
    {
        static Func<object, object, object> getSlot;
        static Func<IList> createTypedList;
        static object armBandValue;
        static object dedicatedBeltValue;
        static Type containerType;
        static Action<string> logWarning;

        internal static bool TryInstall(object harmony, MethodInfo patchMethod, Type harmonyMethodType, ConstructorInfo harmonyMethodConstructor, Type equipmentType, Type slotEnumType, Action<string> warning)
        {
            logWarning = warning;
            MethodInfo target = FindTarget(equipmentType);
            MethodInfo getSlotMethod = ReflectionTools.FindInstanceMethod(equipmentType, "GetSlot", null, slotEnumType);
            if (target == null || getSlotMethod == null || !target.ReturnType.IsGenericType)
                return Fail("SPT 4.1 GetPrioritizedContainersForLoot shape was not found; wearable loot priority was not patched.");

            Type[] genericArguments = target.ReturnType.GetGenericArguments();
            if (genericArguments.Length != 1)
                return Fail("SPT 4.1 loot-priority return type changed; wearable loot priority was not patched.");

            containerType = genericArguments[0];
            getSlot = BuildBinaryObjectCall(equipmentType, slotEnumType, getSlotMethod);
            createTypedList = BuildListFactory(containerType);
            if (getSlot == null || createTypedList == null)
                return Fail("SPT 4.1 loot-priority startup delegates could not be bound; wearable loot priority was not patched.");

            armBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand);
            dedicatedBeltValue = Enum.ToObject(slotEnumType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue);
            object postfix = harmonyMethodConstructor.Invoke(new object[] { typeof(LootPriorityRuntime).GetMethod(nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic) });
            Patch(harmony, patchMethod, harmonyMethodType, target, postfix);
            return true;
        }

        internal static void Reset()
        {
            getSlot = null;
            createTypedList = null;
            armBandValue = null;
            dedicatedBeltValue = null;
            containerType = null;
            logWarning = null;
        }

        static void Postfix(object[] __args, ref object __result)
        {
            try
            {
                if (__args == null || __args.Length < 2 || __args[0] == null || __result == null) return;
                object equipment = __args[0];
                List<object> belt = ReadCapabilityContainers(equipment, armBandValue, AccessoryCapability.LootPriority);
                AppendUnique(belt, ReadCapabilityContainers(equipment, dedicatedBeltValue, AccessoryCapability.LootPriority));
                if (belt.Count == 0) return;

                var groups = new Dictionary<string, List<object>>
                {
                    { LootPriorityPlan.Vest, ReadSlotContainers(equipment, "TacticalVest") },
                    { LootPriorityPlan.Pockets, ReadSlotContainers(equipment, "Pockets") },
                    { LootPriorityPlan.Backpack, ReadSlotContainers(equipment, "Backpack") },
                    { LootPriorityPlan.Secure, ReadSlotContainers(equipment, "SecuredContainer") },
                    { LootPriorityPlan.Belt, belt }
                };

                List<object> vanilla = ToObjects(__result);
                LootItemKind kind = InferKind(vanilla, groups, __args[1]);
                string[] order = LootPriorityPlan.Build(kind, true);
                IList list = createTypedList();
                if (list == null) return;

                for (int i = 0; i < order.Length; i++)
                {
                    if (!groups.TryGetValue(order[i], out List<object> source)) continue;
                    for (int p = 0; p < source.Count; p++)
                        if (source[p] != null && containerType.IsInstanceOfType(source[p])) list.Add(source[p]);
                }
                __result = list;
            }
            catch (Exception exception)
            {
                logWarning?.Invoke("Could not extend loot priority with wearable containers: " + exception.Message);
            }
        }

        static List<object> ReadCapabilityContainers(object equipment, object slotValue, AccessoryCapability capability)
        {
            object item = GetContainedItem(equipment, slotValue);
            string templateId = GetTemplateId(item);
            if (!WearableItemDescriptorRegistry.HasCapability(templateId, capability)) return new List<object>();
            return ReadContainers(item);
        }

        static void AppendUnique(List<object> target, List<object> source)
        {
            for (int i = 0; i < source.Count; i++) if (!target.Contains(source[i])) target.Add(source[i]);
        }

        static string GetTemplateId(object item)
        {
            if (item == null) return null;
            object stringTemplateId = ReflectionTools.ReadMember(item, "StringTemplateId");
            if (stringTemplateId is string direct && !string.IsNullOrEmpty(direct)) return direct;
            object templateId = ReflectionTools.ReadMember(item, "TemplateId");
            return templateId?.ToString();
        }

        static MethodInfo FindTarget(Type equipmentType)
        {
            Assembly assembly = equipmentType.Assembly;
            Type[] types = ReflectionTools.GetTypes(assembly);
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null) continue;
                MethodInfo[] methods;
                try { methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
                catch { continue; }
                for (int p = 0; p < methods.Length; p++)
                {
                    MethodInfo method = methods[p];
                    if (!string.Equals(method.Name, "GetPrioritizedContainersForLoot", StringComparison.Ordinal)) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length == 2 && parameters[0].ParameterType == equipmentType) return method;
                }
            }
            return null;
        }

        static LootItemKind InferKind(List<object> result, Dictionary<string, List<object>> groups, object item)
        {
            LootItemKind[] candidates = { LootItemKind.Money, LootItemKind.Throwable, LootItemKind.Other, LootItemKind.Magazine };
            for (int i = 0; i < candidates.Length; i++)
            {
                string[] order = LootPriorityPlan.Build(candidates[i], false);
                if (SequenceMatches(result, groups, order))
                {
                    if (candidates[i] == LootItemKind.Magazine && LooksLikeAmmo(item)) return LootItemKind.Ammo;
                    return candidates[i];
                }
            }
            return LooksLikeAmmo(item) ? LootItemKind.Ammo : LootItemKind.Other;
        }

        static bool SequenceMatches(List<object> actual, Dictionary<string, List<object>> groups, string[] order)
        {
            int index = 0;
            for (int i = 0; i < order.Length; i++)
            {
                List<object> source = groups[order[i]];
                for (int p = 0; p < source.Count; p++)
                {
                    if (index >= actual.Count || !ReferenceEquals(actual[index], source[p])) return false;
                    index++;
                }
            }
            return index == actual.Count;
        }

        static bool LooksLikeAmmo(object item)
        {
            if (item == null) return false;
            for (Type type = item.GetType(); type != null; type = type.BaseType)
            {
                string name = type.Name;
                if (name.IndexOf("Ammo", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (name.IndexOf("Magazine", StringComparison.OrdinalIgnoreCase) >= 0) return false;
            }
            object template = ReflectionTools.ReadMember(item, "Template");
            if (template != null)
            {
                object ammoType = ReflectionTools.ReadMember(template, "AmmoType");
                object caliber = ReflectionTools.ReadMember(template, "Caliber");
                if (ammoType != null || caliber != null) return true;
            }
            return false;
        }

        static List<object> ReadSlotContainers(object equipment, string slotName)
        {
            object enumValue = Enum.Parse(armBandValue.GetType(), slotName);
            return ReadContainers(GetContainedItem(equipment, enumValue));
        }

        static object GetContainedItem(object equipment, object slotValue)
        {
            if (equipment == null || slotValue == null || getSlot == null) return null;
            object slot = getSlot(equipment, slotValue);
            return ReflectionTools.ReadMember(slot, "ContainedItem");
        }

        static List<object> ReadContainers(object item)
        {
            var result = new List<object>();
            if (item == null) return result;
            AddEnumerable(result, ReflectionTools.ReadMember(item, "Containers"));
            if (result.Count == 0) AddEnumerable(result, ReflectionTools.ReadMember(item, "Grids"));
            return result;
        }

        static void AddEnumerable(List<object> target, object value)
        {
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null || value is string) return;
            foreach (object entry in enumerable)
                if (entry != null && containerType.IsInstanceOfType(entry)) target.Add(entry);
        }

        static List<object> ToObjects(object value)
        {
            var result = new List<object>();
            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null) return result;
            foreach (object entry in enumerable) result.Add(entry);
            return result;
        }

        static Func<object, object, object> BuildBinaryObjectCall(Type ownerType, Type argumentType, MethodInfo method)
        {
            try
            {
                DynamicMethod dm = new DynamicMethod("BAndHBLootGetSlot", typeof(object), new[] { typeof(object), typeof(object) }, typeof(LootPriorityRuntime), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Castclass, ownerType);
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

        static Func<IList> BuildListFactory(Type elementType)
        {
            try
            {
                Type listType = typeof(List<>).MakeGenericType(elementType);
                ConstructorInfo ctor = listType.GetConstructor(Type.EmptyTypes);
                if (ctor == null) return null;
                DynamicMethod dm = new DynamicMethod("BAndHBLootListFactory", typeof(IList), Type.EmptyTypes, typeof(LootPriorityRuntime), true);
                ILGenerator il = dm.GetILGenerator();
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Ret);
                return (Func<IList>)dm.CreateDelegate(typeof(Func<IList>));
            }
            catch { return null; }
        }

        static void Patch(object harmony, MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != harmonyMethodType) continue;
                if (string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
            }
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
