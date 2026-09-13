using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class EmbeddedAccessoryGridRuntime
    {
        const float PanelWidth = 82f;
        const float PanelHeight = 166f;
        const float PanelGap = 8f;
        const float OverlayWidth = 180f;
        const float OverlayHeight = PanelHeight * 2f + PanelGap;
        const float OverlayRightInset = 214f;
        const float OverlayTopInset = 206f;

        sealed class State
        {
            internal readonly WeakReference Owner;
            internal readonly List<Component> Views = new List<Component>();
            internal GameObject Root;
            internal State(Component owner) { Owner = new WeakReference(owner); }
        }

        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Type EquipmentSlotType;
        internal static MethodInfo GetSlot;
        internal static PropertyInfo ItemUiContextInstance;
        internal static FieldInfo GridWindowTemplate;
        internal static FieldInfo ContainedGridsTemplate;
        internal static MethodInfo GeneratedGridsShow;

        static readonly Dictionary<int, State> States = new Dictionary<int, State>();
        static bool logged;
        static bool warned;

        internal static void AfterShow(object ownerObject, object[] args)
        {
            Component owner = ownerObject as Component;
            if (owner == null || args == null || args.Length != 6 || args[1] == null || args[2] == null) return;
            try
            {
                Remove(owner);
                object itemUiContext = ItemUiContextInstance.GetValue(null, null);
                Component template = ResolveTemplate(itemUiContext);
                RectTransform root = CreateOverlayRoot(owner);
                if (template == null || root == null) return;

                State state = new State(owner);
                state.Root = root.gameObject;
                States[owner.GetInstanceID()] = state;
                EmbeddedAccessoryGridLifetime lifetime = owner.gameObject.GetComponent<EmbeddedAccessoryGridLifetime>()
                    ?? owner.gameObject.AddComponent<EmbeddedAccessoryGridLifetime>();
                lifetime.Owner = owner;
                lifetime.Root = root.gameObject;
                Add(state, template, root, args[1], args[0], args[2], itemUiContext,
                    RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue, 0);
                Add(state, template, root, args[1], args[0], args[2], itemUiContext,
                    Convert.ToInt32(Enum.Parse(EquipmentSlotType, "ArmBand", false)), 1);

                if (state.Views.Count == 0) Remove(owner);

                if (!logged && state.Views.Count > 0)
                {
                    logged = true;
                    LogInfo?.Invoke("B&A&HB embedded accessory grids initialized from EFT GeneratedGridsView; HeadBand is above ArmBand and native equipment panels were not moved.");
                }
            }
            catch (Exception exception) { Warn("Embedded accessory grids failed closed", exception); }
        }

        internal static void OwnerDisabled(Component owner)
        {
            if (owner != null) Remove(owner);
        }

        static void Add(State state, Component template, RectTransform host, object equipment, object itemContext, object controller, object itemUiContext, int slotNumber, int index)
        {
            object slotValue = Enum.ToObject(EquipmentSlotType, slotNumber);
            object slot = GetSlot.Invoke(equipment, new[] { slotValue });
            object item = ReflectionTools.ReadMember(slot, "ContainedItem");
            if (item == null || !IsOwnedAccessory(item, slotNumber)) return;

            Component view = UnityEngine.Object.Instantiate(template, host, false);
            if (view == null) return;
            view.gameObject.name = index == 0 ? "BAndHB_EmbeddedHeadBand" : "BAndHB_EmbeddedArmBand";
            RectTransform rect = view.transform as RectTransform;
            if (rect == null) { UnityEngine.Object.Destroy(view.gameObject); return; }
            IgnoreAutomaticLayout(view.gameObject);
            GeneratedGridsShow.Invoke(view, new[] { item, itemContext, controller, null, itemUiContext, false });
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, PanelWidth);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, PanelHeight);
            rect.anchoredPosition = new Vector2(0f, -index * (PanelHeight + PanelGap));

            view.gameObject.SetActive(true);
            state.Views.Add(view);
        }

        static bool IsOwnedAccessory(object item, int slotNumber)
        {
            object value = ReflectionTools.ReadMember(item, "StringTemplateId") ?? ReflectionTools.ReadMember(item, "TemplateId");
            string templateId = value?.ToString();
            if (slotNumber == RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue)
                return string.Equals(templateId, RuntimeIdentity.EmergencyHeadBandItemId, StringComparison.Ordinal);
            return WearableItemDescriptorRegistry.TryGet(templateId, out WearableItemDescriptor descriptor)
                && descriptor.Category == AccessoryCategory.ArmBand;
        }

        static Component ResolveTemplate(object itemUiContext)
        {
            object gridWindow = GridWindowTemplate.GetValue(itemUiContext);
            return gridWindow == null ? null : ContainedGridsTemplate.GetValue(gridWindow) as Component;
        }

        static RectTransform CreateOverlayRoot(Component owner)
        {
            Canvas canvas = owner.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            Canvas rootCanvas = canvas.rootCanvas ?? canvas;
            RectTransform canvasRect = rootCanvas.transform as RectTransform;
            if (canvasRect == null) return null;

            GameObject rootObject = new GameObject("BAndHB_EmbeddedAccessories", typeof(RectTransform));
            rootObject.transform.SetParent(canvasRect, false);
            IgnoreAutomaticLayout(rootObject);
            RectTransform root = (RectTransform)rootObject.transform;
            root.anchorMin = new Vector2(1f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(1f, 1f);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, OverlayWidth);
            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, OverlayHeight);
            root.anchoredPosition = new Vector2(-OverlayRightInset, -OverlayTopInset);
            root.SetAsLastSibling();
            return root;
        }

        static void IgnoreAutomaticLayout(GameObject target)
        {
            Type type = Type.GetType("UnityEngine.UI.LayoutElement, UnityEngine.UI", false);
            if (type == null) return;
            Component element = target.GetComponent(type) ?? target.AddComponent(type);
            type.GetProperty("ignoreLayout", BindingFlags.Instance | BindingFlags.Public)?.SetValue(element, true, null);
        }

        static void Remove(Component owner)
        {
            if (!States.TryGetValue(owner.GetInstanceID(), out State state)) return;
            for (int i = 0; i < state.Views.Count; i++)
            {
                Component view = state.Views[i];
                if (view == null) continue;
                try { ReflectionTools.FindInstanceMethod(view.GetType(), "Close", typeof(void))?.Invoke(view, null); } catch { }
            }
            if (state.Root != null) UnityEngine.Object.Destroy(state.Root);
            States.Remove(owner.GetInstanceID());
        }

        static void Warn(string message, Exception exception)
        {
            if (warned) return;
            warned = true;
            while (exception is TargetInvocationException invocation && invocation.InnerException != null) exception = invocation.InnerException;
            LogWarning?.Invoke(message + ": " + exception.GetType().FullName + ": " + exception.Message);
        }

        internal static void Reset()
        {
            var owners = new List<Component>();
            foreach (State state in States.Values)
                if (state.Owner.Target is Component owner) owners.Add(owner);
            for (int i = 0; i < owners.Count; i++) Remove(owners[i]);
            States.Clear();
            LogInfo = null; LogWarning = null; EquipmentSlotType = null; GetSlot = null;
            ItemUiContextInstance = null; GridWindowTemplate = null; ContainedGridsTemplate = null; GeneratedGridsShow = null;
            logged = false; warned = false;
        }
    }

    internal sealed class EmbeddedAccessoryGridLifetime : MonoBehaviour
    {
        internal Component Owner;
        internal GameObject Root;

        void OnDisable()
        {
            if (Owner != null) EmbeddedAccessoryGridRuntime.OwnerDisabled(Owner);
            else if (Root != null) UnityEngine.Object.Destroy(Root);
            Root = null;
        }
    }

    internal sealed class EmbeddedAccessoryGridPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.embedded-grids";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal EmbeddedAccessoryGridPatches(Action<string> logInfo, Action<string> logWarning) { this.logInfo = logInfo; this.logWarning = logWarning; }

        internal bool TryInstall()
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type equipmentTab = ReflectionTools.FindType("EFT.UI.EquipmentTab");
                Type equipment = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type equipmentSlot = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type itemUiContext = ReflectionTools.FindType("EFT.UI.ItemUiContext");
                Type gridWindow = ReflectionTools.FindType("EFT.UI.GridWindow");
                Type generated = ReflectionTools.FindType("EFT.UI.DragAndDrop.GeneratedGridsView");
                if (harmonyType == null || harmonyMethodType == null || equipmentTab == null || equipment == null || equipmentSlot == null || itemUiContext == null || gridWindow == null || generated == null)
                    return Fail("Embedded-grid EFT boundary is unavailable.");

                MethodInfo show = FindShow(equipmentTab, equipment);
                EmbeddedAccessoryGridRuntime.EquipmentSlotType = equipmentSlot;
                EmbeddedAccessoryGridRuntime.GetSlot = equipment.GetMethod("GetSlot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { equipmentSlot }, null);
                EmbeddedAccessoryGridRuntime.ItemUiContextInstance = itemUiContext.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.GridWindowTemplate = itemUiContext.GetField("_gridWindowTemplate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.ContainedGridsTemplate = gridWindow.GetField("_containedGridsTemplate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.GeneratedGridsShow = FindGeneratedShow(generated);
                EmbeddedAccessoryGridRuntime.LogInfo = logInfo;
                EmbeddedAccessoryGridRuntime.LogWarning = logWarning;
                if (show == null || EmbeddedAccessoryGridRuntime.GetSlot == null || EmbeddedAccessoryGridRuntime.ItemUiContextInstance == null || EmbeddedAccessoryGridRuntime.GridWindowTemplate == null || EmbeddedAccessoryGridRuntime.ContainedGridsTemplate == null || EmbeddedAccessoryGridRuntime.GeneratedGridsShow == null)
                    return Fail("Embedded-grid exact SPT 4.1 lifecycle changed: Show=" + (show != null)
                        + ", GetSlot=" + (EmbeddedAccessoryGridRuntime.GetSlot != null)
                        + ", ItemUiContext.Instance=" + (EmbeddedAccessoryGridRuntime.ItemUiContextInstance != null)
                        + ", GridWindowTemplate=" + (EmbeddedAccessoryGridRuntime.GridWindowTemplate != null)
                        + ", ContainedGridsTemplate=" + (EmbeddedAccessoryGridRuntime.ContainedGridsTemplate != null)
                        + ", GeneratedGridsView.Show=" + (EmbeddedAccessoryGridRuntime.GeneratedGridsShow != null) + ".");

                MethodInfo patch = FindPatch(harmonyType, harmonyMethodType);
                ConstructorInfo hm = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (patch == null || hm == null || unpatchSelf == null) return Fail("Embedded-grid Harmony API unavailable.");
                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                Patch(patch, harmonyMethodType, show, null, hm.Invoke(new object[] { Method(nameof(ShowPostfix)) }));
                logInfo?.Invoke("B&A&HB native embedded HeadBand/ArmBand grids installed on the EquipmentTab lifecycle.");
                return true;
            }
            catch (Exception exception) { Dispose(); return Fail("Embedded-grid installation failed safely: " + exception.Message); }
        }

        static void ShowPostfix(object __instance, object[] __args) { EmbeddedAccessoryGridRuntime.AfterShow(__instance, __args); }
        static MethodInfo Method(string n) => typeof(EmbeddedAccessoryGridPatches).GetMethod(n, BindingFlags.Static | BindingFlags.NonPublic);
        static MethodInfo FindShow(Type t, Type equipment) { foreach (MethodInfo m in t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) { ParameterInfo[] p=m.GetParameters(); if(m.Name=="Show" && p.Length==6 && p[1].ParameterType==equipment) return m; } return null; }
        static MethodInfo FindGeneratedShow(Type t) { foreach(MethodInfo m in t.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)) if(m.Name=="Show" && m.GetParameters().Length==6) return m; return null; }
        static MethodInfo FindPatch(Type ht, Type hmt) { foreach(MethodInfo m in ht.GetMethods(BindingFlags.Instance|BindingFlags.Public)) { if(m.Name!="Patch") continue; ParameterInfo[] p=m.GetParameters(); if(p.Length>2 && typeof(MethodBase).IsAssignableFrom(p[0].ParameterType)) return m; } return null; }
        void Patch(MethodInfo method, Type hmt, MethodInfo original, object prefix, object postfix) { ParameterInfo[] p=method.GetParameters(); object[] a=new object[p.Length]; a[0]=original; for(int i=1;i<p.Length;i++){if(p[i].ParameterType!=hmt)continue;if(p[i].Name.Equals("prefix",StringComparison.OrdinalIgnoreCase))a[i]=prefix;else if(p[i].Name.Equals("postfix",StringComparison.OrdinalIgnoreCase))a[i]=postfix;} method.Invoke(harmony,a); }
        bool Fail(string message) { logWarning?.Invoke(message); return false; }
        public void Dispose() { try { if(harmony!=null && unpatchSelf!=null) unpatchSelf.Invoke(harmony,null); } catch {} harmony=null; unpatchSelf=null; EmbeddedAccessoryGridRuntime.Reset(); }
    }
}
