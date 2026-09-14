using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace BAndHB.OperationalAccess
{
    [BepInPlugin(Id, "B&A&HB operational access hotfix", "0.3.0.1")]
    [BepInDependency(BaseId, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("com.cj.useFromAnywhere", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.admiralam.spt.belt-armband-inventory.operational-hotfix";
        public const string BaseId = "com.admiralam.spt.belt-armband-inventory";
        public const string RequiredBaseHash = "ca2774ba4fc6cc1183863b8f008916f40a78e2b517e4934344286e233c26453a";
        Harmony owner;
        void Start()
        {
            try
            {
                Assembly basis = Chainloader.PluginInfos[BaseId].Instance.GetType().Assembly;
                using (var stream = File.OpenRead(basis.Location))
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (hash != RequiredBaseHash) throw new InvalidOperationException("Unsupported base client SHA-256: " + hash);
                }
                AccessRuntime.Log = text => Logger.LogInfo(text);
                AccessRuntime.Warn = text => Logger.LogWarning(text);
                AccessRuntime.Bind(basis);
                owner = new Harmony(Id);
                AccessRuntime.Install(owner);
                Logger.LogInfo("B&A&HB operational.1 ACTIVE: equipped Belt slot15, nested grid contents, native-first candidates; original client/server/assets unchanged.");
            }
            catch (Exception error)
            {
                if (owner != null) owner.UnpatchSelf();
                owner = null;
                AccessRuntime.Enabled = false;
                Logger.LogError("B&A&HB operational.1 DISABLED (base mod preserved): " + error);
            }
        }
        void OnDestroy()
        {
            AccessRuntime.Enabled = false;
            if (owner != null) owner.UnpatchSelf();
            owner = null;
        }
    }

    internal static class AccessRuntime
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        internal static Action<string> Log, Warn;
        internal static bool Enabled;
        static Type itemType, slotType, inventoryType, controllerType, importedBeltType;
        static MethodInfo itemsMethod, reachableMethod, grenadeMethod, examinedMethod;
        static Func<object, object> inventoryEquipment, controllerInventory, containedItem;
        static Func<object, object> getBeltSlot;
        static Func<object, IEnumerable> parents;
        static Func<object, string> templateId;
        static Func<object, object, bool> examined;
        static Func<object> fastSlots, bindSlots;
        static object originalFastSlots, originalBindSlots;
        static bool failedOnce, itemsReported, reachableReported, grenadesReported;
        static readonly Dictionary<Type, Func<object, object>> GridReaders = new Dictionary<Type, Func<object, object>>();
        static readonly Dictionary<Type, Func<object, object>> GridItemReaders = new Dictionary<Type, Func<object, object>>();

        internal static void Bind(Assembly basis)
        {
            inventoryType = Find("EFT.InventoryLogic.Inventory");
            controllerType = Find("EFT.InventoryLogic.InventoryController");
            itemType = Find("EFT.InventoryLogic.Item");
            slotType = Find("EFT.InventoryLogic.EquipmentSlot");
            var equipmentType = Find("EFT.InventoryLogic.InventoryEquipment");
            importedBeltType = basis.GetType("PackNStrap.Core.Items.CustomBeltItemClass", true);
            var slotEnumerable = typeof(IEnumerable<>).MakeGenericType(slotType);
            itemsMethod = inventoryType.GetMethod("GetItemsInSlots", AnyInstance, null, new[] { slotEnumerable }, null);
            if (itemsMethod == null || itemsMethod.ReturnType != typeof(IEnumerable<>).MakeGenericType(itemType))
                throw new MissingMethodException("Exact Inventory.GetItemsInSlots(IEnumerable<EquipmentSlot>)");
            reachableMethod = controllerType.GetMethod("IsAtReachablePlace", AnyInstance, null, new[] { itemType }, null);
            if (reachableMethod == null || reachableMethod.ReturnType != typeof(bool))
                throw new MissingMethodException("InventoryController.IsAtReachablePlace(Item)");
            inventoryEquipment = Reader(inventoryType, "Equipment", true);
            controllerInventory = Reader(controllerType, "Inventory", true);
            var getSlot = equipmentType.GetMethod("GetSlot", AnyInstance, null, new[] { slotType }, null);
            if (getSlot == null) throw new MissingMethodException("InventoryEquipment.GetSlot");
            var equipment = Expression.Parameter(typeof(object));
            getBeltSlot = Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Call(
                Expression.Convert(equipment, equipmentType), getSlot, Expression.Constant(Enum.ToObject(slotType, 15), slotType)), typeof(object)), equipment).Compile();
            containedItem = Reader(getSlot.ReturnType, "ContainedItem", true);
            templateId = AsStringReader(itemType, "StringTemplateId");
            fastSlots = StaticReader(inventoryType, "FastAccessSlots");
            bindSlots = StaticReader(inventoryType, "BindAvailableSlotsExtended");
            var oldBridge = basis.GetType("SPTBeltArmbandInventory.ReloadCandidateBridgeRuntime", true);
            originalFastSlots = oldBridge.GetField("OriginalFastAccessSlots", AnyStatic)?.GetValue(null);
            originalBindSlots = oldBridge.GetField("OriginalBindAvailableSlots", AnyStatic)?.GetValue(null);

            // Reuse exact base discovery code, not mutable callbacks or its publication fences.
            var discoveryType = basis.GetType("SPTBeltArmbandInventory.FastAccessSlotPatches", true);
            var discovery = discoveryType.GetMethod("FindGetAllParentItems", AnyStatic);
            var parentMethod = discovery == null ? null : discovery.Invoke(null, new object[] { itemType }) as MethodInfo;
            if (parentMethod == null || !parentMethod.IsStatic || parentMethod.GetParameters().Length != 1)
                throw new MissingMethodException("Exact GetAllParentItems(Item)");
            var item = Expression.Parameter(typeof(object));
            parents = Expression.Lambda<Func<object, IEnumerable>>(Expression.Convert(Expression.Call(parentMethod,
                Expression.Convert(item, itemType)), typeof(IEnumerable)), item).Compile();

            // One startup metadata inspection, not a scene scan or polling loop.
            var candidates = SafeTypes(equipmentType.Assembly).SelectMany(t => t.GetMethods(AnyStatic))
                .Where(m => m.Name == "GetThrowablePriorityGrenadesList" && !m.ContainsGenericParameters
                    && m.GetParameters().Length == 1
                    && m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(List<>)).ToArray();
            if (candidates.Length != 1) throw new MissingMethodException("Unique GetThrowablePriorityGrenadesList(controller)");
            grenadeMethod = candidates[0];
            var grenadeType = grenadeMethod.ReturnType.GetGenericArguments()[0];
            var grenadeControllerType = grenadeMethod.GetParameters()[0].ParameterType;
            var examination = grenadeControllerType.GetMethods(AnyInstance).Where(m => m.Name.IndexOf("Examined", StringComparison.OrdinalIgnoreCase) >= 0
                && m.ReturnType == typeof(bool) && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsAssignableFrom(grenadeType)).ToArray();
            examinedMethod = examination.Length == 1 ? examination[0] : examination.SingleOrDefault(m => m.Name == "Examined" && m.GetParameters()[0].ParameterType == itemType);
            if (examinedMethod == null) throw new MissingMethodException("Exact grenade examination boundary");
            var c = Expression.Parameter(typeof(object)); var g = Expression.Parameter(typeof(object));
            examined = Expression.Lambda<Func<object, object, bool>>(Expression.Call(Expression.Convert(c, grenadeControllerType),
                examinedMethod, Expression.Convert(g, examinedMethod.GetParameters()[0].ParameterType)), c, g).Compile();
        }

        internal static void Install(Harmony harmony)
        {
            Patch(harmony, itemsMethod, nameof(ItemsPostfix), itemType, slotType);
            Patch(harmony, reachableMethod, nameof(ReachablePostfix), itemType);
            Patch(harmony, grenadeMethod, nameof(GrenadesPostfix), grenadeMethod.ReturnType.GetGenericArguments()[0]);
            Enabled = true;
            Log?.Invoke("B&A&HB operational.1 bound: " + itemsMethod + "; " + reachableMethod + "; " + grenadeMethod);
        }
        static void Patch(Harmony owner, MethodInfo original, string name, params Type[] args)
        {
            var method = typeof(AccessRuntime).GetMethod(name, AnyStatic).MakeGenericMethod(args);
            owner.Patch(original, postfix: new HarmonyMethod(method) { priority = Priority.Last });
            var info = Harmony.GetPatchInfo(original);
            if (info == null || !info.Postfixes.Any(p => p.owner == Plugin.Id && p.PatchMethod == method))
                throw new InvalidOperationException("Patch ownership not confirmed: " + original);
        }

        static void ItemsPostfix<T, S>(object __instance, IEnumerable<S> __0, ref IEnumerable<T> __result) where T : class
        {
            if (!Enabled || __result == null || __0 == null) return;
            try
            {
                bool permitted = ReferenceEquals(__0, fastSlots()) || ReferenceEquals(__0, bindSlots())
                    || ReferenceEquals(__0, originalFastSlots) || ReferenceEquals(__0, originalBindSlots);
                // Do not consume arbitrary lazy selectors a second time. Known arrays/lists only.
                if (!permitted && __0 is ICollection<S> selected && selected.Count <= 64)
                    foreach (S slot in selected) if (Convert.ToInt32(slot) == 15) { permitted = true; break; }
                if (!permitted) return;
                object root = Root(__instance);
                if (root == null) return;
                if (!BeltAccessCore.TryCollect(root, Children, out var extra)) { Failure("Belt tree rejected (shape/cycle/budget)"); return; }
                __result = BeltAccessCore.Merge(__result, extra);
                if (!itemsReported && extra.Count > 0) { itemsReported = true; Log?.Invoke("B&A&HB operational.1: Belt candidate traversal active, descendants=" + extra.Count); }
            }
            catch (Exception e) { Failure("candidate enumeration: " + e.Message); }
        }
        static void ReachablePostfix<T>(object __instance, T __0, ref bool __result) where T : class
        {
            if (!Enabled || __result || __0 == null) return;
            try
            {
                var root = Root(controllerInventory(__instance));
                if (root != null && BeltAccessCore.IsGridDescendant(__0, root, parents, Children))
                {
                    __result = true;
                    if (!reachableReported) { reachableReported = true; Log?.Invoke("B&A&HB operational.1: equipped Belt grid-path reachability active."); }
                }
            }
            catch (Exception e) { Failure("reachability: " + e.Message); }
        }
        static void GrenadesPostfix<T>(object __0, ref List<T> __result) where T : class
        {
            if (!Enabled || __0 == null || __result == null) return;
            try
            {
                var root = Root(controllerInventory(__0));
                if (root == null) return;
                if (!BeltAccessCore.TryCollect(root, Children, out var extra)) { Failure("Grenade Belt tree rejected"); return; }
                var eligible = new List<object>();
                foreach (object item in extra) if (item is T && examined(__0, item)) eligible.Add(item);
                if (eligible.Count == 0) return;
                __result = BeltAccessCore.Merge(__result, eligible).ToList();
                if (!grenadesReported) { grenadesReported = true; Log?.Invoke("B&A&HB operational.1: examined Belt grenades appended, count=" + eligible.Count); }
            }
            catch (Exception e) { Failure("grenades: " + e.Message); }
        }
        static object Root(object inventory)
        {
            if (inventory == null || !inventoryType.IsInstanceOfType(inventory)) return null;
            var equipment = inventoryEquipment(inventory);
            if (equipment == null) return null;
            var slot = getBeltSlot(equipment);
            var root = slot == null ? null : containedItem(slot);
            if (root == null) return null;
            return templateId(root) == "68ac0000000000000000000c" || importedBeltType.IsInstanceOfType(root) ? root : null;
        }
        static IEnumerable Children(object item)
        {
            if (item == null || !itemType.IsInstanceOfType(item)) yield break;
            var type = item.GetType();
            if (!GridReaders.TryGetValue(type, out var grids)) { grids = Reader(type, "Grids", false); GridReaders.Add(type, grids); }
            var gridArray = grids(item);
            if (gridArray == null) yield break;
            if (!(gridArray is IEnumerable sequence)) throw new InvalidOperationException("Grids is not enumerable");
            int gridCount = 0;
            foreach (object grid in sequence)
            {
                if (++gridCount > 64) throw new InvalidOperationException("Grid count exceeds bound");
                if (grid == null) continue;
                var gridType = grid.GetType();
                if (!GridItemReaders.TryGetValue(gridType, out var items)) { items = Reader(gridType, "Items", true); GridItemReaders.Add(gridType, items); }
                if (!(items(grid) is IEnumerable values)) throw new InvalidOperationException("Grid.Items is not enumerable");
                int childCount = 0;
                foreach (object child in values)
                {
                    if (++childCount > BeltAccessCore.MaxEdges) throw new InvalidOperationException("Grid item count exceeds bound");
                    if (child != null && !itemType.IsInstanceOfType(child)) throw new InvalidOperationException("Grid.Items contains a non-Item");
                    yield return child;
                }
            }
        }
        static Func<object, object> Reader(Type type, string name, bool required)
        {
            var instance = Expression.Parameter(typeof(object));
            for (Type current = type; current != null; current = current.BaseType)
            {
                var property = current.GetProperty(name, AnyInstance | BindingFlags.DeclaredOnly);
                var field = current.GetField(name, AnyInstance | BindingFlags.DeclaredOnly);
                if (property != null && property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) != null)
                    return Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Property(Expression.Convert(instance, current), property), typeof(object)), instance).Compile();
                if (field != null)
                    return Expression.Lambda<Func<object, object>>(Expression.Convert(Expression.Field(Expression.Convert(instance, current), field), typeof(object)), instance).Compile();
            }
            if (required) throw new MissingMemberException(type.FullName, name);
            return _ => null;
        }
        static Func<object, string> AsStringReader(Type type, string name)
        {
            var reader = Reader(type, name, true);
            return value => reader(value) as string;
        }
        static Func<object> StaticReader(Type type, string name)
        {
            var field = type.GetField(name, AnyStatic);
            if (field == null) throw new MissingFieldException(type.FullName, name);
            return Expression.Lambda<Func<object>>(Expression.Convert(Expression.Field(null, field), typeof(object))).Compile();
        }
        static Type Find(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            { var result = assembly.GetType(name, false); if (result != null) return result; }
            throw new TypeLoadException(name);
        }
        static Type[] SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }
        static void Failure(string message)
        {
            if (failedOnce) return;
            failedOnce = true;
            Warn?.Invoke("B&A&HB operational.1 retained native result: " + message);
        }
    }
}
