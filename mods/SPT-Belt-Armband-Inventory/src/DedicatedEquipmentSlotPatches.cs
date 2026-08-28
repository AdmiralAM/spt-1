using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class DedicatedEquipmentSlotRuntime
    {
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Type EquipmentSlotType;
        internal static FieldInfo EquipmentTabSlotViewsField;
        internal static FieldInfo EquipmentTabHeadwearField;
        internal static bool HeadBandCloneCandidateLogged;

        internal static object BeltSlotKey => Enum.ToObject(EquipmentSlotType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue);
        internal static object HeadBandSlotKey => Enum.ToObject(EquipmentSlotType, RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);

        internal static bool ValidatePseudoSlotBoundary()
        {
            if (EquipmentSlotType == null || !EquipmentSlotType.IsEnum) return false;
            object armBand = Enum.Parse(EquipmentSlotType, "ArmBand", false);
            int armBandValue = Convert.ToInt32(armBand);
            Array values = Enum.GetValues(EquipmentSlotType);
            int max = int.MinValue;
            for (int i = 0; i < values.Length; i++) max = Math.Max(max, Convert.ToInt32(values.GetValue(i)));
            return armBandValue == 14
                && max == 14
                && RuntimeIdentity.DedicatedBeltEquipmentSlotValue == max + 1
                && RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue == max + 2;
        }

        internal static void InstallHeadBandView(object equipmentTab)
        {
            if (equipmentTab == null || EquipmentTabSlotViewsField == null || EquipmentTabHeadwearField == null) return;
            try
            {
                IDictionary slotViews = EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;
                if (slotViews == null) return;
                object key = HeadBandSlotKey;
                if (slotViews.Contains(key)) return;

                Component headwear = EquipmentTabHeadwearField.GetValue(equipmentTab) as Component;
                if (headwear == null || headwear.transform == null || headwear.transform.parent == null)
                    throw new InvalidOperationException("EquipmentTab headwear SlotView/proper parent unavailable");

                Component clone = UnityEngine.Object.Instantiate(headwear);
                clone.gameObject.name = "B&A&HB HeadBand Slot";
                clone.transform.SetParent(headwear.transform.parent, false);
                clone.transform.SetSiblingIndex(headwear.transform.GetSiblingIndex());
                slotViews.Add(key, clone);

                if (!HeadBandCloneCandidateLogged)
                {
                    HeadBandCloneCandidateLogged = true;
                    LogInfo?.Invoke("B&A&HB HEADBAND VIEW CANDIDATE: pseudo-slot16 view cloned before Headwear and registered in EquipmentTab map; acceptance requires the native SlotView.Show bind proof.");
                }
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB dedicated HeadBand EquipmentTab projection failed closed: " + Unwrap(exception).Message);
            }
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        internal static void Reset()
        {
            LogInfo = null;
            LogWarning = null;
            EquipmentSlotType = null;
            EquipmentTabSlotViewsField = null;
            EquipmentTabHeadwearField = null;
            HeadBandCloneCandidateLogged = false;
        }
    }

    internal sealed class DedicatedEquipmentSlotPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.dedicated-equipment-slots";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;
        FieldInfo containersOrderField;
        Array originalContainersOrder;
        Array installedContainersOrder;

        internal DedicatedEquipmentSlotPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type containersPanelType = ReflectionTools.FindType("EFT.UI.ContainersPanel");
                Type equipmentTabType = ReflectionTools.FindType("EFT.UI.EquipmentTab");
                Type slotViewType = ReflectionTools.FindType("EFT.UI.DragAndDrop.SlotView");
                if (harmonyType == null || harmonyMethodType == null || equipmentSlotType == null || containersPanelType == null || equipmentTabType == null || slotViewType == null)
                    return Fail("SPT 4.1 dedicated equipment-slot client boundary was not found.");

                DedicatedEquipmentSlotRuntime.EquipmentSlotType = equipmentSlotType;
                DedicatedEquipmentSlotRuntime.LogInfo = logInfo;
                DedicatedEquipmentSlotRuntime.LogWarning = logWarning;
                if (!DedicatedEquipmentSlotRuntime.ValidatePseudoSlotBoundary())
                    return Fail("EquipmentSlot enum no longer ends at ArmBand=14; pseudo-slot values 15/16 refused to avoid collision.");

                containersOrderField = FindContainersOrderField(containersPanelType, equipmentSlotType);
                if (containersOrderField == null)
                    return Fail("ContainersPanel canonical slot-order array was not found exactly; Belt UI projection refused.");
                originalContainersOrder = containersOrderField.GetValue(null) as Array;
                installedContainersOrder = BuildBeltOrder(originalContainersOrder, equipmentSlotType);
                containersOrderField.SetValue(null, installedContainersOrder);

                DedicatedEquipmentSlotRuntime.EquipmentTabSlotViewsField = FindFieldInHierarchy(equipmentTabType, "_slotViews", typeof(IDictionary));
                DedicatedEquipmentSlotRuntime.EquipmentTabHeadwearField = FindFieldInHierarchy(equipmentTabType, "_headwearSlot", slotViewType);
                MethodInfo awake = FindZeroArgInstanceMethod(equipmentTabType, "Awake");
                if (DedicatedEquipmentSlotRuntime.EquipmentTabSlotViewsField == null
                    || DedicatedEquipmentSlotRuntime.EquipmentTabHeadwearField == null
                    || awake == null)
                {
                    RestoreContainersOrder();
                    return Fail("EquipmentTab exact fields changed; HeadBand view candidate projection refused.");
                }

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (patchMethod == null || harmonyMethodConstructor == null || unpatchSelf == null)
                {
                    RestoreContainersOrder();
                    return Fail("Harmony patch API incompatible with dedicated-slot projection.");
                }

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object awakePostfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(EquipmentTabAwakePostfixFactory)) });
                Patch(patchMethod, harmonyMethodType, awake, awakePostfix);

                logInfo?.Invoke("B&A&HB #2 MOD SPT dedicated client slot projection installed: Belt pseudo-slot15 after Pockets; HeadBand pseudo-slot16 view candidate before Headwear. Native binding/captions are owned by the dedicated presentation path.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Dedicated equipment-slot installation failed safely: " + Unwrap(exception).GetType().FullName + ": " + Unwrap(exception).Message);
            }
        }

        static FieldInfo FindContainersOrderField(Type panelType, Type equipmentSlotType)
        {
            FieldInfo[] fields = panelType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.FieldType.IsArray || field.FieldType.GetElementType() != equipmentSlotType) continue;
                Array value = field.GetValue(null) as Array;
                if (value == null || value.Length != 5) continue;
                string[] expected = { "TacticalVest", "Pockets", "Backpack", "SecuredContainer", "Dogtag" };
                bool match = true;
                for (int p = 0; p < expected.Length; p++)
                    if (!string.Equals(value.GetValue(p)?.ToString(), expected[p], StringComparison.Ordinal)) { match = false; break; }
                if (match) return field;
            }
            return null;
        }

        static Array BuildBeltOrder(Array source, Type equipmentSlotType)
        {
            if (source == null || source.Length != 5) throw new InvalidOperationException("canonical ContainersPanel order unavailable");
            Array result = Array.CreateInstance(equipmentSlotType, source.Length + 1);
            int target = 0;
            for (int i = 0; i < source.Length; i++)
            {
                object value = source.GetValue(i);
                result.SetValue(value, target++);
                if (string.Equals(value?.ToString(), "Pockets", StringComparison.Ordinal))
                    result.SetValue(DedicatedEquipmentSlotRuntime.BeltSlotKey, target++);
            }
            if (target != result.Length) throw new InvalidOperationException("Pockets anchor missing from canonical ContainersPanel order");
            return result;
        }

        static FieldInfo FindFieldInHierarchy(Type type, string name, Type assignableType)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                if (field != null && (assignableType.IsAssignableFrom(field.FieldType) || field.FieldType.IsAssignableFrom(assignableType))) return field;
            }
            return null;
        }

        static MethodInfo EquipmentTabAwakePostfixFactory(MethodBase original)
        {
            MethodInfo method = original as MethodInfo;
            if (method == null || method.DeclaringType == null) return null;
            DynamicMethod postfix = new DynamicMethod("BAndHBEquipmentTabAwakePostfix", typeof(void), new[] { method.DeclaringType }, typeof(DedicatedEquipmentSlotPatches), true);
            postfix.DefineParameter(1, ParameterAttributes.None, "__instance");
            ILGenerator il = postfix.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(DedicatedEquipmentSlotRuntime).GetMethod(nameof(DedicatedEquipmentSlotRuntime.InstallHeadBandView), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Ret);
            return postfix;
        }

        static MethodInfo Method(string name)
        {
            MethodInfo[] methods = typeof(DedicatedEquipmentSlotPatches).GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
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
            object[] args = new object[parameters.Length];
            args[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) args[i] = postfix;
            patchMethod.Invoke(harmony, args);
        }

        void RestoreContainersOrder()
        {
            try
            {
                if (containersOrderField != null && installedContainersOrder != null && ReferenceEquals(containersOrderField.GetValue(null), installedContainersOrder))
                    containersOrderField.SetValue(null, originalContainersOrder);
            }
            catch { }
            containersOrderField = null;
            originalContainersOrder = null;
            installedContainersOrder = null;
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
            RestoreContainersOrder();
            DedicatedEquipmentSlotRuntime.Reset();
        }
    }
}
