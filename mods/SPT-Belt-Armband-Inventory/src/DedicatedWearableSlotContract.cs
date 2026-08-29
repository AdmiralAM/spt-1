using System;
using System.Collections.Generic;

namespace SPTBeltArmbandInventory
{
    internal sealed class DedicatedWearableSlotDescriptor
    {
        internal DedicatedWearableSlotDescriptor(
            AccessoryCategory category,
            string slotId,
            string wireSlotId,
            int equipmentSlotValue,
            string uiAnchor,
            bool insertAfterAnchor,
            string displayName)
        {
            Category = category;
            SlotId = slotId;
            WireSlotId = wireSlotId;
            EquipmentSlotValue = equipmentSlotValue;
            UiAnchor = uiAnchor;
            InsertAfterAnchor = insertAfterAnchor;
            DisplayName = displayName;
        }

        internal AccessoryCategory Category { get; private set; }
        internal string SlotId { get; private set; }
        internal string WireSlotId { get; private set; }
        internal int EquipmentSlotValue { get; private set; }
        internal string UiAnchor { get; private set; }
        internal bool InsertAfterAnchor { get; private set; }
        internal string DisplayName { get; private set; }
    }

    internal static class DedicatedWearableSlotContract
    {
        internal const string BeltSlotId = RuntimeIdentity.DedicatedBeltSlotName;
        internal const string HeadBandSlotId = RuntimeIdentity.DedicatedHeadBandSlotName;

        internal static readonly DedicatedWearableSlotDescriptor Belt =
            new DedicatedWearableSlotDescriptor(
                AccessoryCategory.Belt,
                BeltSlotId,
                RuntimeIdentity.DedicatedBeltWireSlotId,
                RuntimeIdentity.DedicatedBeltEquipmentSlotValue,
                "Pockets",
                true,
                "Belt");

        internal static readonly DedicatedWearableSlotDescriptor HeadBand =
            new DedicatedWearableSlotDescriptor(
                AccessoryCategory.HeadBand,
                HeadBandSlotId,
                RuntimeIdentity.DedicatedHeadBandWireSlotId,
                RuntimeIdentity.DedicatedHeadBandEquipmentSlotValue,
                "Headwear",
                false,
                "HeadBand");

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

        internal static bool IsDedicatedWireSlotId(string slotId)
        {
            return string.Equals(slotId, Belt.WireSlotId, StringComparison.Ordinal)
                || string.Equals(slotId, HeadBand.WireSlotId, StringComparison.Ordinal);
        }
    }
}
