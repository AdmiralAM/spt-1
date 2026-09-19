using System;
using System.Collections;
using UnityEngine;

namespace SPTBeltArmbandInventory
{
    internal static class HeadBandRenderSettle
    {
        const float HeadBandCompactHeight = 44f;
        const float Gap = 4f;
        static readonly Vector3[] Corners = new Vector3[4];
        static bool proofLogged;
        static bool failureLogged;

        internal static bool Suppressed;

        internal static void OnHeadwearShown(Component headwearView)
        {
            if (Suppressed || headwearView == null) return;
            TryApplyFlowPlacement(headwearView);
        }

        static bool TryApplyFlowPlacement(Component headwearView)
        {
            if (DedicatedSlotPresentationRuntime.EquipmentTabType == null
                || DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField == null
                || DedicatedSlotPresentationRuntime.HeadBandSlotKey == null
                || DedicatedSlotPresentationRuntime.BeltSlotKey == null
                || DedicatedSlotPresentationRuntime.ArmBandSlotKey == null)
                return false;

            try
            {
                Component equipmentTab = headwearView.GetComponentInParent(DedicatedSlotPresentationRuntime.EquipmentTabType);
                IDictionary slotViews = equipmentTab == null ? null
                    : DedicatedSlotPresentationRuntime.EquipmentTabSlotViewsField.GetValue(equipmentTab) as IDictionary;
                if (slotViews == null) return false;

                RectTransform headBand = RectFor(slotViews, DedicatedSlotPresentationRuntime.HeadBandSlotKey);
                RectTransform belt = RectFor(slotViews, DedicatedSlotPresentationRuntime.BeltSlotKey);
                RectTransform armBand = RectFor(slotViews, DedicatedSlotPresentationRuntime.ArmBandSlotKey);
                if (headBand == null || belt == null || armBand == null) return false;

                RectTransform lowestSpecialSlot = null;
                foreach (DictionaryEntry entry in slotViews)
                {
                    if (entry.Key == null || entry.Value == null
                        || entry.Key.ToString().IndexOf("SpecialSlot", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    RectTransform candidate = (entry.Value as Component)?.transform as RectTransform;
                    if (candidate == null) continue;
                    if (lowestSpecialSlot == null || Bottom(candidate).y < Bottom(lowestSpecialSlot).y)
                        lowestSpecialSlot = candidate;
                }
                if (lowestSpecialSlot == null) return false;

                headBand.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, HeadBandCompactHeight);
                AlignTopLeft(headBand, BottomLeft(lowestSpecialSlot) + Vector3.down * Gap);
                AlignTopLeft(armBand, TopRight(belt) + Vector3.right * Gap);
                headBand.gameObject.SetActive(true);
                armBand.gameObject.SetActive(true);

                if (!proofLogged)
                {
                    proofLogged = true;
                    DedicatedSlotPresentationRuntime.LogInfo?.Invoke(
                        "B&A&HB ACCESSORY FLOW PROOF: HeadBand follows live SpecialSlot bottom; ArmBand follows live Belt right edge; fixed page offsets=False.");
                }
                return true;
            }
            catch (Exception exception)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    DedicatedSlotPresentationRuntime.LogWarning?.Invoke(
                        "B&A&HB accessory flow placement failed safely: "
                        + exception.GetType().FullName + ": " + exception.Message);
                }
                return false;
            }
        }

        static RectTransform RectFor(IDictionary slotViews, object key)
        {
            return slotViews.Contains(key) ? (slotViews[key] as Component)?.transform as RectTransform : null;
        }

        static Vector3 Bottom(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return (Corners[0] + Corners[3]) * 0.5f;
        }

        static Vector3 BottomLeft(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return Corners[0];
        }

        static Vector3 TopRight(RectTransform rect)
        {
            rect.GetWorldCorners(Corners);
            return Corners[2];
        }

        static void AlignTopLeft(RectTransform target, Vector3 destination)
        {
            target.GetWorldCorners(Corners);
            target.position += destination - Corners[1];
        }

        internal static void Reset()
        {
            proofLogged = false;
            failureLogged = false;
        }
    }
}
