using System;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class BeltContainersPanelProjectionRuntime
    {
        internal static Type EquipmentSlotType;
        internal static FieldInfo DefaultTemplateField;
        internal static FieldInfo DogtagTemplateField;
        internal static Action<string> LogWarning;

        internal static bool IsDedicatedBelt(object value)
        {
            if (value == null || EquipmentSlotType == null || !EquipmentSlotType.IsInstanceOfType(value)) return false;
            return Convert.ToInt32(value) == RuntimeIdentity.DedicatedBeltEquipmentSlotValue;
        }

        internal static void Reset()
        {
            EquipmentSlotType = null;
            DefaultTemplateField = null;
            DogtagTemplateField = null;
            LogWarning = null;
        }
    }

    /// <summary>
    /// Bridges the dedicated pseudo-enum Belt slot into EFT's native ContainersPanel
    /// row factory. The canonical slot-order array is owned by
    /// DedicatedEquipmentSlotPatches; this class only makes pseudo-slot 15 render
    /// through the same native container-row prefab used by ordinary equipment
    /// containers. No custom IMGUI or parallel inventory UI is introduced.
    /// </summary>
    internal sealed class BeltContainersPanelProjectionPatches : IDisposable
    {
        sealed class TemplateMutationState
        {
            internal readonly object OriginalDogtag;
            internal readonly object InstalledDefault;

            internal TemplateMutationState(object originalDogtag, object installedDefault)
            {
                OriginalDogtag = originalDogtag;
                InstalledDefault = installedDefault;
            }
        }

        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.belt-containers-panel";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal BeltContainersPanelProjectionPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type panelType = ReflectionTools.FindType("EFT.UI.ContainersPanel");
                Type equipmentSlotType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                if (harmonyType == null || harmonyMethodType == null || panelType == null || equipmentSlotType == null)
                    return Fail("Belt ContainersPanel projection boundary was not found.");

                MethodInfo slotFactory = FindSlotFactory(panelType, equipmentSlotType);
                if (slotFactory == null)
                    return Fail("ContainersPanel native slot factory was not found exactly; dedicated Belt row was not projected.");

                FieldInfo defaultTemplate = FindTemplateField(panelType, "default", slotFactory.ReturnType);
                FieldInfo dogtagTemplate = FindTemplateField(panelType, "dogtag", slotFactory.ReturnType);
                if (defaultTemplate == null || dogtagTemplate == null)
                    return Fail("ContainersPanel native row templates were not found exactly; dedicated Belt row was not projected.");

                BeltContainersPanelProjectionRuntime.EquipmentSlotType = equipmentSlotType;
                BeltContainersPanelProjectionRuntime.DefaultTemplateField = defaultTemplate;
                BeltContainersPanelProjectionRuntime.DogtagTemplateField = dogtagTemplate;
                BeltContainersPanelProjectionRuntime.LogWarning = logWarning;

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (patchMethod == null || harmonyMethodConstructor == null || unpatchSelf == null)
                    return Fail("Harmony prefix/finalizer API incompatible with Belt ContainersPanel projection.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object prefix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(SlotFactoryPrefix)) });
                object finalizer = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(SlotFactoryFinalizer)) });
                Patch(patchMethod, harmonyMethodType, slotFactory, prefix, finalizer);

                logInfo?.Invoke("B&A&HB #2 MOD SPT Belt ContainersPanel projection installed: dedicated Belt pseudo-slot 15 uses the native default container-row template.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Belt ContainersPanel projection failed safely: " + Unwrap(exception).GetType().FullName + ": " + Unwrap(exception).Message);
            }
        }

        static void SlotFactoryPrefix(object[] __args, object __instance, ref object __state)
        {
            if (__args == null || __args.Length != 1 || !BeltContainersPanelProjectionRuntime.IsDedicatedBelt(__args[0])) return;
            try
            {
                object defaultTemplate = BeltContainersPanelProjectionRuntime.DefaultTemplateField.GetValue(__instance);
                if (defaultTemplate == null) return;
                object dogtagTemplate = BeltContainersPanelProjectionRuntime.DogtagTemplateField.GetValue(__instance);
                __state = new TemplateMutationState(dogtagTemplate, defaultTemplate);
                BeltContainersPanelProjectionRuntime.DogtagTemplateField.SetValue(__instance, defaultTemplate);
            }
            catch (Exception exception)
            {
                BeltContainersPanelProjectionRuntime.LogWarning?.Invoke("Could not prepare native Belt row template: " + Unwrap(exception).Message);
            }
        }

        static Exception SlotFactoryFinalizer(Exception __exception, object __instance, object __state)
        {
            try
            {
                TemplateMutationState state = __state as TemplateMutationState;
                if (state == null) return __exception;
                object current = BeltContainersPanelProjectionRuntime.DogtagTemplateField.GetValue(__instance);
                if (RuntimeMutationPolicy.ShouldRestore(current, state.InstalledDefault))
                    BeltContainersPanelProjectionRuntime.DogtagTemplateField.SetValue(__instance, state.OriginalDogtag);
            }
            catch (Exception exception)
            {
                BeltContainersPanelProjectionRuntime.LogWarning?.Invoke("Could not restore ContainersPanel dogtag row template: " + Unwrap(exception).Message);
            }
            return __exception;
        }

        static MethodInfo FindSlotFactory(Type panelType, Type slotEnumType)
        {
            MethodInfo[] methods = panelType.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != 1 || parameters[0].ParameterType != slotEnumType) continue;
                if (method.ReturnType == typeof(void)) continue;
                if (method.ReturnType.Name.IndexOf("SlotView", StringComparison.OrdinalIgnoreCase) >= 0) return method;
            }
            return null;
        }

        static FieldInfo FindTemplateField(Type panelType, string nameHint, Type templateType)
        {
            if (templateType == null) return null;
            for (Type current = panelType; current != null; current = current.BaseType)
            {
                FieldInfo[] fields = current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (!templateType.IsAssignableFrom(field.FieldType)) continue;
                    if (field.Name.IndexOf(nameHint, StringComparison.OrdinalIgnoreCase) >= 0) return field;
                }
            }
            return null;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            MethodInfo[] methods = harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Patch", StringComparison.Ordinal)) continue;
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

        static void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object prefix, object finalizer)
        {
            object harmony = null;
            throw new InvalidOperationException("Static Patch overload should never be called.");
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object prefix, object finalizer)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] args = new object[parameters.Length];
            args[0] = original;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != harmonyMethodType) continue;
                if (string.Equals(parameters[i].Name, "prefix", StringComparison.OrdinalIgnoreCase)) args[i] = prefix;
                else if (string.Equals(parameters[i].Name, "finalizer", StringComparison.OrdinalIgnoreCase)) args[i] = finalizer;
            }
            patchMethod.Invoke(harmony, args);
        }

        static MethodInfo Method(string name)
        {
            return typeof(BeltContainersPanelProjectionPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        }

        static MethodInfo FindZeroArgInstanceMethod(Type type, string name)
        {
            MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
                if (string.Equals(methods[i].Name, name, StringComparison.Ordinal) && methods[i].GetParameters().Length == 0) return methods[i];
            return null;
        }

        static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation && invocation.InnerException != null) current = invocation.InnerException;
            return current;
        }

        bool Fail(string message)
        {
            logWarning?.Invoke(message);
            return false;
        }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            BeltContainersPanelProjectionRuntime.Reset();
        }
    }
}
