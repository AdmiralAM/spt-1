using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    // EFT 4.1.3 Gear Panel is a fixed RectTransform map, not a LayoutGroup. Slot16
    // participates in the same _slotViews map as the native equipment slots. This
    // class is the sole placement owner: move every native slot down by one compact
    // row and put mapped slot16 into the original Headwear position. The host panel's
    // LayoutElement/RectTransform is deliberately untouched: changing preferredHeight
    // caused EFT's parent layout to move the whole character panel off-screen on first
    // stash entry. No clone projection, canvas refresh, retry positioner or polling.
    internal static class HeadBandRenderSettle
    {
        const float HeadBandCompactHeight = 44f;
        const float HeadBandGap = 4f;
        const float StructuralOffset = HeadBandCompactHeight + HeadBandGap;

        sealed class ReflowState
        {
            internal readonly WeakReference EquipmentTab;
            internal readonly Dictionary<int, Vector2> OriginalPositions = new Dictionary<int, Vector2>();

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
                if (headBandView == null || headBandRect == null || headwearRect == null) return false;

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

                Vector3 panelWorldBefore = equipmentTab.transform.position;

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

                float panelWorldDelta = Vector3.Distance(equipmentTab.transform.position, panelWorldBefore);
                bool mapExact = ReferenceEquals(slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey], headBandView);

                if (!proofLogged)
                {
                    proofLogged = true;
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB HEADBAND FIRST-RENDER PROOF: slot16 instance=" + headBandView.GetInstanceID()
                        + "; mapExact=" + mapExact
                        + "; local=" + headBandRect.anchoredPosition.x.ToString("0.0") + "," + headBandRect.anchoredPosition.y.ToString("0.0")
                        + "; nativeY=-" + StructuralOffset.ToString("0.0")
                        + "; panelLayoutMutation=False"
                        + "; panelWorldDelta=" + panelWorldDelta.ToString("0.00")
                        + "; synchronous=True.");
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

            return state;
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

            }

            States.Clear();
            proofLogged = false;
            failureLogged = false;
        }
    }
}
