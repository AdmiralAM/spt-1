using System;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using BepInEx;

namespace SPTBeltArmbandInventory
{
    // Diagnostic-only companion plugin for PR #64 Gate A. It does not mutate item/container behavior.
    [BepInPlugin("com.admiralam.spt.belt-armband-inventory.runtime-type-proof", "B&A&HB Runtime Type Proof", "0.1.0")]
    public sealed class RuntimeTypeProofPlugin : BaseUnityPlugin
    {
        const string RcTpl = "68ac00000000000000000001";
        object harmony;
        MethodInfo unpatchSelf;

        void Awake()
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type panelType = ReflectionTools.FindType("EFT.UI.ContainersPanel");
                Type equipmentType = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type slotEnumType = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                if (harmonyType == null || harmonyMethodType == null || panelType == null || equipmentType == null || slotEnumType == null)
                    throw new InvalidOperationException("required SPT 4.1 runtime types were not found");

                MethodInfo show = FindShow(panelType, equipmentType);
                MethodInfo patch = FindPatchMethod(harmonyType, harmonyMethodType);
                ConstructorInfo hmCtor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                if (show == null || patch == null || hmCtor == null) throw new InvalidOperationException("ContainersPanel.Show/Harmony patch boundary not found");

                RuntimeTypeProof.EquipmentType = equipmentType;
                RuntimeTypeProof.SlotEnumType = slotEnumType;
                RuntimeTypeProof.Log = Logger.LogInfo;
                RuntimeTypeProof.Warn = Logger.LogWarning;

                harmony = Activator.CreateInstance(harmonyType, new object[] { "com.admiralam.spt.belt-armband-inventory.runtime-type-proof" });
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                object postfix = hmCtor.Invoke(new object[] { typeof(RuntimeTypeProofPlugin).GetMethod(nameof(PostfixFactory), BindingFlags.Static | BindingFlags.NonPublic) });
                patch.Invoke(harmony, new[] { show, null, postfix, null, null });
                Logger.LogInfo("B&A&HB TYPE PROOF: ContainersPanel.Show runtime-type diagnostics installed.");
            }
            catch (Exception ex)
            {
                Exception root = Unwrap(ex);
                Logger.LogWarning("B&A&HB TYPE PROOF INSTALL FAIL: " + root.GetType().FullName + ": " + root.Message);
            }
        }

        static MethodInfo PostfixFactory(MethodBase original)
        {
            MethodInfo m = original as MethodInfo;
            if (m == null || m.DeclaringType == null) return null;
            DynamicMethod dm = new DynamicMethod("BeltRuntimeTypeProofPostfix", typeof(void), new[] { typeof(object[]) }, typeof(RuntimeTypeProofPlugin), true);
            dm.DefineParameter(1, ParameterAttributes.None, "__args");
            ILGenerator il = dm.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, typeof(RuntimeTypeProof).GetMethod(nameof(RuntimeTypeProof.Probe), BindingFlags.Static | BindingFlags.NonPublic));
            il.Emit(OpCodes.Ret);
            return dm;
        }

        static MethodInfo FindShow(Type panelType, Type equipmentType)
        {
            foreach (MethodInfo m in panelType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "Show") continue;
                foreach (ParameterInfo p in m.GetParameters()) if (p.ParameterType == equipmentType) return m;
            }
            return null;
        }

        static MethodInfo FindPatchMethod(Type harmonyType, Type harmonyMethodType)
        {
            foreach (MethodInfo m in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                if (m.Name != "Patch") continue;
                ParameterInfo[] p = m.GetParameters();
                if (p.Length == 5 && p[0].ParameterType == typeof(MethodBase) && p[1].ParameterType == harmonyMethodType) return m;
            }
            return null;
        }

        static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex;
        }

        void OnDestroy()
        {
            try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { }
        }

        internal static class RuntimeTypeProof
        {
            internal static Type EquipmentType;
            internal static Type SlotEnumType;
            internal static Action<string> Log;
            internal static Action<string> Warn;
            static bool emitted;

            internal static void Probe(object[] args)
            {
                if (emitted || args == null || EquipmentType == null || SlotEnumType == null) return;
                try
                {
                    object equipment = null;
                    foreach (object a in args) if (a != null && EquipmentType.IsInstanceOfType(a)) { equipment = a; break; }
                    if (equipment == null) return;
                    MethodInfo getSlot = EquipmentType.GetMethod("GetSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { SlotEnumType }, null);
                    object armBand = Enum.Parse(SlotEnumType, "ArmBand");
                    object slot = getSlot == null ? null : getSlot.Invoke(equipment, new[] { armBand });
                    object item = Read(slot, "ContainedItem");
                    if (item == null) return;
                    string tpl = Convert.ToString(Read(item, "TemplateId") ?? Read(item, "Tpl") ?? Read(Read(item, "Template"), "_id") ?? Read(Read(item, "Template"), "Id"));
                    if (!string.Equals(tpl, RcTpl, StringComparison.OrdinalIgnoreCase)) return;
                    emitted = true;

                    Type itemType = item.GetType();
                    object template = Read(item, "Template");
                    object grids = Read(item, "Grids") ?? Read(item, "Containers");
                    int gridCount = Count(grids);
                    bool isContainer = ToBool(Read(item, "IsContainer"));
                    bool searchable = NameChainContains(itemType, "Searchable") || InterfaceContains(itemType, "Searchable");
                    bool containerContract = NameChainContains(itemType, "Container") || InterfaceContains(itemType, "Container") || isContainer;
                    bool customBelt = itemType.FullName.IndexOf("Belt", StringComparison.OrdinalIgnoreCase) >= 0 && itemType.FullName.IndexOf("ArmBand", StringComparison.OrdinalIgnoreCase) < 0;
                    object layoutName = template == null ? null : (Read(template, "LayoutName") ?? Read(template, "Layout"));

                    Log?.Invoke("B&A&HB TYPE PROOF 1/6 PASS: ArmBand.ContainedItem is RC tpl=" + tpl + ", instanceType=" + itemType.FullName + ".");
                    Log?.Invoke("B&A&HB TYPE PROOF 2/6 " + (customBelt ? "PASS" : "FAIL") + ": concrete client item type=" + itemType.FullName + "; customBeltExpected=true.");
                    Log?.Invoke("B&A&HB TYPE PROOF 3/6 " + ((isContainer && searchable && containerContract) ? "PASS" : "FAIL") + ": IsContainer=" + isContainer + ", searchable=" + searchable + ", containerContract=" + containerContract + ".");
                    Log?.Invoke("B&A&HB TYPE PROOF 4/6 " + (gridCount > 0 ? "PASS" : "FAIL") + ": client-visible grid/container count=" + gridCount + "; dimensions=" + DescribeGrids(grids) + ".");
                    Log?.Invoke("B&A&HB TYPE PROOF 5/6 " + (template != null && layoutName != null ? "PASS" : "FAIL") + ": templateType=" + (template == null ? "<null>" : template.GetType().FullName) + ", layout=" + (layoutName ?? "<null>") + ".");
                    Log?.Invoke("B&A&HB TYPE PROOF 6/6 " + (customBelt && isContainer && searchable && gridCount > 0 && template != null && layoutName != null ? "PASS" : "FAIL") + ": no-vanilla-ArmBand-fallback runtime-type gate.");
                }
                catch (Exception ex)
                {
                    Exception root = ex;
                    while (root is TargetInvocationException && root.InnerException != null) root = root.InnerException;
                    Warn?.Invoke("B&A&HB TYPE PROOF FAIL: " + root.GetType().FullName + ": " + root.Message + (root.StackTrace == null ? "" : "\n" + root.StackTrace));
                }
            }

            static object Read(object target, string name)
            {
                if (target == null) return null;
                Type t = target.GetType();
                PropertyInfo p = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(target, null);
                FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return f == null ? null : f.GetValue(target);
            }

            static bool ToBool(object value) { return value is bool && (bool)value; }
            static int Count(object value)
            {
                if (value == null) return 0;
                ICollection c = value as ICollection;
                if (c != null) return c.Count;
                IEnumerable e = value as IEnumerable;
                if (e == null) return 0;
                int n = 0; foreach (object _ in e) n++; return n;
            }
            static bool NameChainContains(Type t, string token)
            {
                for (Type x = t; x != null; x = x.BaseType) if ((x.FullName ?? x.Name).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }
            static bool InterfaceContains(Type t, string token)
            {
                foreach (Type i in t.GetInterfaces()) if ((i.FullName ?? i.Name).IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
                return false;
            }
            static string DescribeGrids(object grids)
            {
                IEnumerable e = grids as IEnumerable;
                if (e == null) return "<none>";
                string result = ""; int i = 0;
                foreach (object g in e)
                {
                    object props = Read(g, "Properties") ?? g;
                    object h = Read(props, "CellsH") ?? Read(props, "Width");
                    object v = Read(props, "CellsV") ?? Read(props, "Height");
                    if (i++ > 0) result += ",";
                    result += (h ?? "?") + "x" + (v ?? "?");
                }
                return result.Length == 0 ? "<none>" : result;
            }
        }
    }
}
