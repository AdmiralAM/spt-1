using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class BeltRuntime
    {
        internal static BeltSlotPosition Position;
        internal static FieldInfo SlotOrderField;
        internal static FieldInfo DefaultTemplateField;
        internal static FieldInfo DogtagTemplateField;
        internal static MethodInfo GetSlotMethod;
        internal static object ArmBandValue;
        internal static Action<string> LogWarning;

        internal static bool ShouldExpose(object[] arguments)
        {
            if (arguments == null || GetSlotMethod == null || ArmBandValue == null) return false;
            for (int i = 0; i < arguments.Length; i++)
            {
                object argument = arguments[i];
                if (argument == null || !GetSlotMethod.DeclaringType.IsInstanceOfType(argument)) continue;
                try
                {
                    object slot = GetSlotMethod.Invoke(argument, new[] { ArmBandValue });
                    object item = ReflectionTools.ReadMember(slot, "ContainedItem");
                    return BeltSlotPlan.ShouldExposeBelt(item != null, ReflectionTools.HasContainers(item));
                }
                catch (Exception exception)
                {
                    if (LogWarning != null) LogWarning("Could not inspect ArmBand item: " + exception.Message);
                    return false;
                }
            }
            return false;
        }

        internal static Array BuildOrder(Array current, bool expose)
        {
            var names = new List<string>(current.Length);
            for (int i = 0; i < current.Length; i++) names.Add(current.GetValue(i).ToString());
            string[] planned = BeltSlotPlan.Build(names, Position, expose);
            Type elementType = current.GetType().GetElementType();
            Array result = Array.CreateInstance(elementType, planned.Length);
            for (int i = 0; i < planned.Length; i++) result.SetValue(Enum.Parse(elementType, planned[i]), i);
            return result;
        }
    }

    internal sealed class DynamicBeltPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.runtime";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal DynamicBeltPatches(Action<string> logInfo, Action<string> logWarning)
        {
            this.logInfo = logInfo;
            this.logWarning = logWarning;
        }

        internal bool TryInstall(BeltSlotPosition position)
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type panelType = ReflectionTools.FindType("EFT.UI.ContainersPanel");
                Type equipmentType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type slotEnumType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                if (harmonyType == null || harmonyMethodType == null || panelType == null || equipmentType == null || slotEnumType == null)
                    return Fail("SPT 4.1 inventory UI types or Harmony were not found; the mod remains disabled.");

                FieldInfo orderField = FindSlotOrderField(panelType, slotEnumType);
                MethodInfo panelShow = FindPanelShow(panelType, equipmentType);
                MethodInfo slotFactory = FindSlotFactory(panelType, slotEnumType);
                FieldInfo defaultTemplate = FindTemplateField(panelType, "default", slotFactory == null ? null : slotFactory.ReturnType);
                FieldInfo dogtagTemplate = FindTemplateField(panelType, "dogtag", slotFactory == null ? null : slotFactory.ReturnType);
                MethodInfo getSlot = equipmentType.GetMethod("GetSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { slotEnumType }, null);
                if (orderField == null || panelShow == null || slotFactory == null || defaultTemplate == null || dogtagTemplate == null || getSlot == null)
                    return Fail("SPT 4.1 ContainersPanel shape changed; no partial patch was installed.");

                BeltRuntime.Position = position;
                BeltRuntime.SlotOrderField = orderField;
                BeltRuntime.DefaultTemplateField = defaultTemplate;
                BeltRuntime.DogtagTemplateField = dogtagTemplate;
                BeltRuntime.GetSlotMethod = getSlot;
                BeltRuntime.ArmBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand);
                BeltRuntime.LogWarning = logWarning;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmony == null || patchMethod == null || harmonyMethodConstructor == null)
                    return Fail("Harmony patch API is incompatible; the mod remains disabled.");

                object panelPrefix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PanelShowPrefix)) });
                object panelFinalizer = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PanelShowFinalizer)) });
                Patch(patchMethod, harmonyMethodType, panelShow, panelPrefix, null, panelFinalizer);

                object factoryPrefix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(SlotFactoryPrefix)) });
                object factoryFinalizer = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(SlotFactoryFinalizer)) });
                Patch(patchMethod, harmonyMethodType, slotFactory, factoryPrefix, null, factoryFinalizer);

                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (logInfo != null) logInfo("Belt/Armband Inventory Phase 1 installed on SPT 4.1 ContainersPanel.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Belt/Armband Inventory patch installation failed safely: " + exception.Message);
            }
        }

        static void PanelShowPrefix(object[] __args, ref object __state)
        {
            try
            {
                Array current = BeltRuntime.SlotOrderField.GetValue(null) as Array;
                __state = current;
                if (current == null) return;
                BeltRuntime.SlotOrderField.SetValue(null, BeltRuntime.BuildOrder(current, BeltRuntime.ShouldExpose(__args)));
            }
            catch (Exception exception)
            {
                if (BeltRuntime.LogWarning != null) BeltRuntime.LogWarning("Could not prepare belt row: " + exception.Message);
            }
        }

        static Exception PanelShowFinalizer(Exception __exception, object __state)
        {
            try
            {
                Array original = __state as Array;
                if (original != null) BeltRuntime.SlotOrderField.SetValue(null, original);
            }
            catch (Exception exception)
            {
                if (BeltRuntime.LogWarning != null) BeltRuntime.LogWarning("Could not restore container slot order: " + exception.Message);
            }
            return __exception;
        }

        static void SlotFactoryPrefix(object[] __args, object __instance, ref object __state)
        {
            if (__args == null || __args.Length == 0 || !string.Equals(__args[0].ToString(), BeltSlotPlan.ArmBand, StringComparison.Ordinal)) return;
            try
            {
                object dogtagTemplate = BeltRuntime.DogtagTemplateField.GetValue(__instance);
                object defaultTemplate = BeltRuntime.DefaultTemplateField.GetValue(__instance);
                __state = dogtagTemplate;
                if (defaultTemplate != null) BeltRuntime.DogtagTemplateField.SetValue(__instance, defaultTemplate);
            }
            catch (Exception exception)
            {
                if (BeltRuntime.LogWarning != null) BeltRuntime.LogWarning("Could not select the container slot template: " + exception.Message);
            }
        }

        static Exception SlotFactoryFinalizer(Exception __exception, object __instance, object __state)
        {
            if (__state == null) return __exception;
            try { BeltRuntime.DogtagTemplateField.SetValue(__instance, __state); }
            catch (Exception exception)
            {
                if (BeltRuntime.LogWarning != null) BeltRuntime.LogWarning("Could not restore the dogtag slot template: " + exception.Message);
            }
            return __exception;
        }

        static FieldInfo FindSlotOrderField(Type panelType, Type enumType)
        {
            FieldInfo[] fields = panelType.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!field.FieldType.IsArray || field.FieldType.GetElementType() != enumType) continue;
                Array value = field.GetValue(null) as Array;
                if (value == null) continue;
                var names = new List<string>(value.Length);
                for (int p = 0; p < value.Length; p++) names.Add(value.GetValue(p).ToString());
                if (BeltSlotPlan.IsExpectedContainerPanelOrder(names)) return field;
            }
            return null;
        }

        static MethodInfo FindPanelShow(Type panelType, Type equipmentType)
        {
            MethodInfo[] methods = panelType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method.Name != "Show") continue;
                ParameterInfo[] parameters = method.GetParameters();
                for (int p = 0; p < parameters.Length; p++) if (parameters[p].ParameterType == equipmentType) return method;
            }
            return null;
        }

        static MethodInfo FindSlotFactory(Type panelType, Type slotEnumType)
        {
            MethodInfo[] methods = panelType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == slotEnumType && method.ReturnType.Name.IndexOf("SlotView", StringComparison.OrdinalIgnoreCase) >= 0) return method;
            }
            return null;
        }

        static FieldInfo FindTemplateField(Type panelType, string nameHint, Type templateType)
        {
            if (templateType == null) return null;
            FieldInfo[] fields = panelType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (!templateType.IsAssignableFrom(field.FieldType)) continue;
                if (field.Name.IndexOf(nameHint, StringComparison.OrdinalIgnoreCase) >= 0) return field;
            }
            return null;
        }

        static MethodInfo Method(string name)
        {
            return typeof(DynamicBeltPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
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
                bool hasPrefix = false;
                bool hasFinalizer = false;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != harmonyMethodType) continue;
                    if (string.Equals(parameters[p].Name, "prefix", StringComparison.OrdinalIgnoreCase)) hasPrefix = true;
                    if (string.Equals(parameters[p].Name, "finalizer", StringComparison.OrdinalIgnoreCase)) hasFinalizer = true;
                }
                if (hasPrefix && hasFinalizer) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object prefix, object postfix, object finalizer)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != harmonyMethodType) continue;
                if (string.Equals(parameters[i].Name, "prefix", StringComparison.OrdinalIgnoreCase)) arguments[i] = prefix;
                else if (string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
                else if (string.Equals(parameters[i].Name, "finalizer", StringComparison.OrdinalIgnoreCase)) arguments[i] = finalizer;
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
            BeltRuntime.SlotOrderField = null;
            BeltRuntime.DefaultTemplateField = null;
            BeltRuntime.DogtagTemplateField = null;
            BeltRuntime.GetSlotMethod = null;
            BeltRuntime.ArmBandValue = null;
            BeltRuntime.LogWarning = null;
        }
    }
}
