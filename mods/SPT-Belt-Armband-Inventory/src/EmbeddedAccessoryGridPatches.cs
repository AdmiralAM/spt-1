using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class EmbeddedAccessoryGridRuntime
    {
        const float Gap = 8f;
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Type EquipmentSlotType;
        internal static FieldInfo SlotViewsField;
        internal static FieldInfo SpecialSlotsPanelField;
        internal static FieldInfo SlotPlaceField;
        static bool logged;
        static bool warned;

        internal static void AfterShow(object ownerObject)
        {
            Component owner = ownerObject as Component;
            MonoBehaviour coroutineOwner = ownerObject as MonoBehaviour;
            if (owner == null || coroutineOwner == null) return;
            try
            {
                IDictionary views = SlotViewsField.GetValue(owner) as IDictionary;
                if (views == null) return;
                object pocketsKey = Enum.Parse(EquipmentSlotType, "Pockets", false);
                object armBandKey = Enum.Parse(EquipmentSlotType, "ArmBand", false);
                object headBandKey = Enum.ToObject(EquipmentSlotType, RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);
                Component pockets = views[pocketsKey] as Component;
                Component headBand = views[headBandKey] as Component;
                Component armBand = views[armBandKey] as Component;
                if (pockets == null || headBand == null || armBand == null) return;

                Transform specialPanel = SpecialSlotsPanelField.GetValue(pockets) as Transform;
                RectTransform content = headBand.transform.parent as RectTransform;
                RectTransform specialRect = specialPanel as RectTransform;
                if (specialRect == null || content == null || !specialPanel.gameObject.activeInHierarchy) return;

                PrepareCompactRow(headBand);
                PrepareCompactRow(armBand);
                coroutineOwner.StartCoroutine(PlaceAfterNativeLayout(content, specialRect, headBand, armBand));
                if (!logged)
                {
                    logged = true;
                    LogInfo?.Invoke("B&A&HB native HeadBand/ArmBand rows placed below the Pockets special-slot panel.");
                }
            }
            catch (Exception exception)
            {
                if (warned) return;
                warned = true;
                while (exception is TargetInvocationException invocation && invocation.InnerException != null) exception = invocation.InnerException;
                LogWarning?.Invoke("Native accessory-row placement failed closed: " + exception.GetType().FullName + ": " + exception.Message);
            }
        }

        static IEnumerator PlaceAfterNativeLayout(RectTransform content, RectTransform specialRect, Component headBand, Component armBand)
        {
            yield return new WaitForEndOfFrame();
            Canvas.ForceUpdateCanvases();
            ForceRebuild(content);
            Canvas.ForceUpdateCanvases();

            Vector3[] corners = new Vector3[4];
            specialRect.GetWorldCorners(corners);
            PlaceNativeRow(headBand, corners[0]);
            RectTransform headRect = headBand.transform as RectTransform;
            float height = headRect == null ? 1f : Math.Max(1f, headRect.rect.height);
            PlaceNativeRow(armBand, corners[0] + Vector3.down * (height + Gap));
        }

        static void PrepareCompactRow(Component view)
        {
            IgnoreAutomaticLayout(view.gameObject);
            RectTransform slotPlace = SlotPlaceField?.GetValue(view) as RectTransform;
            if (slotPlace != null) slotPlace.gameObject.SetActive(false);
        }

        static void PlaceNativeRow(Component view, Vector3 worldPosition)
        {
            RectTransform rect = view.transform as RectTransform;
            if (rect == null) return;
            rect.pivot = new Vector2(0f, 1f);
            rect.position = new Vector3(worldPosition.x, worldPosition.y, rect.position.z);
            rect.SetAsLastSibling();
            view.gameObject.SetActive(true);
        }

        static void ForceRebuild(RectTransform content)
        {
            Type type = Type.GetType("UnityEngine.UI.LayoutRebuilder, UnityEngine.UI", false);
            MethodInfo method = type?.GetMethod("ForceRebuildLayoutImmediate", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(RectTransform) }, null);
            method?.Invoke(null, new object[] { content });
        }

        static void IgnoreAutomaticLayout(GameObject target)
        {
            Type type = Type.GetType("UnityEngine.UI.LayoutElement, UnityEngine.UI", false);
            if (type == null) return;
            Component element = target.GetComponent(type) ?? target.AddComponent(type);
            type.GetProperty("ignoreLayout", BindingFlags.Instance | BindingFlags.Public)?.SetValue(element, true, null);
        }

        internal static void Reset()
        {
            LogInfo = null; LogWarning = null; EquipmentSlotType = null;
            SlotViewsField = null; SpecialSlotsPanelField = null; SlotPlaceField = null;
            logged = false; warned = false;
        }
    }

    internal sealed class EmbeddedAccessoryGridPatches : IDisposable
    {
        const string HarmonyId = "com.admiralam.spt.belt-armband-inventory.embedded-grids";
        readonly Action<string> logInfo;
        readonly Action<string> logWarning;
        object harmony;
        MethodInfo unpatchSelf;

        internal EmbeddedAccessoryGridPatches(Action<string> logInfo, Action<string> logWarning)
        { this.logInfo = logInfo; this.logWarning = logWarning; }

        internal bool TryInstall()
        {
            try
            {
                Type harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", false);
                Type harmonyMethodType = Type.GetType("HarmonyLib.HarmonyMethod, 0Harmony", false);
                Type panel = ReflectionTools.FindType("EFT.UI.ContainersPanel");
                Type equipment = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type equipmentSlot = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type searchable = ReflectionTools.FindType("EFT.UI.DragAndDrop.SearchableSlotView");
                if (harmonyType == null || harmonyMethodType == null || panel == null || equipment == null || equipmentSlot == null || searchable == null)
                    return Fail("Native accessory-row boundary unavailable.");
                MethodInfo show = FindShow(panel, equipment);
                EmbeddedAccessoryGridRuntime.EquipmentSlotType = equipmentSlot;
                EmbeddedAccessoryGridRuntime.SlotViewsField = panel.GetField("_slotViews", BindingFlags.Instance | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.SpecialSlotsPanelField = searchable.GetField("_specSlotsPanel", BindingFlags.Instance | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.SlotPlaceField = searchable.BaseType?.GetField("_slotPlace", BindingFlags.Instance | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.LogInfo = logInfo;
                EmbeddedAccessoryGridRuntime.LogWarning = logWarning;
                if (show == null || EmbeddedAccessoryGridRuntime.SlotViewsField == null || EmbeddedAccessoryGridRuntime.SpecialSlotsPanelField == null || EmbeddedAccessoryGridRuntime.SlotPlaceField == null)
                    return Fail("Exact ContainersPanel/SearchableSlotView fields unavailable.");
                MethodInfo patch = FindPatch(harmonyType, harmonyMethodType);
                ConstructorInfo hm = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) });
                unpatchSelf = harmonyType.GetMethod("UnpatchSelf", BindingFlags.Instance | BindingFlags.Public);
                if (patch == null || hm == null || unpatchSelf == null) return Fail("Native accessory-row Harmony API unavailable.");
                harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                Patch(patch, harmonyMethodType, show, hm.Invoke(new object[] { Method(nameof(ShowPostfix)) }));
                logInfo?.Invoke("B&A&HB native HeadBand/ArmBand rows installed through the Pack 'n' Strap ContainersPanel pattern.");
                return true;
            }
            catch (Exception exception) { Dispose(); return Fail("Native accessory-row installation failed safely: " + exception.Message); }
        }

        static void ShowPostfix(object __instance) { EmbeddedAccessoryGridRuntime.AfterShow(__instance); }
        static MethodInfo Method(string name) => typeof(EmbeddedAccessoryGridPatches).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
        static MethodInfo FindShow(Type type, Type equipment) { foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) { ParameterInfo[] p = method.GetParameters(); if (method.Name == "Show" && p.Length == 6 && p[1].ParameterType == equipment) return method; } return null; }
        static MethodInfo FindPatch(Type harmonyType, Type harmonyMethodType) { foreach (MethodInfo method in harmonyType.GetMethods(BindingFlags.Instance | BindingFlags.Public)) { if (method.Name != "Patch") continue; ParameterInfo[] p = method.GetParameters(); if (p.Length > 2 && typeof(MethodBase).IsAssignableFrom(p[0].ParameterType)) return method; } return null; }
        void Patch(MethodInfo method, Type harmonyMethodType, MethodInfo original, object postfix) { ParameterInfo[] p = method.GetParameters(); object[] args = new object[p.Length]; args[0] = original; for (int i = 1; i < p.Length; i++) if (p[i].ParameterType == harmonyMethodType && p[i].Name.Equals("postfix", StringComparison.OrdinalIgnoreCase)) args[i] = postfix; method.Invoke(harmony, args); }
        bool Fail(string message) { logWarning?.Invoke(message); return false; }
        public void Dispose() { try { if (harmony != null && unpatchSelf != null) unpatchSelf.Invoke(harmony, null); } catch { } harmony = null; unpatchSelf = null; EmbeddedAccessoryGridRuntime.Reset(); }
    }
}
