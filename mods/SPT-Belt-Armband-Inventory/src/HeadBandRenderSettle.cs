using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    // EFT 4.1.3 Gear Panel is a fixed RectTransform map, not a LayoutGroup.
    // Physical RC1 also proved that _slotViews[16] can already contain a provisional
    // "16 Slot" projection before the dedicated presentation path runs. Reusing that
    // object makes geometry evidence ambiguous because unknown components on the
    // provisional projection can keep driving the rendered hierarchy. Isolate slot16
    // once after its real SlotView.Show binding by cloning the already-bound view,
    // replacing the map entry, and destroying the provisional source. The isolated
    // clone then participates in the deterministic fixed-panel reflow below.
    internal static class HeadBandRenderSettle
    {
        const string IsolatedName = "B&A&HB HeadBand Isolated Slot16";
        const float HeadBandCompactHeight = 44f;
        const float HeadBandGap = 4f;
        const float StructuralOffset = HeadBandCompactHeight + HeadBandGap;

        sealed class ReflowState
        {
            internal readonly WeakReference EquipmentTab;
            internal readonly Dictionary<int, Vector2> OriginalPositions = new Dictionary<int, Vector2>();
            internal float OriginalPreferredHeight;
            internal bool HasPreferredHeight;
            internal Component IsolatedHeadBand;

            internal ReflowState(Component equipmentTab) { EquipmentTab = new WeakReference(equipmentTab); }
        }

        static readonly Dictionary<int, ReflowState> States = new Dictionary<int, ReflowState>();
        static bool isolationProofLogged;
        static bool reflowProofLogged;
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

                int key = equipmentTab.GetInstanceID();
                ReflowState state;
                if (!States.TryGetValue(key, out state) || state == null || state.EquipmentTab.Target == null)
                {
                    state = CaptureState(equipmentTab, slotViews);
                    States[key] = state;
                }

                Component mappedHeadBand = slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey] as Component;
                if (mappedHeadBand == null || mappedHeadBand.transform == null) return false;
                Component headBandView = EnsureIsolatedSlot16(equipmentTab, slotViews, mappedHeadBand, state);
                if (headBandView == null || headBandView.transform == null) return false;

                RectTransform headBandRect = headBandView.transform as RectTransform;
                RectTransform headwearRect = headwearView.transform as RectTransform;
                RectTransform gearRect = equipmentTab.transform as RectTransform;
                if (headBandRect == null || headwearRect == null || gearRect == null) return false;

                Vector2 originalHeadwear;
                if (!state.OriginalPositions.TryGetValue(headwearView.GetInstanceID(), out originalHeadwear))
                {
                    originalHeadwear = headwearRect.anchoredPosition;
                    state.OriginalPositions[headwearView.GetInstanceID()] = originalHeadwear;
                }

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

                if (!reflowProofLogged)
                {
                    reflowProofLogged = true;
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB HEADBAND STRUCTURAL REFLOW PROOF: isolated slot16 instance=" + headBandView.GetInstanceID()
                        + " occupies original Headwear position=" + originalHeadwear.x.ToString("0.0") + "," + originalHeadwear.y.ToString("0.0")
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
                    DedicatedSlotPresentationRuntime.LogWarning?.Invoke("B&A&HB HeadBand structural Gear Panel reflow failed safely: " + exception.GetType().FullName + ": " + exception.Message);
                }
                return false;
            }
        }

        static Component EnsureIsolatedSlot16(Component equipmentTab, IDictionary slotViews, Component mapped, ReflowState state)
        {
            if (state.IsolatedHeadBand != null)
            {
                if (!ReferenceEquals(slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey], state.IsolatedHeadBand))
                    slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey] = state.IsolatedHeadBand;
                return state.IsolatedHeadBand;
            }

            string sourceName = mapped.gameObject.name;
            int sourceId = mapped.GetInstanceID();
            Component isolated = UnityEngine.Object.Instantiate(mapped);
            isolated.gameObject.name = IsolatedName;
            isolated.transform.SetParent(equipmentTab.transform, false);
            isolated.transform.SetSiblingIndex(mapped.transform.GetSiblingIndex());
            isolated.gameObject.SetActive(true);
            slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey] = isolated;
            state.IsolatedHeadBand = isolated;

            // The clone contains the already-bound SlotView visual state. Destroy the
            // provisional source only after the map entry has been atomically replaced.
            if (!ReferenceEquals(mapped, isolated)) UnityEngine.Object.Destroy(mapped.gameObject);

            if (!isolationProofLogged)
            {
                isolationProofLogged = true;
                DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                    "B&A&HB HEADBAND IDENTITY PROOF: provisional slot16 source name=" + sourceName
                    + ", instance=" + sourceId + " replaced by isolated rendered instance=" + isolated.GetInstanceID()
                    + ", mapExact=" + ReferenceEquals(slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey], isolated) + ".");
            }
            return isolated;
        }

        static ReflowState CaptureState(Component equipmentTab, IDictionary slotViews)
        {
            ReflowState state = new ReflowState(equipmentTab);
            foreach (DictionaryEntry entry in slotViews)
            {
                Component view = entry.Value as Component;
                RectTransform rect = view == null ? null : view.transform as RectTransform;
                if (view == null || rect == null) continue;
                if (entry.Key != null && DedicatedSlotPresentationRuntime.HeadBandSlotKey != null && entry.Key.Equals(DedicatedSlotPresentationRuntime.HeadBandSlotKey)) continue;
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
            float baseline = state.HasPreferredHeight && state.OriginalPreferredHeight >= 0f ? state.OriginalPreferredHeight : Mathf.Max(1f, gearRect.rect.height);
            preferredHeight.SetValue(layoutElement, baseline + StructuralOffset, null);
        }

        static Component FindComponentByTypeName(Transform transform, string fullName)
        {
            if (transform == null) return null;
            Component[] components = transform.gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component != null && string.Equals(component.GetType().FullName, fullName, StringComparison.Ordinal)) return component;
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
                IDictionary slotViews = DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField == null ? null : DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;
                if (slotViews != null)
                {
                    foreach (DictionaryEntry entry in slotViews)
                    {
                        Component view = entry.Value as Component;
                        RectTransform rect = view == null ? null : view.transform as RectTransform;
                        Vector2 original;
                        if (view != null && rect != null && state.OriginalPositions.TryGetValue(view.GetInstanceID(), out original)) rect.anchoredPosition = original;
                    }
                }
                if (state.HasPreferredHeight)
                {
                    Component layoutElement = FindComponentByTypeName(equipmentTab.transform, "UnityEngine.UI.LayoutElement");
                    PropertyInfo preferredHeight = layoutElement == null ? null : layoutElement.GetType().GetProperty("preferredHeight", BindingFlags.Instance | BindingFlags.Public);
                    if (preferredHeight != null && preferredHeight.CanWrite) preferredHeight.SetValue(layoutElement, state.OriginalPreferredHeight, null);
                }
            }
            States.Clear();
            isolationProofLogged = false;
            reflowProofLogged = false;
            failureLogged = false;
        }
    }
}
