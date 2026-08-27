using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    /// <summary>
    /// Product-level contract for the two new equipment locations. These are not
    /// aliases for vanilla EquipmentSlot values: Belt and HeadBand are dedicated
    /// B&A&HB locations whose client presentation is injected at fixed anchors.
    /// </summary>
    internal sealed class DedicatedWearableSlotDescriptor
    {
        internal DedicatedWearableSlotDescriptor(
            AccessoryCategory category,
            string slotId,
            string uiAnchor,
            bool insertAfterAnchor,
            string displayName)
        {
            Category = category;
            SlotId = slotId;
            UiAnchor = uiAnchor;
            InsertAfterAnchor = insertAfterAnchor;
            DisplayName = displayName;
        }

        internal AccessoryCategory Category { get; private set; }
        internal string SlotId { get; private set; }
        internal string UiAnchor { get; private set; }
        internal bool InsertAfterAnchor { get; private set; }
        internal string DisplayName { get; private set; }
    }

    internal static class DedicatedWearableSlotContract
    {
        internal const string BeltSlotId = RuntimeIdentity.DedicatedBeltSlotName;
        internal const string HeadBandSlotId = RuntimeIdentity.DedicatedHeadBandSlotName;

        // Product placement requirements. Belt is between Pockets and Backpack,
        // therefore it is inserted immediately after Pockets. HeadBand is above
        // Headwear, therefore it is inserted immediately before Headwear.
        internal static readonly DedicatedWearableSlotDescriptor Belt =
            new DedicatedWearableSlotDescriptor(AccessoryCategory.Belt, BeltSlotId, "Pockets", true, "Belt");

        internal static readonly DedicatedWearableSlotDescriptor HeadBand =
            new DedicatedWearableSlotDescriptor(AccessoryCategory.HeadBand, HeadBandSlotId, "Headwear", false, "HeadBand");

        internal static IEnumerable<DedicatedWearableSlotDescriptor> All
        {
            get
            {
                yield return Belt;
                yield return HeadBand;
            }
        }

        internal static DedicatedWearableSlotDescriptor ForCategory(AccessoryCategory category)
        {
            if (category == AccessoryCategory.Belt) return Belt;
            if (category == AccessoryCategory.HeadBand) return HeadBand;
            return null;
        }

        internal static bool IsDedicatedSlotId(string slotId)
        {
            return string.Equals(slotId, BeltSlotId, StringComparison.Ordinal)
                || string.Equals(slotId, HeadBandSlotId, StringComparison.Ordinal);
        }
    }
}
