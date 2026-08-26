using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace SPTBeltArmbandInventory
{
    internal static class HarmonyInstallPolicy
    {
        internal static bool CanBegin(bool harmonyReady, bool patchApiReady, bool methodConstructorReady, bool rollbackReady)
        {
            return harmonyReady && patchApiReady && methodConstructorReady && rollbackReady;
        }
    }

    internal static class FastAccessBeltSyncPolicy
    {
        internal static bool ShouldQueue(bool succeeded, bool ownerMatches, bool armBandRoute, bool hasContainers)
        {
            return succeeded
                && ownerMatches
                && armBandRoute
                && AccessoryCapabilityPolicy.CanUse(
                    AccessoryCategory.ArmBand,
                    AccessoryCapability.FastAccess,
                    true,
                    hasContainers);
        }

        internal static bool ShouldClearSelected(bool isRemoval, bool selectedBelongsToRemovedBelt)
        {
            return isRemoval && selectedBelongsToRemovedBelt;
        }
    }

    internal static class FastAccessBeltSyncRuntime
    {
        sealed class PendingRefresh
        {
            internal readonly WeakReference View;
            internal object RemovedContainer;

            internal PendingRefresh(object view, object removedContainer)
            {
                View = new WeakReference(view);
                RemovedContainer = removedContainer;
            }
        }

        internal static Action<string> LogWarning;
        internal static Action RequestFlush;
        internal static FieldInfo ControllerField;
        internal static FieldInfo ItemUiContextField;
        internal static MethodInfo ShowMethod;
        internal static PropertyInfo TopPriorityGrenadeProperty;
        internal static MethodInfo GetTopLevelItemsMethod;
        static readonly List<PendingRefresh> PendingViews = new List<PendingRefresh>();

        internal static bool HasPending
        {
            get { return PendingViews.Count != 0; }
        }

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
                    object pendingView = PendingViews[i].View.Target;
                    if (pendingView == null)
                    {
                        PendingViews.RemoveAt(i--);
                        continue;
                    }
                    if (!ReferenceEquals(pendingView, view)) continue;
                    if (!added) PendingViews[i].RemovedContainer = item;
                    RequestFlush?.Invoke();
                    return;
                }
                PendingViews.Add(new PendingRefresh(view, added ? null : item));
                RequestFlush?.Invoke();
            }
            catch (Exception exception)
            {
                Warn("Could not queue belt grenade fast-access refresh: " + Unwrap(exception).Message);
            }
        }

        internal static void Flush()
        {
            if (PendingViews.Count == 0) return;

            PendingRefresh[] pending = PendingViews.ToArray();
            PendingViews.Clear();
            if (ControllerField == null || ItemUiContextField == null || ShowMethod == null) return;
            for (int i = 0; i < pending.Length; i++)
            {
                PendingRefresh refresh = pending[i];
                object view = refresh.View.Target;
                if (view == null) continue;
                try
                {
                    object controller = ControllerField.GetValue(view);
                    object context = ItemUiContextField.GetValue(view);
                    if (controller == null || context == null) continue;

                    if (refresh.RemovedContainer != null) ClearRemovedBeltSelection(controller, refresh.RemovedContainer);
                    ShowMethod.Invoke(view, new[] { controller, context });
                }
                catch (Exception exception)
                {
                    Warn("Could not refresh grenade fast-access after belt equip/remove: " + Unwrap(exception).Message);
                }
            }
        }

        static void ClearRemovedBeltSelection(object controller, object removedContainer)
        {
            if (TopPriorityGrenadeProperty == null || !TopPriorityGrenadeProperty.CanRead || !TopPriorityGrenadeProperty.CanWrite || GetTopLevelItemsMethod == null) return;

            object inventory = ReflectionTools.ReadMember(controller, "Inventory");
            object equipment = ReflectionTools.ReadMember(inventory, "Equipment");
            object selectedOwner = ResolveSelectedGrenadeOwner(controller, inventory, equipment, TopPriorityGrenadeProperty);
            if (selectedOwner == null) return;

            object selected = TopPriorityGrenadeProperty.GetValue(selectedOwner, null);
            if (selected == null) return;

            IEnumerable items = GetTopLevelItemsMethod.Invoke(null, new[] { removedContainer }) as IEnumerable;
            bool belongs = ContainsReference(items, selected);
            if (FastAccessBeltSyncPolicy.ShouldClearSelected(true, belongs)) TopPriorityGrenadeProperty.SetValue(selectedOwner, null, null);
        }

        static object ResolveSelectedGrenadeOwner(object controller, object inventory, object equipment, PropertyInfo property)
        {
            Type declaringType = property == null ? null : property.DeclaringType;
            if (declaringType == null) return null;
            if (equipment != null && declaringType.IsInstanceOfType(equipment)) return equipment;
            if (inventory != null && declaringType.IsInstanceOfType(inventory)) return inventory;
            if (controller != null && declaringType.IsInstanceOfType(controller)) return controller;
            return null;
        }

        static bool ContainsReference(IEnumerable items, object target)
        {
            if (items == null || target == null) return false;
            foreach (object item in items)
            {
                if (ReferenceEquals(item, target)) return true;
            }
            return false;
        }

        internal static void Reset()
        {
            PendingViews.Clear();
            LogWarning = null;
            RequestFlush = null;
            ControllerField = null;
            ItemUiContextField = null;
            ShowMethod = null;
            TopPriorityGrenadeProperty = null;
            GetTopLevelItemsMethod = null;
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
                Type equipmentType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                if (harmonyType == null || harmonyMethodType == null || viewType == null || equipmentType == null)
                    return Fail("SPT 4.1 FastAccessGrenadeItemView/InventoryEquipment or Harmony was not found; live belt grenade synchronization is disabled.");

                MethodInfo added = FindHandler(viewType, "IAddHandler", "OnItemAdded");
                MethodInfo removed = FindHandler(viewType, "IRemoveHandler", "OnItemRemoved");
                MethodInfo show = FindShowMethod(viewType);
                ParameterInfo[] showParameters = show == null ? Array.Empty<ParameterInfo>() : show.GetParameters();
                FieldInfo controller = showParameters.Length == 2 ? FindField(viewType, showParameters[0].ParameterType, "InventoryController") : null;
                FieldInfo context = showParameters.Length == 2 ? FindField(viewType, showParameters[1].ParameterType, "ItemUiContext") : null;
                PropertyInfo topPriority = FindTopPriorityGrenadeProperty(viewType.Assembly, equipmentType);
                MethodInfo topLevelItems = FindTopLevelItemsMethod(viewType.Assembly);
                if (added == null || removed == null || show == null || controller == null || context == null)
                    return Fail("SPT 4.1 grenade fast-access boundary is incomplete; live belt grenade synchronization is disabled."
                        + " added=" + Describe(added)
                        + ", removed=" + Describe(removed)
                        + ", show=" + Describe(show)
                        + ", controller=" + Describe(controller)
                        + ", context=" + Describe(context)
                        + ", topPriority=" + Describe(topPriority)
                        + ", topLevelItems=" + Describe(topLevelItems) + ".");

                if (topPriority == null || !topPriority.CanRead || !topPriority.CanWrite || topLevelItems == null)
                {
                    string degradedTopPriority = Describe(topPriority);
                    string degradedTopLevelItems = Describe(topLevelItems);
                    topPriority = null;
                    topLevelItems = null;
                    if (logWarning != null)
                        logWarning("SPT 4.1 grenade fast-access selected-grenade boundary was not found; live ArmBand refresh will install without selected grenade cleanup on removal."
                            + " topPriority=" + degradedTopPriority
                            + ", topLevelItems=" + degradedTopLevelItems + ".");
                }
                else if (logInfo != null)
                {
                    logInfo("B&A&HB selected grenade boundary resolved at " + Describe(topPriority) + ".");
                }

                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                MethodInfo patchMethod = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo harmonyMethodConstructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                MethodInfo rollback = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (!HarmonyInstallPolicy.CanBegin(harmony != null, patchMethod != null, harmonyMethodConstructor != null, rollback != null))
                    return Fail("Harmony patch/rollback API is incompatible; live belt grenade synchronization is disabled.");
                unpatchSelf = rollback;

                FastAccessBeltSyncRuntime.LogWarning = logWarning;
                FastAccessBeltSyncRuntime.ControllerField = controller;
                FastAccessBeltSyncRuntime.ItemUiContextField = context;
                FastAccessBeltSyncRuntime.ShowMethod = show;
                FastAccessBeltSyncRuntime.TopPriorityGrenadeProperty = topPriority;
                FastAccessBeltSyncRuntime.GetTopLevelItemsMethod = topLevelItems;

                object addPostfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(AddedPostfix)) });
                object removePostfix = harmonyMethodConstructor.Invoke(new object[] { Method(nameof(RemovedPostfix)) });
                Patch(patchMethod, harmonyMethodType, added, addPostfix);
                Patch(patchMethod, harmonyMethodType, removed, removePostfix);

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

        internal static MethodInfo FindHandler(Type viewType, string interfaceName, string methodName)
        {
            if (viewType == null || string.IsNullOrEmpty(interfaceName) || string.IsNullOrEmpty(methodName)) return null;

            Type[] interfaces;
            try { interfaces = viewType.GetInterfaces(); }
            catch { interfaces = Array.Empty<Type>(); }
            for (int i = 0; i < interfaces.Length; i++)
            {
                Type contract = interfaces[i];
                if (!string.Equals(contract.Name, interfaceName, StringComparison.Ordinal)
                    && !EndsWith(contract.FullName, "." + interfaceName)) continue;

                InterfaceMapping map;
                try { map = viewType.GetInterfaceMap(contract); }
                catch { continue; }
                for (int p = 0; p < map.InterfaceMethods.Length; p++)
                    if (EndsWith(map.InterfaceMethods[p].Name, methodName)) return map.TargetMethods[p];
            }

            MethodInfo[] methods;
            try { methods = viewType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
            catch { return null; }
            for (int i = 0; i < methods.Length; i++)
                if (EndsWith(methods[i].Name, methodName) && methods[i].GetParameters().Length > 0) return methods[i];
            return null;
        }

        static PropertyInfo FindTopPriorityGrenadeProperty(Assembly assembly, Type equipmentType)
        {
            PropertyInfo direct = FindProperty(equipmentType, "TopPriorityGrenade");
            if (IsUsableProperty(direct)) return direct;

            PropertyInfo only = null;
            int matches = 0;
            Type[] types = ReflectionTools.GetTypes(assembly);
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null) continue;
                PropertyInfo property;
                try { property = type.GetProperty("TopPriorityGrenade", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { continue; }
                if (!IsUsableProperty(property)) continue;
                if (equipmentType != null && property.DeclaringType != null && property.DeclaringType.IsAssignableFrom(equipmentType)) return property;
                only = property;
                matches++;
            }
            return matches == 1 ? only : null;
        }

        static bool IsUsableProperty(PropertyInfo property)
        {
            return property != null && property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0;
        }

        static MethodInfo FindTopLevelItemsMethod(Assembly assembly)
        {
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
                    if (!EndsWith(method.Name, "GetTopLevelItemsFromCollection")) continue;
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != 1 || !typeof(IEnumerable).IsAssignableFrom(method.ReturnType)) continue;
                    return method;
                }
            }
            return null;
        }

        internal static FieldInfo FindField(Type type, Type valueType, string preferredName)
        {
            FieldInfo match = null;
            int matches = 0;
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo[] fields;
                try { fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly); }
                catch { continue; }
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];
                    if (valueType == null || !valueType.IsAssignableFrom(field.FieldType)) continue;
                    if (string.Equals(field.Name, preferredName, StringComparison.Ordinal)
                        || field.Name.IndexOf(preferredName, StringComparison.Ordinal) >= 0) return field;
                    match = field;
                    matches++;
                }
            }
            return matches == 1 ? match : null;
        }

        static PropertyInfo FindProperty(Type type, string name)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (property != null) return property;
            }
            return null;
        }

        static bool EndsWith(string value, string suffix)
        {
            return value != null && value.EndsWith(suffix, StringComparison.Ordinal);
        }

        static string Describe(MemberInfo member)
        {
            return member == null ? "<missing>" : member.DeclaringType?.FullName + "." + member.Name;
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
