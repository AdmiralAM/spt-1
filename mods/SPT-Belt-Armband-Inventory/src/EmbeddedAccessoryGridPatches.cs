using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class EmbeddedAccessoryGridRuntime
    {
        const float HorizontalGap = 4f;
        const float VerticalGap = 4f;
        const float HeadBandRightOffset = 4f;
        const float HeadBandDownOffset = 8f;
        const float HeaderHeight = 22f;
        const float MinimumPanelWidth = 128f;
        internal static Action<string> LogInfo;
        internal static Action<string> LogWarning;
        internal static Type EquipmentSlotType;
        internal static Type EquipmentTabType;
        internal static FieldInfo SlotViewsField;
        internal static FieldInfo SpecialSlotsPanelField;
        internal static FieldInfo SlotPlaceField;
        internal static FieldInfo SlotBackgroundField;
        internal static FieldInfo SearchableItemViewField;
        internal static FieldInfo GridsContainerField;
        internal static FieldInfo ContainedGridsViewField;
        static bool logged;
        static bool warned;

        internal static void AfterShow(object ownerObject)
        {
            Component owner = ownerObject as Component;
            MonoBehaviour coroutineOwner = ownerObject as MonoBehaviour;
            if (owner == null || coroutineOwner == null) return;
            // EquipmentTab is the character paper-doll on the left. Its accepted
            // Face/Headwear/HeadBand arrangement has a separate native owner and
            // must never be compacted or moved by the stash ContainersPanel layout.
            if (EquipmentTabType != null && owner.GetComponentInParent(EquipmentTabType) != null) return;
            try
            {
                IDictionary views = SlotViewsField.GetValue(owner) as IDictionary;
                if (views == null) return;
                object pocketsKey = Enum.Parse(EquipmentSlotType, "Pockets", false);
                object armBandKey = Enum.Parse(EquipmentSlotType, "ArmBand", false);
                object beltKey = Enum.ToObject(EquipmentSlotType, RuntimeIdentity.DedicatedBeltEquipmentSlotValue);
                object headBandKey = Enum.ToObject(EquipmentSlotType, RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue);
                Component pockets = views[pocketsKey] as Component;
                Component belt = views[beltKey] as Component;
                Component headBand = views[headBandKey] as Component;
                Component armBand = views[armBandKey] as Component;
                if (pockets == null || belt == null || headBand == null || armBand == null) return;

                Transform specialPanel = SpecialSlotsPanelField.GetValue(pockets) as Transform;
                RectTransform content = headBand.transform.parent as RectTransform;
                RectTransform specialRect = specialPanel as RectTransform;
                if (specialRect == null || content == null || !specialPanel.gameObject.activeInHierarchy) return;

                PrepareCompactRow(headBand);
                PrepareCompactRow(armBand);
                coroutineOwner.StartCoroutine(PlaceAfterNativeLayout(content, specialRect, belt, headBand, armBand));
            }
            catch (Exception exception)
            {
                if (warned) return;
                warned = true;
                while (exception is TargetInvocationException invocation && invocation.InnerException != null) exception = invocation.InnerException;
                LogWarning?.Invoke("Native accessory-row placement failed closed: " + exception.GetType().FullName + ": " + exception.Message);
            }
        }

        static IEnumerator PlaceAfterNativeLayout(RectTransform content, RectTransform specialRect, Component belt, Component headBand, Component armBand)
        {
            for (int settle = 0; settle < 6; settle++)
            {
                yield return new WaitForEndOfFrame();
                CompactNativeRow(headBand);
                CompactNativeRow(armBand);
                Canvas.ForceUpdateCanvases();
                ForceRebuild(content);
                Canvas.ForceUpdateCanvases();

                Vector3 specialBottomLeft = VisibleBottomLeftIn(content, specialRect);
                Vector3 beltTopRight = TopRightIn(content, belt);
                Vector3 headBandAnchor = specialBottomLeft
                    + Vector3.right * HeadBandRightOffset
                    + Vector3.down * (VerticalGap + HeadBandDownOffset);
                Vector3 armBandAnchor = beltTopRight + Vector3.right * HorizontalGap;
                PlaceNativeRow(headBand, content.TransformPoint(headBandAnchor));
                PlaceNativeRow(armBand, content.TransformPoint(armBandAnchor));
            }
            if (!logged)
            {
                logged = true;
                LogInfo?.Invoke("B&A&HB ACCESSORY FLOW PROOF: HeadBand follows the live Special Slots lower-left edge; ArmBand follows the live Belt upper-right edge; fixed page offsets=False.");
            }
        }

        static void PrepareCompactRow(Component view)
        {
            IgnoreAutomaticLayout(view.gameObject);
            RectTransform slotPlace = SlotPlaceField?.GetValue(view) as RectTransform;
            HideRootBranch(view.transform, slotPlace);
            Component slotBackground = SlotBackgroundField?.GetValue(view) as Component;
            HideRootBranch(view.transform, slotBackground == null ? null : slotBackground.transform);
        }

        static void HideRootBranch(Transform row, Transform descendant)
        {
            if (row == null || descendant == null || descendant == row) return;
            Transform branch = descendant;
            while (branch.parent != null && branch.parent != row) branch = branch.parent;
            if (branch.parent == row) branch.gameObject.SetActive(false);
        }

        static float CompactNativeRow(Component view)
        {
            PrepareCompactRow(view);
            RectTransform rect = view.transform as RectTransform;
            Component searchableItem = SearchableItemViewField?.GetValue(view) as Component;
            RectTransform grids = searchableItem == null ? null : GridsContainerField?.GetValue(searchableItem) as RectTransform;
            if (rect == null || grids == null) return 1f;

            ForceRebuild(grids);
            MeasureGridViews(rect, searchableItem, out float gridWidth, out float gridHeight);
            float width = Math.Max(MinimumPanelWidth, gridWidth);
            float height = HeaderHeight + gridHeight;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            ForceRebuild(rect);
            return height;
        }

        static void MeasureGridViews(RectTransform row, Component searchableItem, out float width, out float height)
        {
            width = 1f;
            height = 1f;
            object contained = ContainedGridsViewField?.GetValue(searchableItem);
            IEnumerable views = ReflectionTools.ReadMember(contained, "GridViews") as IEnumerable
                ?? ReflectionTools.ReadMember(contained, "_gridViews") as IEnumerable;
            if (views == null) return;
            bool measured = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            Vector3[] corners = new Vector3[4];
            foreach (object entry in views)
            {
                RectTransform grid = (entry as Component)?.transform as RectTransform;
                if (grid == null || !grid.gameObject.activeInHierarchy) continue;
                grid.GetWorldCorners(corners);
                for (int i = 0; i < corners.Length; i++)
                {
                    Vector3 point = row.InverseTransformPoint(corners[i]);
                    minX = Math.Min(minX, point.x); maxX = Math.Max(maxX, point.x);
                    minY = Math.Min(minY, point.y); maxY = Math.Max(maxY, point.y);
                }
                measured = true;
            }
            if (!measured) return;
            width = Math.Max(1f, maxX - minX);
            height = Math.Max(1f, maxY - minY);
        }

        static Vector3 VisibleBottomLeftIn(RectTransform space, RectTransform root)
        {
            float left = float.MaxValue;
            float bottom = float.MaxValue;
            Vector3[] corners = new Vector3[4];
            for (int childIndex = 0; childIndex < root.childCount; childIndex++)
            {
                RectTransform child = root.GetChild(childIndex) as RectTransform;
                MeasureVisibleRect(child);
                if (child == null) continue;
                for (int grandchildIndex = 0; grandchildIndex < child.childCount; grandchildIndex++)
                {
                    MeasureVisibleRect(child.GetChild(grandchildIndex) as RectTransform);
                }
            }
            if (left < float.MaxValue && bottom < float.MaxValue) return new Vector3(left, bottom, 0f);
            root.GetWorldCorners(corners);
            return space.InverseTransformPoint(corners[0]);

            void MeasureVisibleRect(RectTransform rect)
            {
                if (rect == null || !rect.gameObject.activeInHierarchy || rect.rect.width < 1f || rect.rect.height < 1f) return;
                rect.GetWorldCorners(corners);
                for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    Vector3 point = space.InverseTransformPoint(corners[cornerIndex]);
                    left = Math.Min(left, point.x);
                    bottom = Math.Min(bottom, point.y);
                }
            }
        }

        static Vector3 TopRightIn(RectTransform space, Component view)
        {
            Component searchableItem = SearchableItemViewField?.GetValue(view) as Component;
            object contained = searchableItem == null ? null : ContainedGridsViewField?.GetValue(searchableItem);
            IEnumerable grids = ReflectionTools.ReadMember(contained, "GridViews") as IEnumerable
                ?? ReflectionTools.ReadMember(contained, "_gridViews") as IEnumerable;
            float right = float.MinValue;
            float top = float.MinValue;
            Vector3[] corners = new Vector3[4];
            if (grids != null)
            {
                foreach (object entry in grids)
                {
                    RectTransform grid = (entry as Component)?.transform as RectTransform;
                    if (grid == null || !grid.gameObject.activeInHierarchy) continue;
                    grid.GetWorldCorners(corners);
                    for (int i = 0; i < corners.Length; i++)
                    {
                        Vector3 point = space.InverseTransformPoint(corners[i]);
                        right = Math.Max(right, point.x);
                    }
                }
            }
            RectTransform fallback = view.transform as RectTransform;
            if (fallback == null) return Vector3.zero;
            fallback.GetWorldCorners(corners);
            bool useFallbackRight = right == float.MinValue;
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 point = space.InverseTransformPoint(corners[i]);
                top = Math.Max(top, point.y);
                if (useFallbackRight) right = Math.Max(right, point.x);
            }
            return new Vector3(right, top, 0f);
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
            LogInfo = null; LogWarning = null; EquipmentSlotType = null; EquipmentTabType = null;
            SlotViewsField = null; SpecialSlotsPanelField = null; SlotPlaceField = null; SlotBackgroundField = null;
            SearchableItemViewField = null; GridsContainerField = null; ContainedGridsViewField = null;
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
                Type equipmentTab = ReflectionTools.FindType("EFT.UI.EquipmentTab");
                Type equipment = ReflectionTools.FindType("EFT.InventoryLogic.InventoryEquipment");
                Type equipmentSlot = ReflectionTools.FindType("EFT.InventoryLogic.EquipmentSlot");
                Type searchable = ReflectionTools.FindType("EFT.UI.DragAndDrop.SearchableSlotView");
                Type searchableItem = ReflectionTools.FindType("EFT.UI.DragAndDrop.SearchableItemView");
                if (harmonyType == null || harmonyMethodType == null || panel == null || equipmentTab == null || equipment == null || equipmentSlot == null || searchable == null || searchableItem == null)
                    return Fail("Native accessory-row boundary unavailable.");
                MethodInfo show = FindShow(panel, equipment);
                EmbeddedAccessoryGridRuntime.EquipmentSlotType = equipmentSlot;
                EmbeddedAccessoryGridRuntime.EquipmentTabType = equipmentTab;
                EmbeddedAccessoryGridRuntime.SlotViewsField = panel.GetField("_slotViews", BindingFlags.Instance | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.SpecialSlotsPanelField = searchable.GetField("_specSlotsPanel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.SlotPlaceField = searchable.BaseType?.GetField("_slotPlace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.SlotBackgroundField = searchable.BaseType?.GetField("_slotBackground", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.SearchableItemViewField = searchable.GetField("_searchableItemView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.GridsContainerField = searchableItem.GetField("_gridsContainer", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.ContainedGridsViewField = searchableItem.GetField("_containedGridsView", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                EmbeddedAccessoryGridRuntime.LogInfo = logInfo;
                EmbeddedAccessoryGridRuntime.LogWarning = logWarning;
                if (show == null || EmbeddedAccessoryGridRuntime.SlotViewsField == null || EmbeddedAccessoryGridRuntime.SpecialSlotsPanelField == null || EmbeddedAccessoryGridRuntime.SlotPlaceField == null || EmbeddedAccessoryGridRuntime.SlotBackgroundField == null || EmbeddedAccessoryGridRuntime.SearchableItemViewField == null || EmbeddedAccessoryGridRuntime.GridsContainerField == null || EmbeddedAccessoryGridRuntime.ContainedGridsViewField == null)
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
