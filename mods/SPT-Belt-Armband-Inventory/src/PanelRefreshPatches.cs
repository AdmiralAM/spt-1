using System;
using System.Collections.Generic;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class PanelRefreshRuntime
    {
        sealed class PanelState
        {
            internal WeakReference Panel;
            internal object[] Arguments;
            internal bool Exposed;
        }

        static readonly List<PanelState> Panels = new List<PanelState>();
        internal static MethodInfo PanelShowMethod;
        internal static MethodInfo PanelCloseMethod;
        internal static Action<string> LogWarning;
        static bool pending;
        static bool refreshing;

        internal static void Track(object panel, object[] arguments)
        {
            if (panel == null || arguments == null) return;
            Forget(panel);
            Panels.Add(new PanelState
            {
                Panel = new WeakReference(panel),
                Arguments = (object[])arguments.Clone(),
                Exposed = BeltRuntime.ShouldExpose(arguments)
            });
        }

        internal static void Forget(object panel)
        {
            if (panel == null) return;
            for (int i = Panels.Count - 1; i >= 0; i--)
            {
                object target = Panels[i].Panel.Target;
                if (target == null || ReferenceEquals(target, panel)) Panels.RemoveAt(i);
            }
        }

        internal static void NotifySlotChanged(object slotView, object[] arguments)
        {
            if (refreshing || slotView == null || arguments == null || arguments.Length < 2) return;
            try
            {
                object eventArgs = arguments[1];
                object status = ReflectionTools.ReadMember(eventArgs, "Status");
                if (status == null || !string.Equals(status.ToString(), "Succeed", StringComparison.OrdinalIgnoreCase)) return;

                object slot = ReflectionTools.ReadMember(slotView, "Slot");
                object id = ReflectionTools.ReadMember(slot, "ID");
                if (id == null || !string.Equals(id.ToString(), BeltSlotPlan.ArmBand, StringComparison.Ordinal)) return;

                pending = true;
            }
            catch (Exception exception)
            {
                if (LogWarning != null) LogWarning("Could not observe ArmBand slot change: " + exception.Message);
            }
        }

        internal static void Flush()
        {
            if (!pending || refreshing || PanelShowMethod == null || PanelCloseMethod == null) return;
            pending = false;

            PanelState[] snapshot = Panels.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                PanelState state = snapshot[i];
                object panel = state.Panel.Target;
                if (panel == null)
                {
                    Panels.Remove(state);
                    continue;
                }

                bool now;
                try { now = BeltRuntime.ShouldExpose(state.Arguments); }
                catch { continue; }
                if (now == state.Exposed) continue;

                try
                {
                    refreshing = true;
                    PanelCloseMethod.Invoke(panel, null);
                    PanelShowMethod.Invoke(panel, state.Arguments);
                }
                catch (Exception exception)
                {
                    if (LogWarning != null) LogWarning("Could not refresh belt row after ArmBand change: " + Unwrap(exception).Message);
                }
                finally
                {
                    refreshing = false;
                }
            }
        }

        internal static void Reset()
        {
            Panels.Clear();
            PanelShowMethod = null;
            PanelCloseMethod = null;
            LogWarning = null;
            pending = false;
            refreshing = false;
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
        }
    }

    internal sealed class PanelRefreshPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.refresh";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal PanelRefreshPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type equipmentType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type slotViewType = ReflectionTools.FindType("EFT.UI.DragAndDrop.SlotView");
                if (harmonyType == null || harmonyMethodType == null || panelType == null || equipmentType == null || slotViewType == null)
                    return Fail("SPT 4.1 panel refresh types or Harmony were not found; dynamic belt-row refresh is disabled.");

                MethodInfo panelShow = FindPanelShow(panelType, equipmentType);
                MethodInfo panelClose = panelType.GetMethod("Close", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo addToSlot = FindSlotChangeMethod(slotViewType, "OnAddToSlot");
                MethodInfo removeFromSlot = FindSlotChangeMethod(slotViewType, "OnRemoveFromSlot");
                if (panelShow == null || panelClose == null || addToSlot == null || removeFromSlot == null)
                    return Fail("SPT 4.1 SlotView/ContainersPanel shape changed; dynamic belt-row refresh is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; dynamic belt-row refresh is disabled.");
                unpatchSelf = rollback;

                PanelRefreshRuntime.PanelShowMethod = panelShow;
                PanelRefreshRuntime.PanelCloseMethod = panelClose;
                PanelRefreshRuntime.LogWarning = logWarning;

                Patch(patchMethod, harmonyMethodType, panelShow, null,
                    harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PanelShowPostfix)) }));
                Patch(patchMethod, harmonyMethodType, panelClose, null,
                    harmonyMethodConstructor.Invoke(new object[] { Method(nameof(PanelClosePostfix)) }));
                Patch(patchMethod, harmonyMethodType, addToSlot, null,
                    harmonyMethodConstructor.Invoke(new object[] { Method(nameof(SlotChangedPostfix)) }));
                Patch(patchMethod, harmonyMethodType, removeFromSlot, null,
                    harmonyMethodConstructor.Invoke(new object[] { Method(nameof(SlotChangedPostfix)) }));

                if (logInfo != null) logInfo("Belt/Armband Inventory dynamic ArmBand refresh installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Dynamic belt-row refresh installation failed safely: " + exception.Message);
            }
        }

        static void PanelShowPostfix(object __instance, object[] __args)
        {
            PanelRefreshRuntime.Track(__instance, __args);
        }

        static void PanelClosePostfix(object __instance)
        {
            PanelRefreshRuntime.Forget(__instance);
        }

        static void SlotChangedPostfix(object __instance, object[] __args)
        {
            PanelRefreshRuntime.NotifySlotChanged(__instance, __args);
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

        static MethodInfo FindSlotChangeMethod(Type slotViewType, string name)
        {
            MethodInfo method = slotViewType.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return method != null && method.GetParameters().Length == 2 ? method : null;
        }

        static MethodInfo Method(string name)
        {
            return typeof(PanelRefreshPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
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
                bool hasPostfix = false;
                for (int p = 1; p < parameters.Length; p++)
                {
                    if (parameters[p].ParameterType != harmonyMethodType) continue;
                    if (string.Equals(parameters[p].Name, "prefix", StringComparison.OrdinalIgnoreCase)) hasPrefix = true;
                    if (string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) hasPostfix = true;
                }
                if (hasPrefix && hasPostfix) return method;
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object prefix, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType != harmonyMethodType) continue;
                if (string.Equals(parameters[i].Name, "prefix", StringComparison.OrdinalIgnoreCase)) arguments[i] = prefix;
                else if (string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
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
            PanelRefreshRuntime.Reset();
        }
    }
}
