using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    // EFT 4.1.3 Gear Panel is a fixed RectTransform map, not a LayoutGroup. Slot16
    // participates in the same _slotViews map as the native equipment slots. This
    // class is the sole placement owner: reserve one compact row, move every native
    // slot down by that row, and put the mapped slot16 into the original Headwear
    // screen position. No clone projection, no retry positioner, no polling.
    internal static class HeadBandRenderSettle
    {
        const float HeadBandCompactHeight = 44f;
        const float HeadBandGap = 4f;
        const float StructuralOffset = HeadBandCompactHeight + HeadBandGap;

        sealed class ReflowState
        {
            internal readonly WeakReference EquipmentTab;
            internal readonly Dictionary<int, Vector2> OriginalPositions = new Dictionary<int, Vector2>();
            internal float OriginalPreferredHeight;
            internal bool HasPreferredHeight;
            internal Vector3 OriginalGearWorldPosition;
            internal bool HasGearWorldPosition;

            internal ReflowState(Component equipmentTab)
            {
                EquipmentTab = new WeakReference(equipmentTab);
            }
        }

        static readonly Dictionary<int, ReflowState> States = new Dictionary<int, ReflowState>();
        static bool proofLogged;
        static bool failureLogged;

        internal static void OnHeadwearShown(Component headwearView)
        {
            if (headwearView == null || headwearView.transform == null) return;
            TryApplyStructuralReflow(headwearView);
        }

        static bool TryApplyStructuralReflow(Component headwearView)
        {
            if (DedicatedSlotPresentationRuntime.EquipmentTabType == null
                || DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField == null
                || DedicatedSlotPresentationRuntime.HeadBandSlotKey == null)
                return false;

            try
            {
                Component equipmentTab = headwearView.GetComponentInParent(DedicatedSlotPresentationRuntime.EquipmentTabType);
                if (equipmentTab == null || equipmentTab.transform == null) return false;

                IDictionary slotViews = DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;
                if (slotViews == null || !slotViews.Contains(DedicatedSlotPresentationRuntime.HeadBandSlotKey)) return false;

                Component headBandView = slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey] as Component;
                RectTransform headBandRect = headBandView == null ? null : headBandView.transform as RectTransform;
                RectTransform headwearRect = headwearView.transform as RectTransform;
                RectTransform gearRect = equipmentTab.transform as RectTransform;
                if (headBandView == null || headBandRect == null || headwearRect == null || gearRect == null) return false;

                int key = equipmentTab.GetInstanceID();
                ReflowState state;
                if (!States.TryGetValue(key, out state) || state == null || state.EquipmentTab.Target == null)
                {
                    state = CaptureState(equipmentTab, slotViews, gearRect);
                    States[key] = state;
                }

                Vector2 originalHeadwear;
                if (!state.OriginalPositions.TryGetValue(headwearView.GetInstanceID(), out originalHeadwear))
                {
                    originalHeadwear = headwearRect.anchoredPosition;
                    state.OriginalPositions[headwearView.GetInstanceID()] = originalHeadwear;
                }

                Vector3 originalHeadwearWorld = headwearRect.TransformPoint(headwearRect.rect.center);
                Vector3 topBefore = GearTopWorld(gearRect);

                foreach (DictionaryEntry entry in slotViews)
                {
                    Component view = entry.Value as Component;
                    if (view == null || ReferenceEquals(view, headBandView)) continue;
                    RectTransform rect = view.transform as RectTransform;
                    if (rect == null) continue;

                    int viewId = view.GetInstanceID();
                    Vector2 original;
                    if (!state.OriginalPositions.TryGetValue(viewId, out original))
                    {
                        original = rect.anchoredPosition;
                        state.OriginalPositions[viewId] = original;
                    }
                    rect.anchoredPosition = new Vector2(original.x, original.y - StructuralOffset);
                }

                if (!ReferenceEquals(headBandView.transform.parent, equipmentTab.transform))
                    headBandView.transform.SetParent(equipmentTab.transform, false);
                headBandView.transform.SetSiblingIndex(headwearView.transform.GetSiblingIndex());
                headBandRect.anchorMin = headwearRect.anchorMin;
                headBandRect.anchorMax = headwearRect.anchorMax;
                headBandRect.pivot = headwearRect.pivot;
                headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1f, headwearRect.rect.width));
                headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, HeadBandCompactHeight);
                headBandRect.anchoredPosition = originalHeadwear;
                headBandView.gameObject.SetActive(true);

                ReserveGearPanelHeight(equipmentTab, gearRect, state);
                Canvas.ForceUpdateCanvases();

                Vector3 topAfter = GearTopWorld(gearRect);
                Vector3 topCorrection = topBefore - topAfter;
                if (topCorrection.sqrMagnitude > 0.0001f)
                    gearRect.position += topCorrection;

                Vector3 headBandWorld = headBandRect.TransformPoint(headBandRect.rect.center);
                float worldDelta = Vector3.Distance(headBandWorld, originalHeadwearWorld);
                bool mapExact = ReferenceEquals(slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey], headBandView);

                if (!proofLogged)
                {
                    proofLogged = true;
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB HEADBAND SCREEN REFLOW PROOF: slot16 instance=" + headBandView.GetInstanceID()
                        + "; mapExact=" + mapExact
                        + "; local=" + headBandRect.anchoredPosition.x.ToString("0.0") + "," + headBandRect.anchoredPosition.y.ToString("0.0")
                        + "; nativeY=-" + StructuralOffset.ToString("0.0")
                        + "; reservedHeight=+" + StructuralOffset.ToString("0.0")
                        + "; gearTopCorrectionY=" + topCorrection.y.ToString("0.00")
                        + "; worldDeltaFromOriginalHeadwear=" + worldDelta.ToString("0.00") + ".");
                }
                return true;
            }
            catch (Exception exception)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    DedicatedSlotPresentationRuntime.LogWarning?.Invoke(
                        "B&A&HB HeadBand structural Gear Panel reflow failed safely: "
                        + exception.GetType().FullName + ": " + exception.Message);
                }
                return false;
            }
        }

        static ReflowState CaptureState(Component equipmentTab, IDictionary slotViews, RectTransform gearRect)
        {
            ReflowState state = new ReflowState(equipmentTab);
            foreach (DictionaryEntry entry in slotViews)
            {
                Component view = entry.Value as Component;
                RectTransform rect = view == null ? null : view.transform as RectTransform;
                if (view == null || rect == null) continue;
                if (entry.Key != null && DedicatedSlotPresentationRuntime.HeadBandSlotKey != null
                    && entry.Key.Equals(DedicatedSlotPresentationRuntime.HeadBandSlotKey)) continue;
                state.OriginalPositions[view.GetInstanceID()] = rect.anchoredPosition;
            }

            state.OriginalGearWorldPosition = gearRect.position;
            state.HasGearWorldPosition = true;

            Component layoutElement = FindComponentByTypeName(equipmentTab.transform, "UnityEngine.UI.LayoutElement");
            if (layoutElement != null)
            {
                PropertyInfo preferredHeight = layoutElement.GetType().GetProperty("preferredHeight", BindingFlags.Instance | BindingFlags.Public);
                if (preferredHeight != null && preferredHeight.CanRead && preferredHeight.CanWrite)
                {
                    state.OriginalPreferredHeight = Convert.ToSingle(preferredHeight.GetValue(layoutElement, null));
                    state.HasPreferredHeight = true;
                }
            }
            return state;
        }

        static void ReserveGearPanelHeight(Component equipmentTab, RectTransform gearRect, ReflowState state)
        {
            Component layoutElement = FindComponentByTypeName(equipmentTab.transform, "UnityEngine.UI.LayoutElement");
            if (layoutElement == null) return;
            PropertyInfo preferredHeight = layoutElement.GetType().GetProperty("preferredHeight", BindingFlags.Instance | BindingFlags.Public);
            if (preferredHeight == null || !preferredHeight.CanWrite) return;
            float baseline = state.HasPreferredHeight && state.OriginalPreferredHeight >= 0f
                ? state.OriginalPreferredHeight
                : Mathf.Max(1f, gearRect.rect.height);
            preferredHeight.SetValue(layoutElement, baseline + StructuralOffset, null);
        }

        static Vector3 GearTopWorld(RectTransform gearRect)
        {
            return gearRect.TransformPoint(new Vector3(gearRect.rect.center.x, gearRect.rect.yMax, 0f));
        }

        static Component FindComponentByTypeName(Transform transform, string fullName)
        {
            if (transform == null) return null;
            Component[] components = transform.gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && string.Equals(component.GetType().FullName, fullName, StringComparison.Ordinal))
                    return component;
            }
            return null;
        }

        internal static void Reset()
        {
            foreach (KeyValuePair<int, ReflowState> pair in States)
            {
                ReflowState state = pair.Value;
                Component equipmentTab = state == null ? null : state.EquipmentTab.Target as Component;
                if (equipmentTab == null) continue;

                IDictionary slotViews = DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField == null
                    ? null
                    : DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;
                if (slotViews != null)
                {
                    foreach (DictionaryEntry entry in slotViews)
                    {
                        Component view = entry.Value as Component;
                        RectTransform rect = view == null ? null : view.transform as RectTransform;
                        Vector2 original;
                        if (view != null && rect != null && state.OriginalPositions.TryGetValue(view.GetInstanceID(), out original))
                            rect.anchoredPosition = original;
                    }
                }

                if (state.HasPreferredHeight)
                {
                    Component layoutElement = FindComponentByTypeName(equipmentTab.transform, "UnityEngine.UI.LayoutElement");
                    PropertyInfo preferredHeight = layoutElement == null ? null : layoutElement.GetType().GetProperty("preferredHeight", BindingFlags.Instance | BindingFlags.Public);
                    if (preferredHeight != null && preferredHeight.CanWrite)
                        preferredHeight.SetValue(layoutElement, state.OriginalPreferredHeight, null);
                }

                if (state.HasGearWorldPosition)
                    equipmentTab.transform.position = state.OriginalGearWorldPosition;
            }

            States.Clear();
            proofLogged = false;
            failureLogged = false;
        }
    }
}
