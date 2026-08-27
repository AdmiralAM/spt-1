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
            return value != null
                && EquipmentSlotType != null
                && EquipmentSlotType.IsInstanceOfType(value)
                && Convert.ToInt32(value) == RuntimeIdentity.DedicatedBeltEquipmentSlotValue;
        }

        internal static void Reset()
        {
            EquipmentSlotType = null;
            DefaultTemplateField = null;
            DogtagTemplateField = null;
            LogWarning = null;
        }
    }

    internal sealed class BeltContainersPanelProjectionPatches : IDisposable
    {
        sealed class MutationState
        {
            internal readonly object Original;
            internal readonly object Installed;
            internal MutationState(object original, object installed) { Original = original; Installed = installed; }
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
                    return Fail("ContainersPanel native slot factory was not found exactly.");

                FieldInfo defaultTemplate = FindTemplateField(panelType, "default", slotFactory.ReturnType);
                FieldInfo dogtagTemplate = FindTemplateField(panelType, "dogtag", slotFactory.ReturnType);
                if (defaultTemplate == null || dogtagTemplate == null)
                    return Fail("ContainersPanel native row templates were not found exactly.");

                BeltContainersPanelProjectionRuntime.EquipmentSlotType = equipmentSlotType;
                BeltContainersPanelProjectionRuntime.DefaultTemplateField = defaultTemplate;
                BeltContainersPanelProjectionRuntime.DogtagTemplateField = dogtagTemplate;
                BeltContainersPanelProjectionRuntime.LogWarning = logWarning;

                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = FindZeroArgInstanceMethod(harmonyType, "UnpatchSelf");
                if (patchMethod == null || harmonyMethodCtor == null || unpatchSelf == null)
                    return Fail("Harmony prefix/finalizer API incompatible with Belt ContainersPanel projection.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                object prefix = harmonyMethodCtor.Invoke(new object[] { Method(nameof(SlotFactoryPrefix)) });
                object finalizer = harmonyMethodCtor.Invoke(new object[] { Method(nameof(SlotFactoryFinalizer)) });
                Patch(patchMethod, harmonyMethodType, slotFactory, prefix, finalizer);

                logInfo?.Invoke("B&A&HB #2 MOD SPT Belt ContainersPanel bridge installed: dedicated Belt pseudo-slot 15 uses EFT native container-row presentation.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                Exception root = Unwrap(exception);
                return Fail("Belt ContainersPanel projection failed safely: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static void SlotFactoryPrefix(object[] __args, object __instance, ref object __state)
        {
            if (__args == null || __args.Length != 1 || !BeltContainersPanelProjectionRuntime.IsDedicatedBelt(__args[0])) return;
            try
            {
                object installed = BeltContainersPanelProjectionRuntime.DefaultTemplateField.GetValue(__instance);
                if (installed == null) return;
                object original = BeltContainersPanelProjectionRuntime.DogtagTemplateField.GetValue(__instance);
                __state = new MutationState(original, installed);
                BeltContainersPanelProjectionRuntime.DogtagTemplateField.SetValue(__instance, installed);
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
                MutationState state = __state as MutationState;
                if (state == null) return __exception;
                object current = BeltContainersPanelProjectionRuntime.DogtagTemplateField.GetValue(__instance);
                if (RuntimeMutationPolicy.ShouldRestore(current, state.Installed))
                    BeltContainersPanelProjectionRuntime.DogtagTemplateField.SetValue(__instance, state.Original);
            }
            catch (Exception exception)
            {
                BeltContainersPanelProjectionRuntime.LogWarning?.Invoke("Could not restore ContainersPanel dogtag template: " + Unwrap(exception).Message);
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
                if (parameters.Length != 1 || parameters[0].ParameterType != slotEnumType || method.ReturnType == typeof(void)) continue;
                if (method.ReturnType.Name.IndexOf("SlotView", StringComparison.OrdinalIgnoreCase) >= 0) return method;
            }
            return null;
        }

        static FieldInfo FindTemplateField(Type panelType, string hint, Type templateType)
        {
            if (templateType == null) return null;
            for (Type current = panelType; current != null; current = current.BaseType)
            {
                FieldInfo[] fields = current.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                    if (templateType.IsAssignableFrom(fields[i].FieldType)
                        && fields[i].Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                        return fields[i];
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
                bool prefix = false, finalizer = false;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != harmonyMethodType) continue;
                    if (string.Equals(parameters[p].Name, "prefix", StringComparison.OrdinalIgnoreCase)) prefix = true;
                    if (string.Equals(parameters[p].Name, "finalizer", StringComparison.OrdinalIgnoreCase)) finalizer = true;
                }
                if (prefix && finalizer) return method;
            }
            return null;
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

        static MethodInfo Method(string name) => typeof(BeltContainersPanelProjectionPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);

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

        bool Fail(string message) { logWarning?.Invoke(message); return false; }

        public void Dispose()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
            harmony = null;
            unpatchSelf = null;
            BeltContainersPanelProjectionRuntime.Reset();
        }
    }
}
