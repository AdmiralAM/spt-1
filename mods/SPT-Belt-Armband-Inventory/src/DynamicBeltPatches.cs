using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class BeltRuntime
    {
        internal static BeltSlotPosition Position;
        internal static FieldInfo DefaultTemplateField;
        internal static FieldInfo SlotViewsContainerField;
        internal static FieldInfo SlotViewsDictionaryField;
        internal static MethodInfo GetSlotMethod;
        internal static MethodInfo SlotViewShowMethod;
        internal static MethodInfo IsAllowedToSeeSlotMethod;
        internal static PropertyInfo ItemUiContextInstanceProperty;
        internal static object ArmBandValue;
        internal static object PocketsValue;
        internal static object BackpackValue;
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;

        internal static void AddBeltRow(object panel, object[] arguments)
        {
            if (panel == null || arguments == null || GetSlotMethod == null || ArmBandValue == null) return;

            try
            {
                object equipment = FindArgument(arguments, GetSlotMethod.DeclaringType);
                if (equipment == null) return;

                object armBandSlot = GetSlotMethod.Invoke(equipment, new[] { ArmBandValue });
                object item = ReflectionTools.ReadMember(armBandSlot, "ContainedItem");
                if (!BeltSlotPlan.ShouldExposeBelt(item != null, ReflectionTools.HasContainers(item))) return;

                object inventoryController = FindArgumentForMethod(arguments, IsAllowedToSeeSlotMethod);
                if (inventoryController != null && IsAllowedToSeeSlotMethod != null)
                {
                    object allowed = IsAllowedToSeeSlotMethod.Invoke(inventoryController, new[] { armBandSlot, ArmBandValue });
                    if (allowed is bool && !(bool)allowed) return;
                }

                IDictionary views = SlotViewsDictionaryField.GetValue(panel) as IDictionary;
                if (views == null)
                {
                    LogWarning?.Invoke("B&A&HB UI: ContainersPanel SlotView registry was unavailable after vanilla Show.");
                    return;
                }

                if (views.Contains(ArmBandValue))
                {
                    LogWarning?.Invoke("B&A&HB UI: ContainersPanel already contains an ArmBand projection; refusing to overwrite another mod's row.");
                    return;
                }

                UnityEngine.Object template = DefaultTemplateField.GetValue(panel) as UnityEngine.Object;
                Transform container = SlotViewsContainerField.GetValue(panel) as Transform;
                if (template == null || container == null)
                {
                    LogWarning?.Invoke("B&A&HB UI: default container SlotView template or row parent is unavailable.");
                    return;
                }

                UnityEngine.Object clone = UnityEngine.Object.Instantiate(template);
                Component beltView = clone as Component;
                if (beltView == null)
                {
                    UnityEngine.Object.DestroyImmediate(clone);
                    LogWarning?.Invoke("B&A&HB UI: cloned container template is not a Component/SlotView.");
                    return;
                }

                bool registered = false;
                try
                {
                    beltView.transform.SetParent(container, false);
                    object[] showArguments = BuildSlotViewShowArguments(armBandSlot, arguments);
                    if (showArguments == null)
                    {
                        LogWarning?.Invoke("B&A&HB UI: SlotView.Show arguments could not be resolved from the active ContainersPanel.Show call.");
                        return;
                    }

                    SlotViewShowMethod.Invoke(beltView, showArguments);
                    TrySetHeaderText(beltView, "BELT");
                    beltView.gameObject.name = "BELT Slot";
                    PlaceBeltRow(views, beltView.transform);

                    views.Add(ArmBandValue, beltView);
                    registered = true;

                    LogRows(views);
                    LogInfo?.Invoke("B&A&HB UI PROOF: separate BELT SlotView created, type-bound to the active ContainersPanel context, parented and registered; normal Gear Panel ArmBand was not modified.");
                }
                finally
                {
                    if (!registered)
                    {
                        TryCloseSlotView(beltView);
                        UnityEngine.Object.DestroyImmediate(beltView.gameObject);
                    }
                }
            }
            catch (Exception exception)
            {
                LogWarning?.Invoke("B&A&HB UI: could not create separate BELT row: " + Unwrap(exception).Message);
            }
        }

        static object[] BuildSlotViewShowArguments(object armBandSlot, object[] panelArguments)
        {
            if (SlotViewShowMethod == null || armBandSlot == null || panelArguments == null) return null;

            ParameterInfo[] parameters = SlotViewShowMethod.GetParameters();
            if (parameters.Length == 0) return null;

            object[] result = new object[parameters.Length];
            bool[] used = new bool[panelArguments.Length];
            object itemUiContext = ItemUiContextInstanceProperty == null ? null : ItemUiContextInstanceProperty.GetValue(null, null);
            bool inRaid = FindBooleanArgument(panelArguments);

            for (int i = 0; i < parameters.Length; i++)
            {
                Type targetType = parameters[i].ParameterType;
                if (i == 0 && targetType.IsInstanceOfType(armBandSlot))
                {
                    result[i] = armBandSlot;
                    continue;
                }

                if (targetType == typeof(bool))
                {
                    result[i] = !inRaid;
                    continue;
                }

                if (itemUiContext != null && targetType.IsInstanceOfType(itemUiContext))
                {
                    result[i] = itemUiContext;
                    continue;
                }

                int sourceIndex = FindCompatibleArgument(panelArguments, used, targetType);
                if (sourceIndex >= 0)
                {
                    result[i] = panelArguments[sourceIndex];
                    used[sourceIndex] = true;
                    continue;
                }

                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                {
                    LogWarning?.Invoke("B&A&HB UI: unresolved required SlotView.Show parameter " + parameters[i].Name + " (" + targetType.FullName + ").");
                    return null;
                }

                result[i] = null;
            }

            LogShowBinding(parameters, result);
            return result;
        }

        static int FindCompatibleArgument(object[] arguments, bool[] used, Type targetType)
        {
            for (int i = 0; i < arguments.Length; i++)
            {
                if (used[i]) continue;
                object value = arguments[i];
                if (value == null) continue;
                if (targetType.IsInstanceOfType(value)) return i;
            }
            return -1;
        }

        static void LogShowBinding(ParameterInfo[] parameters, object[] values)
        {
            if (LogInfo == null) return;
            string summary = "B&A&HB UI BIND:";
            for (int i = 0; i < parameters.Length; i++)
            {
                object value = values[i];
                summary += " " + parameters[i].Name + "=" + (value == null ? "<null>" : value.GetType().Name);
            }
            LogInfo(summary);
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

        static object FindArgumentForMethod(object[] arguments, MethodInfo method)
        {
            return method == null ? null : FindArgument(arguments, method.DeclaringType);
        }

        static bool FindBooleanArgument(object[] arguments)
        {
            for (int i = arguments.Length - 1; i >= 0; i--)
                if (arguments[i] is bool) return (bool)arguments[i];
            return false;
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

        static void LogRows(IDictionary views)
        {
            if (LogInfo == null) return;
            foreach (DictionaryEntry entry in views)
            {
                Component view = entry.Value as Component;
                if (view == null) continue;
                object slot = ReflectionTools.ReadMember(view, "Slot");
                object slotId = ReflectionTools.ReadMember(slot, "ID");
                object item = ReflectionTools.ReadMember(slot, "ContainedItem");
                string parent = view.transform.parent == null ? "<null>" : view.transform.parent.name;
                LogInfo("B&A&HB UI ROW: key=" + entry.Key
                    + ", view=" + view.GetType().FullName
                    + ", slot=" + (slotId ?? "<null>")
                    + ", parent=" + parent
                    + ", containerItem=" + (item != null && ReflectionTools.HasContainers(item)));
            }
        }

        static void TryCloseSlotView(Component slotView)
        {
            try
            {
                MethodInfo close = slotView.GetType().GetMethod("Close", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                close?.Invoke(slotView, null);
            }
            catch { }
        }

        internal static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
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
                Type itemUiContextType = ReflectionTools.FindType("EFT.UI.ItemUiContext");
                if (harmonyType == null || harmonyMethodType == null || panelType == null || equipmentType == null || slotEnumType == null || slotViewType == null || itemUiContextType == null)
                    return Fail("SPT 4.1 inventory UI types or Harmony were not found; BELT presentation is disabled.");

                MethodInfo panelShow = FindPanelShow(panelType, equipmentType);
                MethodInfo getSlot = equipmentType.GetMethod("GetSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { slotEnumType }, null);
                FieldInfo defaultTemplate = FindField(panelType, "_defaultSlotTemplate", slotViewType);
                FieldInfo slotViewsContainer = FindField(panelType, "_slotViewsContainer", typeof(Transform));
                FieldInfo slotViewsDictionary = FindSlotViewsDictionary(panelType, slotEnumType, slotViewType);
                MethodInfo slotViewShow = FindSlotViewShow(slotViewType);
                PropertyInfo itemUiContextInstance = itemUiContextType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo isAllowed = FindIsAllowedToSeeSlot(panelShow);

                if (panelShow == null || getSlot == null || defaultTemplate == null || slotViewsContainer == null || slotViewsDictionary == null || slotViewShow == null || itemUiContextInstance == null)
                    return Fail("SPT 4.1 ContainersPanel lifecycle boundary changed; separate BELT row was not installed.");

                BeltRuntime.Position = position;
                BeltRuntime.DefaultTemplateField = defaultTemplate;
                BeltRuntime.SlotViewsContainerField = slotViewsContainer;
                BeltRuntime.SlotViewsDictionaryField = slotViewsDictionary;
                BeltRuntime.GetSlotMethod = getSlot;
                BeltRuntime.SlotViewShowMethod = slotViewShow;
                BeltRuntime.IsAllowedToSeeSlotMethod = isAllowed;
                BeltRuntime.ItemUiContextInstanceProperty = itemUiContextInstance;
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

                object postfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PanelShowPostfix)) });
                Patch(patchMethod, harmonyMethodType, panelShow, postfix);

                logInfo?.Invoke("B&A&HB MOD SPT: separate ContainersPanel BELT projection installed; real inventory host remains ArmBand.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("BELT presentation installation failed safely: " + BeltRuntime.Unwrap(exception).Message);
            }
        }

        static void PanelShowPostfix(object __instance, object[] __args)
        {
            BeltRuntime.AddBeltRow(__instance, __args);
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

        static MethodInfo FindSlotViewShow(Type slotViewType)
        {
            foreach (MethodInfo method in slotViewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name != "Show") continue;
                ParameterInfo[] p = method.GetParameters();
                if (p.Length == 7 && string.Equals(p[0].ParameterType.Name, "Slot", StringComparison.Ordinal)) return method;
            }
            return null;
        }

        static MethodInfo FindIsAllowedToSeeSlot(MethodInfo panelShow)
        {
            if (panelShow == null) return null;
            ParameterInfo[] parameters = panelShow.GetParameters();
            Type controllerType = null;
            for (int i = 0; i < parameters.Length; i++)
                if (parameters[i].ParameterType.Name.IndexOf("InventoryController", StringComparison.OrdinalIgnoreCase) >= 0) controllerType = parameters[i].ParameterType;
            if (controllerType == null) return null;

            foreach (MethodInfo method in controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (!string.Equals(method.Name, "IsAllowedToSeeSlot", StringComparison.Ordinal)) continue;
                if (method.GetParameters().Length == 2) return method;
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
                for (int i = 1; i < parameters.Length; i++)
                    if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
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
            BeltRuntime.DefaultTemplateField = null;
            BeltRuntime.SlotViewsContainerField = null;
            BeltRuntime.SlotViewsDictionaryField = null;
            BeltRuntime.GetSlotMethod = null;
            BeltRuntime.SlotViewShowMethod = null;
            BeltRuntime.IsAllowedToSeeSlotMethod = null;
            BeltRuntime.ItemUiContextInstanceProperty = null;
            BeltRuntime.ArmBandValue = null;
            BeltRuntime.PocketsValue = null;
            BeltRuntime.BackpackValue = null;
            BeltRuntime.LogInfo = null;
            BeltRuntime.LogWarning = null;
        }
    }
}
