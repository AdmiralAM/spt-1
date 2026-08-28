using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    // The inventory EquipmentTab writes parts of its layout after SlotView.Show.
    // Re-apply the exact HeadBand placement only during the next few canvas render
    // passes, then unsubscribe completely. This is bounded UI lifecycle work, not
    // an Update loop, scene scan, or permanent polling path.
    internal static class HeadBandRenderSettle
    {
        const int MaxRenderPasses = 6;
        const float HeadBandCompactHeight = 44f;
        const float HeadBandGap = 4f;

        sealed class PendingLayout
        {
            internal readonly WeakReference Headwear;
            internal int Passes;

            internal PendingLayout(Component headwear)
            {
                Headwear = new WeakReference(headwear);
            }
        }

        static readonly List<PendingLayout> Pending = new List<PendingLayout>();
        static bool subscribed;
        static bool proofLogged;

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

            if (!TryApply(headwearView)) return;
            Pending.Add(new PendingLayout(headwearView));
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

                bool applied = TryApply(headwearView);
                pending.Passes++;
                if (pending.Passes < MaxRenderPasses) continue;

                if (applied && !proofLogged)
                {
                    proofLogged = true;
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB FIRST-OPEN RENDER PROOF: HeadBand slot16 survived bounded Canvas layout settle without tab switching; passes="
                        + MaxRenderPasses + ".");
                }

                Pending.RemoveAt(i--);
            }

            if (Pending.Count != 0 || !subscribed) return;
            Canvas.willRenderCanvases -= OnWillRenderCanvases;
            subscribed = false;
        }

        static bool TryApply(Component headwearView)
        {
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

            float headwearHeight = Mathf.Max(1f, headwearRect.rect.height);
            float width = Mathf.Max(1f, headwearRect.rect.width);

            if (!ReferenceEquals(headBandView.transform.parent, headwearView.transform.parent))
                headBandView.transform.SetParent(headwearView.transform.parent, false);
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

        internal static void Reset()
        {
            if (subscribed)
            {
                Canvas.willRenderCanvases -= OnWillRenderCanvases;
                subscribed = false;
            }
            Pending.Clear();
            proofLogged = false;
        }
    }
}
