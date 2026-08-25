using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal enum BeltSlotPosition
    {
        AbovePockets,
        BelowPockets
    }

    internal static class BeltSlotPlan
    {
        internal const string TacticalVest = "TacticalVest";
        internal const string ArmBand = "ArmBand";
        internal const string Pockets = "Pockets";
        internal const string Backpack = "Backpack";
        internal const string SecuredContainer = "SecuredContainer";
        internal const string Dogtag = "Dogtag";

        internal static string[] Build(IReadOnlyList<string> current, BeltSlotPosition position, bool exposeBelt)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));

            var normalized = new List<string>(current.Count + 1);
            for (int i = 0; i < current.Count; i++)
            {
                string slot = current[i];
                if (string.IsNullOrWhiteSpace(slot) || string.Equals(slot, ArmBand, StringComparison.Ordinal)) continue;
                if (!normalized.Contains(slot)) normalized.Add(slot);
            }

            if (!exposeBelt) return normalized.ToArray();

            int pocketsIndex = normalized.IndexOf(Pockets);
            if (pocketsIndex < 0)
            {
                normalized.Add(ArmBand);
                return normalized.ToArray();
            }

            int insertAt = position == BeltSlotPosition.AbovePockets ? pocketsIndex : pocketsIndex + 1;
            normalized.Insert(insertAt, ArmBand);
            return normalized.ToArray();
        }

        internal static bool IsExpectedContainerPanelOrder(IReadOnlyList<string> slots)
        {
            if (slots == null) return false;
            return Contains(slots, TacticalVest)
                && Contains(slots, Pockets)
                && Contains(slots, Backpack)
                && Contains(slots, SecuredContainer)
                && Contains(slots, Dogtag);
        }

        internal static bool ShouldExposeBelt(bool hasItem, bool isContainer)
        {
            return AccessoryCategoryPolicy.CanExposeContainer(AccessoryCategory.ArmBand, hasItem, isContainer);
        }

        static bool Contains(IReadOnlyList<string> slots, string expected)
        {
            for (int i = 0; i < slots.Count; i++)
                if (string.Equals(slots[i], expected, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
