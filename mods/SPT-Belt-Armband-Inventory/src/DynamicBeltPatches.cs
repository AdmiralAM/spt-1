using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class BeltRuntime
    {
        sealed class PanelShowState
        {
            internal object Panel;
            internal Array OriginalSlots;
            internal bool SlotsChanged;
            internal bool Completed;
        }

        internal static BeltSlotPosition Position;
        internal static FieldInfo DefaultTemplateField;
        internal static FieldInfo EquipmentSlotsField;
        internal static FieldInfo SlotViewsContainerField;
        internal static FieldInfo SlotViewsDictionaryField;
        internal static MethodInfo GetSlotMethod;
        internal static object ArmBandValue;
        internal static object PocketsValue;
        internal static object BackpackValue;
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;

        static object activePanel;
        static Component trackedClone;

        internal static object BeginPanelShow(object panel, object[] arguments)
        {
            if (panel == null || arguments == null || GetSlotMethod == null || ArmBandValue == null || EquipmentSlotsField == null) return null;

            try
            {
                object equipment = FindArgument(arguments, GetSlotMethod.DeclaringType);
                if (equipment == null) return null;

                object armBandSlot = GetSlotMethod.Invoke(equipment, new[] { ArmBandValue });
                object item = ReflectionTools.ReadMember(armBandSlot, "ContainedItem");
                if (!BeltSlotPlan.ShouldExposeBelt(item != null, ReflectionTools.HasContainers(item))) return null;

                Array original = EquipmentSlotsField.GetValue(null) as Array;
                if (original == null)
                {
                    LogWarning?.Invoke("B&A&HB UI: ContainersPanel equipment-slot sequence was unavailable; BELT row was not projected.");
                    return null;
                }

                PanelShowState state = new PanelShowState
                {
                    Panel = panel,
                    OriginalSlots = original,
                    SlotsChanged = false,
                    Completed = false
                };

                if (!Contains(original, ArmBandValue))
                {
                    Array projected = InsertArmBand(original);
                    if (projected == null)
                    {
                        LogWarning?.Invoke("B&A&HB UI: could not construct temporary ContainersPanel slot sequence; BELT row was not projected.");
                        return null;
                    }

                    EquipmentSlotsField.SetValue(null, projected);
                    state.SlotsChanged = true;
                }

                activePanel = panel;
                trackedClone = null;
                return state;
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB UI: could not prepare ContainersPanel BELT projection: " + Unwrap(exception).Message);
                activePanel = null;
                trackedClone = null;
                return null;
            }
        }

        internal static object TryCreateArmBandSlotView(object panel, object slotName)
        {
            if (panel == null || slotName == null || ArmBandValue == null || DefaultTemplateField == null) return null;
            if (!ReferenceEquals(panel, activePanel) || !slotName.Equals(ArmBandValue)) return null;

            try
            {
                UnityEngine.Object template = DefaultTemplateField.GetValue(panel) as UnityEngine.Object;
                if (template == null)
                {
                    LogWarning?.Invoke("B&A&HB UI: default ContainersPanel SlotView template was unavailable for ArmBand.");
                    return null;
                }

                UnityEngine.Object clone = UnityEngine.Object.Instantiate(template);
                trackedClone = clone as Component;
                if (trackedClone == null)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                    LogWarning?.Invoke("B&A&HB UI: cloned default container template is not a SlotView component.");
                    return null;
                }

                return clone;
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB UI: ArmBand slot factory interception failed safely: " + Unwrap(exception).Message);
                trackedClone = null;
                return null;
            }
        }

        internal static void CompletePanelShow(object panel, object stateObject)
        {
            PanelShowState state = stateObject as PanelShowState;
            if (state == null || state.Completed) return;

            try
            {
                if (!ReferenceEquals(panel, state.Panel) || !ReferenceEquals(panel, activePanel)) return;
                try
                {
                    ValidateAndLabelBeltRow(panel);
                }
                catch (Exception exception)
                {
                    LogWarning?.Invoke("B&A&HB UI: BELT row was created through the native ContainersPanel lifecycle, but post-Show validation/decoration failed safely: " + Unwrap(exception).Message);
                }
            }
            finally
            {
                RestorePanelShow(state);
            }
        }

        internal static void RestorePanelShow(object stateObject)
        {
            PanelShowState state = stateObject as PanelShowState;
            if (state == null || state.Completed) return;

            try
            {
                if (state.SlotsChanged && EquipmentSlotsField != null && state.OriginalSlots != null)
                    EquipmentSlotsField.SetValue(null, state.OriginalSlots);
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB UI: could not restore vanilla ContainersPanel slot sequence: " + Unwrap(exception).Message);
            }
            finally
            {
                state.Completed = true;
                if (ReferenceEquals(activePanel, state.Panel)) activePanel = null;
                trackedClone = null;
            }
        }

        static void ValidateAndLabelBeltRow(object panel)
        {
            if (trackedClone == null || SlotViewsDictionaryField == null) return;

            IDictionary views = SlotViewsDictionaryField.GetValue(panel) as IDictionary;
            if (views == null || !views.Contains(ArmBandValue))
            {
                LogWarning?.Invoke("B&A&HB UI: ContainersPanel did not register the intercepted ArmBand SlotView.");
                return;
            }

            Component registered = views[ArmBandValue] as Component;
            if (!ReferenceEquals(registered, trackedClone))
            {
                LogWarning?.Invoke("B&A&HB UI: ArmBand SlotView registry entry was not the tracked default-template clone; leaving it untouched.");
                return;
            }

            object slot = ReflectionTools.ReadMember(registered, "Slot");
            object slotId = ReflectionTools.ReadMember(slot, "ID");
            object item = ReflectionTools.ReadMember(slot, "ContainedItem");
            if (slot == null || slotId == null || !string.Equals(slotId.ToString(), BeltSlotPlan.ArmBand, StringComparison.Ordinal)
                || !BeltSlotPlan.ShouldExposeBelt(item != null, ReflectionTools.HasContainers(item)))
            {
                LogWarning?.Invoke("B&A&HB UI: intercepted SlotView was not bound to a container ArmBand slot; leaving it untouched.");
                return;
            }

            Transform expectedParent = SlotViewsContainerField == null ? null : SlotViewsContainerField.GetValue(panel) as Transform;
            if (expectedParent != null && registered.transform.parent != expectedParent)
            {
                LogWarning?.Invoke("B&A&HB UI: intercepted ArmBand SlotView is outside the active ContainersPanel row parent; leaving layout untouched.");
                return;
            }

            TrySetHeaderText(registered, "BELT");
            registered.gameObject.name = "BELT Slot";
            PlaceBeltRow(views, registered.transform);
            LogInfo?.Invoke("B&A&HB UI PROOF: ContainersPanel created, bound and registered the BELT row through its native SlotView lifecycle; only the ArmBand slot template selection was intercepted.");
        }

        static Array InsertArmBand(Array source)
        {
            Type elementType = source.GetType().GetElementType();
            if (elementType == null || !elementType.IsInstanceOfType(ArmBandValue)) return null;

            int anchor = IndexOf(source, Position == BeltSlotPosition.AbovePockets ? PocketsValue : BackpackValue);
            if (anchor < 0) anchor = source.Length;
            if (Position == BeltSlotPosition.BelowPockets)
            {
                int pockets = IndexOf(source, PocketsValue);
                if (pockets >= 0) anchor = pockets + 1;
            }

            Array result = Array.CreateInstance(elementType, source.Length + 1);
            int target = 0;
            for (int i = 0; i < source.Length + 1; i++)
            {
                if (i == anchor) result.SetValue(ArmBandValue, i);
                else result.SetValue(source.GetValue(target++), i);
            }
            return result;
        }

        static bool Contains(Array source, object value) => IndexOf(source, value) >= 0;

        static int IndexOf(Array source, object value)
        {
            if (source == null || value == null) return -1;
            for (int i = 0; i < source.Length; i++)
            {
                object current = source.GetValue(i);
                if (current != null && current.Equals(value)) return i;
            }
            return -1;
        }

        static object FindArgument(object[] arguments, Type type)
        {
            if (type == null) return null;
            for (int i = 0; i < arguments.Length; i++)
            {
                object value = arguments[i];
                if (value != null && type.IsInstanceOfType(value)) return value;
            }
            return null;
        }

        static void PlaceBeltRow(IDictionary views, Transform belt)
        {
            object anchorKey = Position == BeltSlotPosition.AbovePockets ? PocketsValue : BackpackValue;
            Component anchor = anchorKey == null ? null : views[anchorKey] as Component;
            if (anchor == null) return;
            belt.SetSiblingIndex(anchor.transform.GetSiblingIndex());
        }

        static void TrySetHeaderText(Component slotView, string text)
        {
            if (slotView == null) return;
            FieldInfo headerField = slotView.GetType().GetField("_headerText", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            object header = headerField == null ? null : headerField.GetValue(slotView);
            if (header == null) return;
            PropertyInfo textProperty = header.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public);
            if (textProperty != null && textProperty.CanWrite) textProperty.SetValue(header, text, null);
        }

        internal static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
        }

        internal static void Reset()
        {
            activePanel = null;
            trackedClone = null;
            DefaultTemplateField = null;
            EquipmentSlotsField = null;
            SlotViewsContainerField = null;
            SlotViewsDictionaryField = null;
            GetSlotMethod = null;
            ArmBandValue = null;
            PocketsValue = null;
            BackpackValue = null;
            LogInfo = null;
            LogWarning = null;
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
                Type slotViewType = ReflectionTools.FindType("EFT.UI.DragAndDrop.SlotView");
                if (harmonyType == null || harmonyMethodType == null || panelType == null || equipmentType == null || slotEnumType == null || slotViewType == null)
                    return Fail("SPT 4.1 inventory UI types or Harmony were not found; BELT presentation is disabled.");

                MethodInfo panelShow = FindPanelShow(panelType, equipmentType);
                MethodInfo slotFactory = FindSlotFactory(panelType, slotEnumType, slotViewType);
                MethodInfo getSlot = equipmentType.GetMethod("GetSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { slotEnumType }, null);
                FieldInfo defaultTemplate = FindField(panelType, "_defaultSlotTemplate", slotViewType);
                FieldInfo equipmentSlots = FindEquipmentSlotsField(panelType, slotEnumType);
                FieldInfo slotViewsContainer = FindField(panelType, "_slotViewsContainer", typeof(Transform));
                FieldInfo slotViewsDictionary = FindSlotViewsDictionary(panelType, slotEnumType, slotViewType);

                if (panelShow == null || slotFactory == null || getSlot == null || defaultTemplate == null || equipmentSlots == null || slotViewsDictionary == null)
                    return Fail("SPT 4.1 ContainersPanel lifecycle boundary changed; BELT projection was not installed.");

                BeltRuntime.Position = position;
                BeltRuntime.DefaultTemplateField = defaultTemplate;
                BeltRuntime.EquipmentSlotsField = equipmentSlots;
                BeltRuntime.SlotViewsContainerField = slotViewsContainer;
                BeltRuntime.SlotViewsDictionaryField = slotViewsDictionary;
                BeltRuntime.GetSlotMethod = getSlot;
                BeltRuntime.ArmBandValue = Enum.Parse(slotEnumType, BeltSlotPlan.ArmBand);
                BeltRuntime.PocketsValue = Enum.Parse(slotEnumType, BeltSlotPlan.Pockets);
                BeltRuntime.BackpackValue = Enum.Parse(slotEnumType, BeltSlotPlan.Backpack);
                BeltRuntime.LogInfo = logInfo;
                BeltRuntime.LogWarning = logWarning;

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; BELT presentation is disabled.");
                unpatchSelf = rollback;

                MethodInfo factoryPrefix = BuildFactoryPrefix(panelType, slotEnumType, slotViewType);
                object factoryPrefixPatch = harmonyMethodConstructor.Invoke(new object[] { factoryPrefix });
                Patch(patchMethod, harmonyMethodType, slotFactory, factoryPrefixPatch, null, null);

                object showPrefix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PanelShowPrefix)) });
                object showPostfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PanelShowPostfix)) });
                object showFinalizer = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PanelShowFinalizer)) });
                Patch(patchMethod, harmonyMethodType, panelShow, showPrefix, showPostfix, showFinalizer);

                logInfo?.Invoke("B&A&HB MOD SPT: ContainersPanel-native BELT projection installed; real inventory host remains ArmBand.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("BELT presentation installation failed safely: " + BeltRuntime.Unwrap(exception).Message);
            }
        }

        static void PanelShowPrefix(object __instance, object[] __args, out object __state)
        {
            __state = BeltRuntime.BeginPanelShow(__instance, __args);
        }

        static void PanelShowPostfix(object __instance, object __state)
        {
            BeltRuntime.CompletePanelShow(__instance, __state);
        }

        static Exception PanelShowFinalizer(Exception __exception, object __state)
        {
            BeltRuntime.RestorePanelShow(__state);
            return __exception;
        }

        static MethodInfo BuildFactoryPrefix(Type panelType, Type slotEnumType, Type slotViewType)
        {
            DynamicMethod method = new DynamicMethod(
                "BeltArmBandSlotFactoryPrefix",
                typeof(bool),
                new[] { panelType, slotEnumType, slotViewType.MakeByRefType() },
                typeof(DynamicBeltPatches),
                true);
            method.DefineParameter(1, ParameterAttributes.None, "__instance");
            method.DefineParameter(2, ParameterAttributes.None, "__0");
            method.DefineParameter(3, ParameterAttributes.None, "__result");

            ILGenerator il = method.GetILGenerator();
            LocalBuilder clone = il.DeclareLocal(typeof(object));
            Label runOriginal = il.DefineLabel();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_1);
            if (slotEnumType.IsValueType) il.Emit(OpCodes.Box, slotEnumType);
            il.Emit(OpCodes.Call, typeof(BeltRuntime).GetMethod(nameof(BeltRuntime.TryCreateArmBandSlotView), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public));
            il.Emit(OpCodes.Stloc, clone);
            il.Emit(OpCodes.Ldloc, clone);
            il.Emit(OpCodes.Brfalse_S, runOriginal);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldloc, clone);
            il.Emit(OpCodes.Castclass, slotViewType);
            il.Emit(OpCodes.Stind_Ref);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);

            il.MarkLabel(runOriginal);
            il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Ret);
            return method;
        }

        static MethodInfo FindPanelShow(Type panelType, Type equipmentType)
        {
            foreach (MethodInfo method in panelType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "Show") continue;
                ParameterInfo[] parameters = method.GetParameters();
                for (int i = 0; i < parameters.Length; i++)
                    if (parameters[i].ParameterType == equipmentType) return method;
            }
            return null;
        }

        static MethodInfo FindSlotFactory(Type panelType, Type slotEnumType, Type slotViewType)
        {
            MethodInfo match = null;
            foreach (MethodInfo method in panelType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != slotEnumType) continue;
                if (!slotViewType.IsAssignableFrom(method.ReturnType)) continue;
                if (match != null) return null;
                match = method;
            }
            return match;
        }

        static FieldInfo FindEquipmentSlotsField(Type panelType, Type slotEnumType)
        {
            FieldInfo exact = panelType.GetField("equipmentSlot_0", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (exact != null && exact.FieldType.IsArray && exact.FieldType.GetElementType() == slotEnumType) return exact;

            FieldInfo match = null;
            foreach (FieldInfo field in panelType.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!field.FieldType.IsArray || field.FieldType.GetElementType() != slotEnumType) continue;
                if (match != null) return null;
                match = field;
            }
            return match;
        }

        static FieldInfo FindField(Type owner, string exactName, Type fieldType)
        {
            FieldInfo field = owner.GetField(exactName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && fieldType.IsAssignableFrom(field.FieldType) ? field : null;
        }

        static FieldInfo FindSlotViewsDictionary(Type panelType, Type slotEnumType, Type slotViewType)
        {
            foreach (FieldInfo field in panelType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                Type type = field.FieldType;
                if (!type.IsGenericType) continue;
                Type[] args = type.GetGenericArguments();
                if (args.Length == 2 && args[0] == slotEnumType && slotViewType.IsAssignableFrom(args[1])) return field;
            }
            return null;
        }

        static MethodInfo Method(string name) => typeof(DynamicBeltPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (method.Name != "Patch") continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length < 2 || !typeof(MethodBase).IsAssignableFrom(parameters[0].ParameterType)) continue;
                bool prefix = false;
                bool postfix = false;
                bool finalizer = false;
                for (int i = 1; i < parameters.Length; i++)
                {
                    if (parameters[i].ParameterType != harmonyMethodType) continue;
                    if (string.Equals(parameters[i].Name, "prefix", StringComparison.OrdinalIgnoreCase)) prefix = true;
                    else if (string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) postfix = true;
                    else if (string.Equals(parameters[i].Name, "finalizer", StringComparison.OrdinalIgnoreCase)) finalizer = true;
                }
                if (prefix && postfix && finalizer) return method;
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
            logWarning?.Invoke(message);
            return false;
        }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); }
            catch { }
            harmony = null;
            unpatchSelf = null;
            BeltRuntime.Reset();
        }
    }
}
