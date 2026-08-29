using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    // EFT 4.1.3 EquipmentTab/Gear Panel does not use a LayoutGroup for equipment
    // slots. Physical RC1 topology proved that the slots are fixed top-anchored
    // RectTransforms under Gear Panel, while Gear Panel itself is sized through a
    // LayoutElement + ContentSizeFitter. Insert slot16 structurally by reserving one
    // compact row at the top of Gear Panel and shifting the complete native slot map
    // down by exactly HeadBand height + gap. No polling, retries, or scene scan.
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
            internal bool Applied;

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
            if (headwearView == null
                || headwearView.transform == null
                || DedicatedSlotPresentationRuntime.EquipmentTabType == null
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
                if (headBandView == null || headBandView.transform == null) return false;

                RectTransform headBandRect = headBandView.transform as RectTransform;
                RectTransform headwearRect = headwearView.transform as RectTransform;
                RectTransform gearRect = equipmentTab.transform as RectTransform;
                if (headBandRect == null || headwearRect == null || gearRect == null) return false;

                int key = equipmentTab.GetInstanceID();
                ReflowState state;
                if (!States.TryGetValue(key, out state) || state == null || state.EquipmentTab.Target == null)
                {
                    state = CaptureState(equipmentTab, slotViews);
                    States[key] = state;
                }

                Vector2 originalHeadwear;
                if (!state.OriginalPositions.TryGetValue(headwearView.GetInstanceID(), out originalHeadwear))
                {
                    originalHeadwear = headwearRect.anchoredPosition;
                    state.OriginalPositions[headwearView.GetInstanceID()] = originalHeadwear;
                }

                // Every native EquipmentTab slot keeps its exact authored X and spacing;
                // only Y is translated by one compact row. Reapply from captured originals
                // so repeated SlotView.Show calls are idempotent and cannot accumulate drift.
                foreach (DictionaryEntry entry in slotViews)
                {
                    Component view = entry.Value as Component;
                    if (view == null || view.transform == null || ReferenceEquals(view, headBandView)) continue;
                    RectTransform rect = view.transform as RectTransform;
                    if (rect == null) continue;

                    Vector2 original;
                    int viewId = view.GetInstanceID();
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
                state.Applied = true;

                if (!proofLogged)
                {
                    proofLogged = true;
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB HEADBAND STRUCTURAL REFLOW PROOF: fixed Gear Panel contract applied; slot16 occupies original Headwear position="
                        + originalHeadwear.x.ToString("0.0") + "," + originalHeadwear.y.ToString("0.0")
                        + "; native slots translatedY=-" + StructuralOffset.ToString("0.0")
                        + "; reservedHeight=+" + StructuralOffset.ToString("0.0") + ".");
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

        static ReflowState CaptureState(Component equipmentTab, IDictionary slotViews)
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
            // Plugin teardown normally destroys the screen with the session. Restore
            // any still-live Gear Panel positions/height defensively for hot reloads.
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
            }

            States.Clear();
            proofLogged = false;
            failureLogged = false;
        }
    }
}
