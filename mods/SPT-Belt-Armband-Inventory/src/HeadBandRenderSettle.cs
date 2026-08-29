using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    // Prefer real parent-layout participation for slot16 when EFT exposes a native
    // LayoutGroup. If the EquipmentTab uses fixed serialized RectTransforms instead,
    // emit one exact topology snapshot and keep the old fixed placement only as a
    // diagnostic fallback. Work remains bounded to the next few canvas passes.
    internal static class HeadBandRenderSettle
    {
        const int MaxRenderPasses = 6;
        const float HeadBandCompactHeight = 44f;
        const float HeadBandGap = 4f;

        sealed class PendingLayout
        {
            internal readonly WeakReference Headwear;
            internal int Passes;
            internal bool NativeLayout;

            internal PendingLayout(Component headwear, bool nativeLayout)
            {
                Headwear = new WeakReference(headwear);
                NativeLayout = nativeLayout;
            }
        }

        static readonly List<PendingLayout> Pending = new List<PendingLayout>();
        static bool subscribed;
        static bool proofLogged;
        static bool topologyLogged;
        static bool nativeInsertionLogged;

        internal static void OnHeadwearShown(Component headwearView)
        {
            if (headwearView == null || headwearView.transform == null) return;

            for (int i = 0; i < Pending.Count; i++)
            {
                Component existing = Pending[i].Headwear.Target as Component;
                if (existing == null)
                {
                    Pending.RemoveAt(i--);
                    continue;
                }

                if (ReferenceEquals(existing, headwearView))
                {
                    EnsureSubscribed();
                    return;
                }
            }

            bool nativeLayout;
            if (!TryApply(headwearView, out nativeLayout)) return;
            Pending.Add(new PendingLayout(headwearView, nativeLayout));
            EnsureSubscribed();
        }

        static void EnsureSubscribed()
        {
            if (subscribed) return;
            Canvas.willRenderCanvases += OnWillRenderCanvases;
            subscribed = true;
        }

        static void OnWillRenderCanvases()
        {
            for (int i = 0; i < Pending.Count; i++)
            {
                PendingLayout pending = Pending[i];
                Component headwearView = pending.Headwear.Target as Component;
                if (headwearView == null)
                {
                    Pending.RemoveAt(i--);
                    continue;
                }

                bool nativeLayout;
                bool applied = TryApply(headwearView, out nativeLayout);
                pending.NativeLayout |= nativeLayout;
                pending.Passes++;
                if (pending.Passes < MaxRenderPasses) continue;

                if (applied && !proofLogged)
                {
                    proofLogged = true;
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB FIRST-OPEN STRUCTURAL PROOF: HeadBand slot16 bounded settle completed; passes="
                        + MaxRenderPasses + "; mode=" + (pending.NativeLayout ? "native-layout" : "fallback-fixed") + ".");
                }

                Pending.RemoveAt(i--);
            }

            if (Pending.Count != 0 || !subscribed) return;
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            subscribed = false;
        }

        static bool TryApply(Component headwearView, out bool nativeLayout)
        {
            nativeLayout = false;
            if (headwearView == null
                || headwearView.transform == null
                || headwearView.transform.parent == null
                || DedicatedSlotPresentationRuntime.EquipmentTabType == null
                || DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField == null
                || DedicatedSlotPresentationRuntime.HeadBandSlotKey == null)
                return false;

            Component equipmentTab = headwearView.GetComponentInParent(DedicatedSlotPresentationRuntime.EquipmentTabType);
            IDictionary slotViews = equipmentTab == null
                ? null
                : DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;
            Component headBandView = slotViews != null && slotViews.Contains(DedicatedSlotPresentationRuntime.HeadBandSlotKey)
                ? slotViews[DedicatedSlotPresentationRuntime.HeadBandSlotKey] as Component
                : null;
            if (headBandView == null || headBandView.transform == null) return false;

            RectTransform headBandRect = headBandView.transform as RectTransform;
            RectTransform headwearRect = headwearView.transform as RectTransform;
            if (headBandRect == null || headwearRect == null) return false;

            Transform parent = headwearView.transform.parent;
            Component nativeLayoutOwner = FindLayoutGroup(parent);
            if (nativeLayoutOwner != null)
            {
                if (!ReferenceEquals(headBandView.transform.parent, parent))
                    headBandView.transform.SetParent(parent, false);
                headBandView.transform.SetSiblingIndex(headwearView.transform.GetSiblingIndex());
                headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, HeadBandCompactHeight);
                headBandView.gameObject.SetActive(true);
                nativeLayout = true;

                if (!nativeInsertionLogged)
                {
                    nativeInsertionLogged = true;
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB HEADBAND NATIVE INSERTION PROOF: slot16 inserted before Headwear under parent="
                        + parent.name + "; layout=" + nativeLayoutOwner.GetType().FullName + ".");
                }
                return true;
            }

            if (!topologyLogged)
            {
                topologyLogged = true;
                LogTopology(equipmentTab, slotViews, headwearView, headBandView);
            }

            // Temporary diagnostic fallback. A successful fallback is deliberately
            // reported as fallback-fixed, never as native-layout success.
            float headwearHeight = Mathf.Max(1f, headwearRect.rect.height);
            float width = Mathf.Max(1f, headwearRect.rect.width);
            if (!ReferenceEquals(headBandView.transform.parent, parent))
                headBandView.transform.SetParent(parent, false);
            headBandView.transform.SetSiblingIndex(headwearView.transform.GetSiblingIndex());
            headBandRect.anchorMin = headwearRect.anchorMin;
            headBandRect.anchorMax = headwearRect.anchorMax;
            headBandRect.pivot = headwearRect.pivot;
            headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            headBandRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, HeadBandCompactHeight);
            headBandRect.anchoredPosition = headwearRect.anchoredPosition
                + new Vector2(0f, (headwearHeight + HeadBandCompactHeight) * 0.5f + HeadBandGap);
            headBandView.gameObject.SetActive(true);
            return true;
        }

        static Component FindLayoutGroup(Transform parent)
        {
            if (parent == null) return null;
            Component[] components = parent.gameObject.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null) continue;
                for (Type type = component.GetType(); type != null; type = type.BaseType)
                {
                    string name = type.FullName ?? type.Name;
                    if (name != null && name.IndexOf("LayoutGroup", StringComparison.OrdinalIgnoreCase) >= 0)
                        return component;
                }
            }
            return null;
        }

        static void LogTopology(Component equipmentTab, IDictionary slotViews, Component headwearView, Component headBandView)
        {
            try
            {
                StringBuilder header = new StringBuilder();
                header.Append("B&A&HB HEADBAND NATIVE TOPOLOGY: no direct LayoutGroup owns Headwear; ");
                header.Append("equipmentTab=").Append(equipmentTab == null ? "<null>" : equipmentTab.gameObject.name);
                header.Append("; headwearParent=").Append(PathOf(headwearView.transform.parent));
                header.Append("; parentComponents=").Append(ComponentTypes(headwearView.transform.parent));
                DedicatedSlotPresentationRuntime.LogInfo?.Invoke(header.ToString());

                if (slotViews == null) return;
                foreach (DictionaryEntry entry in slotViews)
                {
                    Component view = entry.Value as Component;
                    if (view == null || view.transform == null) continue;
                    RectTransform rect = view.transform as RectTransform;
                    StringBuilder line = new StringBuilder();
                    line.Append("B&A&HB HEADBAND NATIVE SLOT: id=").Append(entry.Key == null ? "<null>" : entry.Key.ToString());
                    line.Append("; name=").Append(view.gameObject.name);
                    line.Append("; parent=").Append(PathOf(view.transform.parent));
                    line.Append("; sibling=").Append(view.transform.GetSiblingIndex());
                    if (rect != null)
                    {
                        line.Append("; pos=").Append(rect.anchoredPosition.x.ToString("0.0")).Append(",").Append(rect.anchoredPosition.y.ToString("0.0"));
                        line.Append("; size=").Append(rect.rect.width.ToString("0.0")).Append("x").Append(rect.rect.height.ToString("0.0"));
                        line.Append("; anchorMin=").Append(rect.anchorMin.x.ToString("0.00")).Append(",").Append(rect.anchorMin.y.ToString("0.00"));
                        line.Append("; anchorMax=").Append(rect.anchorMax.x.ToString("0.00")).Append(",").Append(rect.anchorMax.y.ToString("0.00"));
                    }
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(line.ToString());
                }

                Transform ancestor = headwearView.transform.parent;
                for (int depth = 0; ancestor != null && depth < 5; depth++, ancestor = ancestor.parent)
                {
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB HEADBAND NATIVE ANCESTOR: depth=" + depth
                        + "; path=" + PathOf(ancestor)
                        + "; components=" + ComponentTypes(ancestor) + ".");
                }
            }
            catch (Exception exception)
            {
                DedicatedSlotPresentationRuntime.LogWarning?.Invoke(
                    "B&A&HB HeadBand native topology capture failed safely: " + exception.GetType().FullName + ": " + exception.Message);
            }
        }

        static string PathOf(Transform transform)
        {
            if (transform == null) return "<null>";
            List<string> parts = new List<string>();
            for (Transform current = transform; current != null && parts.Count < 8; current = current.parent)
                parts.Add(current.name);
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        static string ComponentTypes(Transform transform)
        {
            if (transform == null) return "<null>";
            Component[] components = transform.gameObject.GetComponents<Component>();
            List<string> names = new List<string>();
            for (int i = 0; i < components.Length; i++)
                if (components[i] != null) names.Add(components[i].GetType().FullName);
            return string.Join(",", names.ToArray());
        }

        internal static void Reset()
        {
            if (subscribed)
            {
                Canvas.willRenderCanvases -= OnWillRenderCanvases;
                subscribed = false;
            }
            Pending.Clear();
            proofLogged = false;
            topologyLogged = false;
            nativeInsertionLogged = false;
        }
    }
}
