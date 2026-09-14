using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    // Compiled into the existing primary assembly by the reproducible offline integrator.
    // This is not a BepInEx plugin and has no separately loaded runtime assembly.
    public static class IntegratedBeltAccess
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const string OwnerId = "com.admiralam.spt.belt-armband-inventory.integrated-access";
        const int MaxItems = 512, MaxDepth = 8;
        static Action<string> info, warn;
        static Type itemType, inventoryType, controllerType, importedType, slotType, grenadeType;
        static MethodInfo getSlot, itemsMethod, reachMethod, grenadesMethod, slotsGetter, examined;
        static object beltValue, owner;
        static MethodInfo unpatch;
        static FieldInfo fastSlots, bindSlots;
        static object capturedFast, capturedBind;
        static bool active, failedOnce, candidatesLogged, reachLogged, grenadeLogged;
        static readonly Dictionary<Type, Dictionary<string, Func<object, object>>> Readers = new Dictionary<Type, Dictionary<string, Func<object, object>>>();

        public static bool IsActive() { return active; }
        public static bool Install(Action<string> logInfo, Action<string> logWarning)
        {
            if (active) return true;
            info = logInfo; warn = logWarning;
            try
            {
                Bind();
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", true);
                Type hmType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", true);
                owner = Activator.CreateInstance(harmonyType, new object[] { OwnerId });
                unpatch = harmonyType.GetMethod("UnpatchSelf", Instance, null, Type.EmptyTypes, null);
                if (unpatch == null) throw new MissingMethodException("Harmony.UnpatchSelf");
                Patch(harmonyType, hmType, itemsMethod, nameof(Candidates), true, true);
                Patch(harmonyType, hmType, reachMethod, nameof(Reachable), true, true);
                if (grenadesMethod != null) Patch(harmonyType, hmType, grenadesMethod, nameof(Grenades), false, true);
                if (slotsGetter != null) Patch(harmonyType, hmType, slotsGetter, nameof(GrenadeSlots), true, false);
                active = true;
                info?.Invoke("B&A&HB integrated-access RC2 ACTIVE: primary DLL; equipped Belt and nested grids; native-first; no GetAllParentItems dependency. Bound " + reachMethod.DeclaringType.FullName + "." + reachMethod.Name);
                return true;
            }
            catch (Exception e)
            {
                Dispose();
                warn?.Invoke("B&A&HB integrated-access RC2 DISABLED: " + Unwrap(e));
                return false;
            }
        }

        static void Bind()
        {
            itemType = Find("EFT.InventoryLogic.Item");
            inventoryType = Find("EFT.InventoryLogic.Inventory");
            controllerType = Find("EFT.InventoryLogic.InventoryController");
            slotType = Find("EFT.InventoryLogic.EquipmentSlot");
            Type equipmentType = Find("EFT.InventoryLogic.InventoryEquipment");
            importedType = typeof(IntegratedBeltAccess).Assembly.GetType("PackNStrap.Core.Items.CustomBeltItemClass", false);
            getSlot = equipmentType.GetMethod("GetSlot", Instance, null, new[] { slotType }, null);
            if (getSlot == null) throw new MissingMethodException(equipmentType.FullName, "GetSlot(EquipmentSlot)");
            beltValue = Enum.ToObject(slotType, 15);
            itemsMethod = inventoryType.GetMethod("GetItemsInSlots", Instance, null, new[] { typeof(IEnumerable<>).MakeGenericType(slotType) }, null);
            if (itemsMethod == null || itemsMethod.ReturnType != typeof(IEnumerable<>).MakeGenericType(itemType)) throw new MissingMethodException("Inventory.GetItemsInSlots exact return/parameter contract");
            reachMethod = controllerType.GetMethod("IsAtReachablePlace", Instance, null, new[] { itemType }, null);
            if (reachMethod == null || reachMethod.ReturnType != typeof(bool)) throw new MissingMethodException(controllerType.FullName, "IsAtReachablePlace(Item):bool");
            fastSlots = inventoryType.GetField("FastAccessSlots", Static);
            bindSlots = inventoryType.GetField("BindAvailableSlotsExtended", Static);
            if (fastSlots == null || bindSlots == null) throw new MissingFieldException("Inventory fast-access arrays");
            capturedFast = fastSlots.GetValue(null); capturedBind = bindSlots.GetValue(null);
            // Resolve known accessors at startup; nearest declared member wins over inherited duplicates.
            Getter(inventoryType, "Equipment", true);
            Getter(controllerType, "Inventory", true);
            Getter(getSlot.ReturnType, "ContainedItem", true);
            Getter(itemType, "StringTemplateId", true);
            grenadesMethod = null; examined = null; grenadeType = null;
            Type[] types;
            try { types = equipmentType.Assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            foreach (Type type in types)
            {
                if (type == null) continue;
                foreach (MethodInfo method in type.GetMethods(Static))
                {
                    if (method.Name != "GetThrowablePriorityGrenadesList" || method.ContainsGenericParameters) continue;
                    ParameterInfo[] args = method.GetParameters();
                    if (args.Length != 1 || !args[0].ParameterType.IsAssignableFrom(controllerType) && !controllerType.IsAssignableFrom(args[0].ParameterType)) continue;
                    if (!method.ReturnType.IsGenericType || method.ReturnType.GetGenericTypeDefinition() != typeof(List<>)) continue;
                    if (grenadesMethod != null) throw new AmbiguousMatchException("GetThrowablePriorityGrenadesList");
                    grenadesMethod = method;
                }
            }
            if (grenadesMethod == null) throw new MissingMethodException("GetThrowablePriorityGrenadesList(controller)");
            grenadeType = grenadesMethod.ReturnType.GetGenericArguments()[0];
            Type gc = grenadesMethod.GetParameters()[0].ParameterType;
            examined = gc.GetMethod("Examined", Instance, null, new[] { itemType }, null);
            if (examined == null)
            {
                foreach (MethodInfo method in gc.GetMethods(Instance))
                {
                    ParameterInfo[] args = method.GetParameters();
                    if (method.Name.IndexOf("Examined", StringComparison.OrdinalIgnoreCase) < 0 || method.ReturnType != typeof(bool) || args.Length != 1 || !args[0].ParameterType.IsAssignableFrom(grenadeType)) continue;
                    if (examined != null) throw new AmbiguousMatchException("Grenade examination");
                    examined = method;
                }
            }
            if (examined == null) throw new MissingMethodException("Grenade examination");
            slotsGetter = equipmentType.GetProperty("GrenadeThrowingSlots", Instance)?.GetGetMethod(true);
        }

        static void Patch(Type harmonyType, Type hmType, MethodInfo target, string handlerName, bool hasInstance, bool hasArgument)
        {
            MethodInfo handler = typeof(IntegratedBeltAccess).GetMethod(handlerName, Static);
            Type resultType = target.ReturnType;
            var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BAndHBIntegrated_" + handlerName), AssemblyBuilderAccess.Run);
            var type = assembly.DefineDynamicModule("Runtime").DefineType("Postfix", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
            var parameters = new List<Type>();
            if (hasInstance) parameters.Add(typeof(object));
            if (hasArgument) parameters.Add(typeof(object));
            parameters.Add(resultType.MakeByRefType());
            var method = type.DefineMethod("Apply", MethodAttributes.Public | MethodAttributes.Static, typeof(void), parameters.ToArray());
            int position = 1;
            if (hasInstance) method.DefineParameter(position++, ParameterAttributes.None, "__instance");
            if (hasArgument) method.DefineParameter(position++, ParameterAttributes.None, "__0");
            method.DefineParameter(position, ParameterAttributes.None, "__result");
            ILGenerator il = method.GetILGenerator();
            int resultIndex = parameters.Count - 1;
            il.Emit(OpCodes.Ldarg, resultIndex);
            if (hasInstance) il.Emit(OpCodes.Ldarg_0); else il.Emit(OpCodes.Ldnull);
            if (hasArgument) il.Emit(OpCodes.Ldarg, hasInstance ? 1 : 0); else il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldarg, resultIndex);
            il.Emit(OpCodes.Ldobj, resultType);
            if (resultType.IsValueType) il.Emit(OpCodes.Box, resultType);
            il.Emit(OpCodes.Call, handler);
            if (resultType.IsValueType) il.Emit(OpCodes.Unbox_Any, resultType); else il.Emit(OpCodes.Castclass, resultType);
            il.Emit(OpCodes.Stobj, resultType); il.Emit(OpCodes.Ret);
            MethodInfo postfix = type.CreateTypeInfo().AsType().GetMethod("Apply");
            object hm = Activator.CreateInstance(hmType, new object[] { postfix });
            hmType.GetField("priority", Instance)?.SetValue(hm, 0);
            MethodInfo patch = null;
            foreach (MethodInfo candidate in harmonyType.GetMethods(Instance))
            {
                if (candidate.Name != "Patch") continue;
                ParameterInfo[] args = candidate.GetParameters();
                if (args.Length < 3 || args[0].ParameterType != typeof(MethodBase)) continue;
                foreach (ParameterInfo arg in args) if (arg.Name == "postfix" && arg.ParameterType == hmType) patch = candidate;
            }
            if (patch == null) throw new MissingMethodException("Harmony.Patch postfix");
            ParameterInfo[] signature = patch.GetParameters();
            var values = new object[signature.Length]; values[0] = target;
            for (int i = 1; i < values.Length; i++) if (signature[i].Name == "postfix") values[i] = hm;
            patch.Invoke(owner, values);
        }

        public static object Candidates(object inventory, object selector, object native)
        {
            if (!active || native == null || selector == null) return native;
            try
            {
                bool allowed = ReferenceEquals(selector, fastSlots.GetValue(null)) || ReferenceEquals(selector, bindSlots.GetValue(null)) || ReferenceEquals(selector, capturedFast) || ReferenceEquals(selector, capturedBind);
                if (!allowed && selector is IList list && list.Count <= 64) foreach (object slot in list) if (Convert.ToInt32(slot) == 15) { allowed = true; break; }
                if (!allowed) return native;
                object root = Root(inventory); if (root == null) return native;
                List<object> extra = Collect(root);
                object result = Merge(native, extra, itemType, false);
                if (!candidatesLogged && extra.Count > 0) { candidatesLogged = true; info?.Invoke("B&A&HB integrated-access: Belt candidate enumeration active."); }
                return result;
            }
            catch (Exception e) { Failure("candidates", e); return native; }
        }
        public static object Reachable(object controller, object item, object native)
        {
            if (!active || native is bool yes && yes || item == null) return native;
            try
            {
                object root = Root(Read(controller, "Inventory", true));
                if (root == null || ReferenceEquals(root, item)) return native;
                if (!Contains(Collect(root), item)) return native;
                if (!reachLogged) { reachLogged = true; info?.Invoke("B&A&HB integrated-access: equipped Belt grid reachability active."); }
                return true;
            }
            catch (Exception e) { Failure("reachability", e); return native; }
        }
        public static object Grenades(object unused, object controller, object native)
        {
            if (!active || native == null || controller == null) return native;
            try
            {
                object root = Root(Read(controller, "Inventory", true)); if (root == null) return native;
                var extra = new List<object>();
                foreach (object item in Collect(root)) if (grenadeType.IsInstanceOfType(item) && (bool)examined.Invoke(controller, new[] { item })) extra.Add(item);
                if (extra.Count == 0) return native;
                if (!grenadeLogged) { grenadeLogged = true; info?.Invoke("B&A&HB integrated-access: examined Belt grenade enumeration active."); }
                return Merge(native, extra, grenadeType, true);
            }
            catch (Exception e) { Failure("grenades", e); return native; }
        }
        public static object GrenadeSlots(object equipment, object unused, object native)
        {
            if (!active || native == null || equipment == null) return native;
            try
            {
                object slot = getSlot.Invoke(equipment, new[] { beltValue });
                object item = Read(slot, "ContainedItem", true);
                if (!Supported(item)) return native;
                if (!(native is IList list) || !native.GetType().IsGenericType || native.GetType().GetGenericTypeDefinition() != typeof(List<>)) return native;
                if (Contains(list, slot)) return native;
                var copy = (IList)Activator.CreateInstance(native.GetType()); foreach (object value in list) copy.Add(value); copy.Add(slot); return copy;
            }
            catch (Exception e) { Failure("grenade slots", e); return native; }
        }
        static object Root(object inventory)
        {
            if (inventory == null || !inventoryType.IsInstanceOfType(inventory)) return null;
            object equipment = Read(inventory, "Equipment", true); if (equipment == null) return null;
            object slot = getSlot.Invoke(equipment, new[] { beltValue });
            object root = Read(slot, "ContainedItem", true); return Supported(root) ? root : null;
        }
        static bool Supported(object item)
        {
            return item != null && (Read(item, "StringTemplateId", true) as string == "68ac0000000000000000000c" || importedType != null && importedType.IsInstanceOfType(item));
        }
        static List<object> Collect(object root)
        {
            var nodes = new List<object> { root }; var depths = new List<int> { 0 }; var result = new List<object>();
            for (int i = 0; i < nodes.Count; i++)
            {
                object grids = Read(nodes[i], "Grids", false); if (grids == null) continue;
                if (!(grids is IEnumerable sequence)) throw new InvalidOperationException("Grids is not enumerable");
                int gridCount = 0;
                foreach (object grid in sequence)
                {
                    if (++gridCount > 64) throw new InvalidOperationException("Grid budget exceeded");
                    if (grid == null) continue;
                    if (!(Read(grid, "Items", true) is IEnumerable children)) throw new InvalidOperationException("Grid.Items is not enumerable");
                    foreach (object child in children)
                    {
                        if (child == null) continue;
                        if (!itemType.IsInstanceOfType(child)) throw new InvalidOperationException("Grid contains non-Item");
                        if (depths[i] >= MaxDepth || nodes.Count >= MaxItems || Contains(nodes, child)) throw new InvalidOperationException("Belt cycle/duplicate/depth/item budget");
                        nodes.Add(child); depths.Add(depths[i] + 1); result.Add(child);
                    }
                }
            }
            return result;
        }
        static object Merge(object native, List<object> extra, Type element, bool listResult)
        {
            if (extra.Count == 0) return native;
            if (!(native is IEnumerable sequence)) return native;
            var result = new List<object>();
            foreach (object value in sequence) result.Add(value);
            foreach (object value in extra) if (element.IsInstanceOfType(value) && !Contains(result, value)) result.Add(value);
            if (listResult) { var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element)); foreach (object item in result) list.Add(item); return list; }
            Array array = Array.CreateInstance(element, result.Count); for (int i = 0; i < result.Count; i++) array.SetValue(result[i], i); return array;
        }
        static bool Contains(IEnumerable items, object item) { foreach (object value in items) if (ReferenceEquals(value, item)) return true; return false; }
        static object Read(object instance, string name, bool required) { return instance == null ? null : Getter(instance.GetType(), name, required)(instance); }
        static object Missing(object value) { return null; }
        static Func<object, object> Getter(Type type, string name, bool required)
        {
            if (!Readers.TryGetValue(type, out var readers)) { readers = new Dictionary<string, Func<object, object>>(StringComparer.Ordinal); Readers.Add(type, readers); }
            if (readers.TryGetValue(name, out var cached)) return cached;
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperty(name, Instance | BindingFlags.DeclaredOnly);
                FieldInfo field = current.GetField(name, Instance | BindingFlags.DeclaredOnly);
                MethodInfo getter = property != null && property.GetIndexParameters().Length == 0 ? property.GetGetMethod(true) : null;
                if (getter == null && field == null) continue;
                var method = new DynamicMethod("BAndHBRead" + name, typeof(object), new[] { typeof(object) }, typeof(IntegratedBeltAccess), true);
                ILGenerator il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, current);
                Type result;
                if (getter != null) { il.Emit(getter.IsVirtual ? OpCodes.Callvirt : OpCodes.Call, getter); result = getter.ReturnType; }
                else { il.Emit(OpCodes.Ldfld, field); result = field.FieldType; }
                if (result.IsValueType) il.Emit(OpCodes.Box, result); il.Emit(OpCodes.Ret);
                var reader = (Func<object, object>)method.CreateDelegate(typeof(Func<object, object>)); readers.Add(name, reader); return reader;
            }
            if (required) throw new MissingMemberException(type.FullName, name);
            Func<object, object> empty = Missing; readers.Add(name, empty); return empty;
        }
        static Type Find(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) { Type found = assembly.GetType(name, false); if (found != null) return found; }
            throw new TypeLoadException(name);
        }
        static Exception Unwrap(Exception e) { while (e is TargetInvocationException t && t.InnerException != null) e = t.InnerException; return e; }
        static void Failure(string area, Exception e) { if (failedOnce) return; failedOnce = true; warn?.Invoke("B&A&HB integrated-access retained native result: " + area + ": " + Unwrap(e).Message); }
        public static void Dispose()
        {
            active = false;
            try { if (owner != null && unpatch != null) unpatch.Invoke(owner, null); }
            catch (Exception e) { warn?.Invoke("B&A&HB integrated-access cleanup: " + Unwrap(e).Message); }
            owner = null; unpatch = null;
        }
    }
}
