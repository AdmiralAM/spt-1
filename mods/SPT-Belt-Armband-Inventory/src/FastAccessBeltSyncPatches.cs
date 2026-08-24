using System;
using System.Collections.Generic;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class FastAccessBeltSyncPolicy
    {
        internal static bool ShouldQueue(bool succeeded, bool ownerMatches, bool armBandRoute, bool hasContainers)
        {
            return succeeded && ownerMatches && armBandRoute && hasContainers;
        }
    }

    internal static class FastAccessBeltSyncRuntime
    {
        internal static Action<string> LogWarning;
        internal static FieldInfo ControllerField;
        internal static FieldInfo ItemUiContextField;
        internal static MethodInfo ShowMethod;
        static readonly List<object> PendingViews = new List<object>();

        internal static void Queue(object view, object eventArgs, bool added)
        {
            if (view == null || eventArgs == null || ControllerField == null || ShowMethod == null) return;

            try
            {
                object controller = ControllerField.GetValue(view);
                if (controller == null) return;

                object status = ReflectionTools.ReadMember(eventArgs, "Status");
                bool succeeded = status != null && string.Equals(status.ToString(), "Succeed", StringComparison.Ordinal);
                object ownerId = ReflectionTools.ReadMember(eventArgs, "OwnerId");
                object controllerId = ReflectionTools.ReadMember(controller, "ID");
                bool ownerMatches = ownerId != null && controllerId != null && Equals(ownerId, controllerId);

                object address = ReflectionTools.ReadMember(eventArgs, added ? "To" : "From");
                object slot = ReflectionTools.ReadMember(address, "Slot");
                object slotId = ReflectionTools.ReadMember(slot, "ID");
                bool armBandRoute = slotId != null && string.Equals(slotId.ToString(), BeltSlotPlan.ArmBand, StringComparison.Ordinal);

                object item = ReflectionTools.ReadMember(eventArgs, "Item");
                bool hasContainers = ReflectionTools.HasContainers(item);
                if (!FastAccessBeltSyncPolicy.ShouldQueue(succeeded, ownerMatches, armBandRoute, hasContainers)) return;

                for (int i = 0; i < PendingViews.Count; i++)
                {
                    if (ReferenceEquals(PendingViews[i], view)) return;
                }
                PendingViews.Add(view);
            }
            catch (Exception exception)
            {
                Warn("Could not queue belt grenade fast-access refresh: " + Unwrap(exception).Message);
            }
        }

        internal static void Flush()
        {
            if (PendingViews.Count == 0 || ControllerField == null || ItemUiContextField == null || ShowMethod == null) return;

            object[] views = PendingViews.ToArray();
            PendingViews.Clear();
            for (int i = 0; i < views.Length; i++)
            {
                object view = views[i];
                try
                {
                    object controller = ControllerField.GetValue(view);
                    object context = ItemUiContextField.GetValue(view);
                    if (controller == null || context == null) continue;
                    ShowMethod.Invoke(view, new[] { controller, context });
                }
                catch (Exception exception)
                {
                    Warn("Could not refresh grenade fast-access after belt equip/remove: " + Unwrap(exception).Message);
                }
            }
        }

        internal static void Reset()
        {
            PendingViews.Clear();
            LogWarning = null;
            ControllerField = null;
            ItemUiContextField = null;
            ShowMethod = null;
        }

        static void Warn(string message)
        {
            if (LogWarning != null) LogWarning(message);
        }

        static Exception Unwrap(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            return invocation != null && invocation.InnerException != null ? invocation.InnerException : exception;
        }
    }

    internal sealed class FastAccessBeltSyncPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.grenade-belt-events";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal FastAccessBeltSyncPatches(Action<string> logInfo, Action<string> logWarning)
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
                Type viewType = ReflectionTools.FindType("EFT.UI.DragAndDrop.FastAccessGrenadeItemView");
                if (harmonyType == null || harmonyMethodType == null || viewType == null)
                    return Fail("SPT 4.1 FastAccessGrenadeItemView or Harmony was not found; live belt grenade synchronization is disabled.");

                MethodInfo added = viewType.GetMethod("OnItemAdded", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo removed = viewType.GetMethod("OnItemRemoved", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                MethodInfo show = FindShowMethod(viewType);
                FieldInfo controller = FindField(viewType, "InventoryController");
                FieldInfo context = FindField(viewType, "ItemUiContext");
                if (added == null || removed == null || show == null || controller == null || context == null)
                    return Fail("SPT 4.1 grenade fast-access event shape changed; live belt grenade synchronization is disabled.");

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (harmony == null || patchMethod == null || harmonyMethodConstructor == null)
                    return Fail("Harmony patch API is incompatible; live belt grenade synchronization is disabled.");

                FastAccessBeltSyncRuntime.LogWarning = logWarning;
                FastAccessBeltSyncRuntime.ControllerField = controller;
                FastAccessBeltSyncRuntime.ItemUiContextField = context;
                FastAccessBeltSyncRuntime.ShowMethod = show;

                object addPostfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(AddedPostfix)) });
                object removePostfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(RemovedPostfix)) });
                Patch(patchMethod, harmonyMethodType, added, addPostfix);
                Patch(patchMethod, harmonyMethodType, removed, removePostfix);

                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (logInfo != null) logInfo("Belt/Armband Inventory live grenade fast-access synchronization installed.");
                return true;
            }
            catch (Exception exception)
            {
                Dispose();
                return Fail("Live belt grenade synchronization installation failed safely: " + exception.Message);
            }
        }

        static void AddedPostfix(object __instance, object[] __args)
        {
            if (__args == null || __args.Length == 0) return;
            FastAccessBeltSyncRuntime.Queue(__instance, __args[0], true);
        }

        static void RemovedPostfix(object __instance, object[] __args)
        {
            if (__args == null || __args.Length == 0) return;
            FastAccessBeltSyncRuntime.Queue(__instance, __args[0], false);
        }

        static MethodInfo FindShowMethod(Type viewType)
        {
            MethodInfo[] methods = viewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (!string.Equals(method.Name, "Show", StringComparison.Ordinal) || method.ReturnType != typeof(void)) continue;
                if (method.GetParameters().Length == 2) return method;
            }
            return null;
        }

        static FieldInfo FindField(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }
            return null;
        }

        static MethodInfo Method(string name)
        {
            return typeof(FastAccessBeltSyncPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
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
                {
                    if (parameters[p].ParameterType == harmonyMethodType && string.Equals(parameters[p].Name, "postfix", StringComparison.OrdinalIgnoreCase)) return method;
                }
            }
            return null;
        }

        void Patch(MethodInfo patchMethod, Type harmonyMethodType, MethodInfo original, object postfix)
        {
            ParameterInfo[] parameters = patchMethod.GetParameters();
            object[] arguments = new object[parameters.Length];
            arguments[0] = original;
            for (int i = 1; i < parameters.Length; i++)
            {
                if (parameters[i].ParameterType == harmonyMethodType && string.Equals(parameters[i].Name, "postfix", StringComparison.OrdinalIgnoreCase)) arguments[i] = postfix;
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
            FastAccessBeltSyncRuntime.Reset();
        }
    }
}
