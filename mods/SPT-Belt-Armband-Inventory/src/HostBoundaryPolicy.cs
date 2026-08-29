using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal static class HostBoundaryPolicy
    {
        // ArmBand is the only wearable whose authority is a vanilla equipment slot.
        // Belt and HeadBand are dedicated B&A&HB locations and must never be silently
        // rebound to a similarly named EFT enum/member discovered at runtime.
        internal static string FindExactHost(AccessoryCategory category, IEnumerable<string> slotNames)
        {
            if (category == AccessoryCategory.Belt)
                return DedicatedWearableSlotContract.BeltSlotId;
            if (category == AccessoryCategory.HeadBand)
                return DedicatedWearableSlotContract.HeadBandSlotId;
            if (category != AccessoryCategory.ArmBand || slotNames == null)
                return null;

            foreach (string name in slotNames)
            {
                if (string.Equals(name, BeltSlotPlan.ArmBand, StringComparison.Ordinal))
                    return name;
            }
            return null;
        }

        internal static bool IsSafeExactHost(AccessoryCategory category, string slotName)
        {
            if (category == AccessoryCategory.Belt)
                return string.Equals(slotName, DedicatedWearableSlotContract.BeltSlotId, StringComparison.Ordinal);
            if (category == AccessoryCategory.HeadBand)
                return string.Equals(slotName, DedicatedWearableSlotContract.HeadBandSlotId, StringComparison.Ordinal);
            return category == AccessoryCategory.ArmBand
                && string.Equals(slotName, BeltSlotPlan.ArmBand, StringComparison.Ordinal);
        }

        internal static bool IsForbiddenVanillaSubstitute(AccessoryCategory category, string slotName)
        {
            if (string.IsNullOrEmpty(slotName)) return false;
            if (category == AccessoryCategory.Belt)
                return string.Equals(slotName, BeltSlotPlan.ArmBand, StringComparison.Ordinal)
                    || string.Equals(slotName, "Pockets", StringComparison.Ordinal)
                    || string.Equals(slotName, "Backpack", StringComparison.Ordinal);
            if (category == AccessoryCategory.HeadBand)
                return string.Equals(slotName, "Headwear", StringComparison.Ordinal)
                    || string.Equals(slotName, "FaceCover", StringComparison.Ordinal)
                    || string.Equals(slotName, "Earpiece", StringComparison.Ordinal)
                    || string.Equals(slotName, BeltSlotPlan.ArmBand, StringComparison.Ordinal);
            return false;
        }
    }
}
