using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace SPTBeltArmbandInventory
{
    // Native 40743 action boundaries, verified from the user's Assembly-CSharp.dll.
    // This type is integrated into the primary client, not registered as a plugin.
    public static class NativeBeltActions
    {
        const BindingFlags S = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags I = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        const string OwnerId = "com.admiralam.spt.belt-armband-inventory.native-actions";
        public const string Revision = "integrated.rc3";
        static Action<string> info, warn;
        static object owner;
        static MethodInfo unpatch;
        static Type controllerType, equipmentType, ammoType, magazineType, instructionType;
        static Func<object, List<object>> collect;
        static Func<object, object> rootFromInventory, controllerInventory, containedItem;
        static Func<object, bool> supported;
        static MethodInfo getSlot;
        static object beltValue;
        static FieldInfo instructionOpcode, instructionOperand;
        static ConstructorInfo instructionCopy;
        static bool active, failedOnce, reloadReported, packingReported;
        static int rewrittenCalls;
        static readonly Dictionary<MethodInfo, MethodInfo> Wrappers = new Dictionary<MethodInfo, MethodInfo>();
        static readonly Dictionary<Type, Func<object, object, bool>> Predicates = new Dictionary<Type, Func<object, object, bool>>();
        [ThreadStatic] static Dictionary<object, List<object>> prefixes;

        public static bool IsActive() { return active && IntegratedBeltAccess.IsActive(); }
        public static bool Install(Action<string> logInfo, Action<string> logWarning)
        {
            if (IsActive()) return true;
            info = logInfo; warn = logWarning;
            if (!IntegratedBeltAccess.Install(logInfo, logWarning)) return false;
            try
            {
                controllerType = Find("EFT.InventoryLogic.InventoryController");
                equipmentType = Find("EFT.InventoryLogic.InventoryEquipment");
                ammoType = Find("EFT.InventoryLogic.Ammo");
                magazineType = Find("EFT.InventoryLogic.Magazine");
                Type slotEnum = Find("EFT.InventoryLogic.EquipmentSlot");
                getSlot = equipmentType.GetMethod("GetSlot", I, null, new[] { slotEnum }, null);
                if (getSlot == null) throw new MissingMethodException("InventoryEquipment.GetSlot");
                beltValue = Enum.ToObject(slotEnum, 15);
                Type core = typeof(IntegratedBeltAccess);
                collect = (Func<object, List<object>>)Delegate.CreateDelegate(typeof(Func<object, List<object>>), core.GetMethod("Collect", S));
                rootFromInventory = (Func<object, object>)Delegate.CreateDelegate(typeof(Func<object, object>), core.GetMethod("Root", S));
                supported = (Func<object, bool>)Delegate.CreateDelegate(typeof(Func<object, bool>), core.GetMethod("Supported", S));
                MethodInfo getter = core.GetMethod("Getter", S);
                controllerInventory = (Func<object, object>)getter.Invoke(null, new object[] { controllerType, "Inventory", true });
                containedItem = (Func<object, object>)getter.Invoke(null, new object[] { getSlot.ReturnType, "ContainedItem", true });
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", true);
                Type hmType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", true);
                instructionType = Type.GetType("HarmonyLib.CodeInstruction, 0Harmony", true);
                instructionOpcode = instructionType.GetField("opcode", I);
                instructionOperand = instructionType.GetField("operand", I);
                instructionCopy = instructionType.GetConstructor(new[] { instructionType });
                if (instructionOpcode == null || instructionOperand == null || instructionCopy == null) throw new MissingMemberException("Harmony CodeInstruction copy/opcode/operand");
                owner = Activator.CreateInstance(harmonyType, new object[] { OwnerId });
                unpatch = harmonyType.GetMethod("UnpatchSelf", I, null, Type.EmptyTypes, null);
                if (unpatch == null) throw new MissingMethodException("Harmony.UnpatchSelf");
                MethodInfo transpiler = EmitTranspiler();
                Type translator = Find("EFT.FirearmHandsInputTranslator");
                string[] actions = { "Reload", "ReloadBarrels", "ReloadRevolverDrum", "ReloadWithAmmo", "ReloadExternalMagazine" };
                foreach (string action in actions) Patch(harmonyType, hmType, Unique(translator, action), transpiler);
                Patch(harmonyType, hmType, Unique(Find("EFT.FirearmHandsInputTranslator+CG_LoadAmmoToChamber"), "method_0"), transpiler);
                Patch(harmonyType, hmType, Unique(Find("EFT.UI.ItemUiContext"), "FindCompatibleAmmo"), transpiler);
                Patch(harmonyType, hmType, Unique(Find("EFT.UI.ItemUiContext+CG_MoveNext1+Struct1069"), "MoveNext"), transpiler);
                // The native paths have 6 reload queries and 3 packing queries.
                if (rewrittenCalls < 9) throw new InvalidOperationException("Native action call-site coverage incomplete: " + rewrittenCalls);
                active = true;
                info?.Invoke("B&A&HB integrated-access RC3 ACTIVE: primary DLL; native reload and actual magazine packing bridged; verified call sites=" + rewrittenCalls);
                return true;
            }
            catch (Exception e)
            {
                Dispose();
                warn?.Invoke("B&A&HB integrated-access RC3 DISABLED: " + Unwrap(e));
                return false;
            }
        }

        static MethodInfo Unique(Type type, string name)
        {
            MethodInfo found = null;
            foreach (MethodInfo method in type.GetMethods(I | S | BindingFlags.DeclaredOnly))
            {
                if (method.Name != name || method.ContainsGenericParameters) continue;
                if (found != null) throw new AmbiguousMatchException(type.FullName + "." + name);
                found = method;
            }
            return found ?? throw new MissingMethodException(type.FullName, name);
        }
        static void Patch(Type harmony, Type hm, MethodInfo target, MethodInfo transpiler)
        {
            MethodInfo patch = null;
            foreach (MethodInfo method in harmony.GetMethods(I))
            {
                if (method.Name != "Patch") continue;
                var args = method.GetParameters();
                if (args.Length == 0 || args[0].ParameterType != typeof(MethodBase)) continue;
                foreach (var arg in args) if (arg.Name == "transpiler" && arg.ParameterType == hm) patch = method;
            }
            if (patch == null) throw new MissingMethodException("Harmony.Patch transpiler");
            var signature = patch.GetParameters(); var values = new object[signature.Length]; values[0] = target;
            for (int i = 1; i < values.Length; i++) if (signature[i].Name == "transpiler") values[i] = Activator.CreateInstance(hm, new object[] { transpiler });
            patch.Invoke(owner, values);
        }
        static TypeBuilder NewType(string name)
        {
            var a = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("BAndHBNative_" + name + "_" + Wrappers.Count), AssemblyBuilderAccess.Run);
            return a.DefineDynamicModule("Runtime").DefineType("Bridge", TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed);
        }
        static MethodInfo EmitTranspiler()
        {
            Type sequence = typeof(IEnumerable<>).MakeGenericType(instructionType);
            var type = NewType("Transpiler");
            var method = type.DefineMethod("Apply", MethodAttributes.Public | MethodAttributes.Static, sequence, new[] { sequence, typeof(MethodBase) });
            method.DefineParameter(1, ParameterAttributes.None, "instructions");
            method.DefineParameter(2, ParameterAttributes.None, "__originalMethod");
            var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Call, typeof(NativeBeltActions).GetMethod(nameof(Rewrite), S));
            il.Emit(OpCodes.Castclass, sequence); il.Emit(OpCodes.Ret);
            return type.CreateTypeInfo().AsType().GetMethod("Apply");
        }
        public static object Rewrite(object instructions, object original)
        {
            var result = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(instructionType));
            int matched = 0;
            foreach (object source in (IEnumerable)instructions)
            {
                object instruction = instructionCopy.Invoke(new[] { source });
                var opcode = (OpCode)instructionOpcode.GetValue(instruction);
                var method = instructionOperand.GetValue(instruction) as MethodInfo;
                if ((opcode == OpCodes.Call || opcode == OpCodes.Callvirt) && method != null)
                {
                    int kind = Kind(method);
                    if (kind != 0)
                    {
                        if (!Wrappers.TryGetValue(method, out MethodInfo replacement))
                        {
                            replacement = EmitCall(method, kind);
                            Wrappers.Add(method, replacement);
                        }
                        instructionOpcode.SetValue(instruction, OpCodes.Call);
                        instructionOperand.SetValue(instruction, replacement);
                        if (kind != 4) matched++;
                    }
                }
                result.Add(instruction);
            }
            if (matched == 0) throw new MissingMethodException("Expected native inventory call absent in " + original);
            rewrittenCalls += matched;
            return result;
        }
        static int Kind(MethodInfo method)
        {
            if (method.DeclaringType == controllerType && method.IsGenericMethod)
            {
                var type = method.GetGenericArguments();
                if (type.Length != 1 || type[0] != ammoType && type[0] != magazineType) return 0;
                if (method.Name == "GetReachableItemsOfTypeNonAlloc" && method.ReturnType == typeof(void) && method.GetParameters().Length == 2) return 1;
                if (method.Name == "GetReachableItemsOfType" && method.GetParameters().Length == 1) return 2;
            }
            if (method.DeclaringType.FullName == "EFT.InventoryLogic.ItemExtensions" && method.IsStatic && method.ReturnType == typeof(void) && method.GetParameters().Length == 2)
            {
                if (!method.IsGenericMethod && method.Name == "GetAllAssembledItemsNonAlloc") return 3;
                if (method.IsGenericMethod && method.Name == "GetAllAssembledItems" && method.GetGenericArguments().Length == 1 && method.GetGenericArguments()[0] == ammoType) return 3;
            }
            Type declaring = method.DeclaringType;
            if (method.Name == "Sort" && declaring.IsGenericType && declaring.GetGenericTypeDefinition() == typeof(List<>) && method.GetParameters().Length == 1)
            {
                Type element = declaring.GetGenericArguments()[0]; Type argument = method.GetParameters()[0].ParameterType;
                if ((element == ammoType || element == magazineType) && argument.IsGenericType && argument.GetGenericTypeDefinition() == typeof(Comparison<>)) return 4;
            }
            return 0;
        }
        static MethodInfo EmitCall(MethodInfo original, int kind)
        {
            var signature = new List<Type>();
            if (!original.IsStatic) signature.Add(original.DeclaringType);
            foreach (var p in original.GetParameters()) signature.Add(p.ParameterType);
            var type = NewType("Call"); var method = type.DefineMethod("Apply", MethodAttributes.Public | MethodAttributes.Static, original.ReturnType, signature.ToArray());
            var il = method.GetILGenerator();
            for (short i = 0; i < signature.Count; i++) il.Emit(OpCodes.Ldarg, i);
            il.Emit(original.IsStatic ? OpCodes.Call : OpCodes.Callvirt, original);
            LocalBuilder native = null;
            if (original.ReturnType != typeof(void)) { native = il.DeclareLocal(original.ReturnType); il.Emit(OpCodes.Stloc, native); }
            il.Emit(OpCodes.Ldarg_0);
            if (kind == 1 || kind == 2)
            {
                Type element = original.GetGenericArguments()[0]; EnsurePredicate(element);
                if (kind == 1) { il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Ldarg_2); }
                else { il.Emit(OpCodes.Ldloc, native); il.Emit(OpCodes.Ldarg_1); }
                il.Emit(OpCodes.Ldtoken, element); il.Emit(OpCodes.Call, typeof(Type).GetMethod("GetTypeFromHandle"));
                il.Emit(OpCodes.Call, typeof(NativeBeltActions).GetMethod(kind == 1 ? nameof(AppendReload) : nameof(ExtendReload), S));
                if (kind == 2) il.Emit(OpCodes.Castclass, original.ReturnType);
            }
            else if (kind == 3)
            {
                il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, typeof(NativeBeltActions).GetMethod(nameof(AppendPacking), S));
            }
            else il.Emit(OpCodes.Call, typeof(NativeBeltActions).GetMethod(nameof(RestoreNativePriority), S));
            il.Emit(OpCodes.Ret);
            return type.CreateTypeInfo().AsType().GetMethod("Apply");
        }
        static void EnsurePredicate(Type element)
        {
            if (Predicates.ContainsKey(element)) return;
            Type predicate = typeof(Predicate<>).MakeGenericType(element);
            var method = new DynamicMethod("BAndHBPredicate", typeof(bool), new[] { typeof(object), typeof(object) }, typeof(NativeBeltActions), true);
            var il = method.GetILGenerator(); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Castclass, predicate); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Castclass, element);
            il.Emit(OpCodes.Callvirt, predicate.GetMethod("Invoke")); il.Emit(OpCodes.Ret);
            Predicates.Add(element, (Func<object, object, bool>)method.CreateDelegate(typeof(Func<object, object, bool>)));
        }
        static bool Contains(IEnumerable sequence, object item) { foreach (object value in sequence) if (ReferenceEquals(value, item)) return true; return false; }
        static List<object> Eligible(object controller, object predicate, Type element)
        {
            var result = new List<object>();
            if (controller == null || !controllerType.IsInstanceOfType(controller)) return result;
            object root = rootFromInventory(controllerInventory(controller));
            if (root == null)
            {
                if (!reloadReported) { reloadReported = true; info?.Invoke("B&A&HB RC3 native reload reached; no supported equipped Belt root."); }
                return result;
            }
            foreach (object item in collect(root)) if (element.IsInstanceOfType(item) && (predicate == null || Predicates[element](predicate, item))) result.Add(item);
            if (!reloadReported) { reloadReported = true; info?.Invoke("B&A&HB RC3 native reload reached: eligible Belt descendants=" + result.Count + ", type=" + element.Name); }
            return result;
        }
        public static void AppendReload(object controller, object destination, object predicate, Type element)
        {
            if (!IsActive() || !(destination is IList list) || list.IsFixedSize || list.IsReadOnly) return;
            int before = list.Count;
            try
            {
                var extra = Eligible(controller, predicate, element); var additions = new List<object>();
                foreach (object item in extra) if (!Contains(list, item)) additions.Add(item);
                if (additions.Count == 0) return;
                var prefix = new List<object>(); foreach (object item in list) prefix.Add(item);
                foreach (object item in additions) list.Add(item);
                if (prefixes == null) prefixes = new Dictionary<object, List<object>>();
                if (prefixes.Count >= 8 && !prefixes.ContainsKey(destination)) prefixes.Clear();
                prefixes[destination] = prefix;
            }
            catch (Exception e) { while (list.Count > before) list.RemoveAt(list.Count - 1); Failure("native reload", e); }
        }
        public static object ExtendReload(object controller, object native, object predicate, Type element)
        {
            if (!IsActive() || !(native is IEnumerable source)) return native;
            try
            {
                var extra = Eligible(controller, predicate, element); if (extra.Count == 0) return native;
                var items = new List<object>(); foreach (object item in source) items.Add(item);
                foreach (object item in extra) if (!Contains(items, item)) items.Add(item);
                var result = Array.CreateInstance(element, items.Count); for (int i = 0; i < items.Count; i++) result.SetValue(items[i], i); return result;
            }
            catch (Exception e) { Failure("native reload sequence", e); return native; }
        }
        public static void RestoreNativePriority(object destination)
        {
            if (!IsActive() || prefixes == null || !prefixes.TryGetValue(destination, out var prefix)) return;
            prefixes.Remove(destination);
            if (!(destination is IList list) || list.IsReadOnly || list.IsFixedSize) return;
            // Native Sort has already run. Stable partition retains that ordering within each source group.
            var ordered = new List<object>(); foreach (object item in list) if (Contains(prefix, item)) ordered.Add(item);
            foreach (object item in list) if (!Contains(prefix, item)) ordered.Add(item);
            for (int i = 0; i < ordered.Count; i++) list[i] = ordered[i];
        }
        public static void AppendPacking(object equipment, object destination)
        {
            if (!IsActive() || equipment == null || !equipmentType.IsInstanceOfType(equipment) || !(destination is IList list) || list.IsReadOnly || list.IsFixedSize) return;
            int before = list.Count;
            try
            {
                object slot = getSlot.Invoke(equipment, new[] { beltValue });
                object root = slot == null ? null : containedItem(slot); if (!supported(root)) return;
                var additions = new List<object>();
                foreach (object item in collect(root)) if (ammoType.IsInstanceOfType(item) && !Contains(list, item)) additions.Add(item);
                foreach (object item in additions) list.Add(item);
                if (!packingReported) { packingReported = true; info?.Invoke("B&A&HB RC3 native packing enumeration reached: appended Ammo stacks=" + additions.Count); }
                // Native IsAvailable, CheckCompatibility, stack totals and loading operations run afterwards, unmodified.
            }
            catch (Exception e) { while (list.Count > before) list.RemoveAt(list.Count - 1); Failure("native packing", e); }
        }
        static Type Find(string name)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) { var t = a.GetType(name, false); if (t != null) return t; }
            throw new TypeLoadException(name);
        }
        static Exception Unwrap(Exception e) { while (e is TargetInvocationException t && t.InnerException != null) e = t.InnerException; return e; }
        static void Failure(string area, Exception e) { if (failedOnce) return; failedOnce = true; warn?.Invoke("B&A&HB RC3 retained native result: " + area + ": " + Unwrap(e)); }
        public static void Dispose()
        {
            active = false;
            try { if (owner != null && unpatch != null) unpatch.Invoke(owner, null); }
            catch (Exception e) { warn?.Invoke("B&A&HB RC3 cleanup: " + Unwrap(e)); }
            owner = null; unpatch = null;
            Wrappers.Clear(); Predicates.Clear(); prefixes?.Clear(); rewrittenCalls = 0;
            reloadReported = packingReported = failedOnce = false;
            IntegratedBeltAccess.Dispose();
        }
    }
}
