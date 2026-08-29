using System;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class DedicatedWearableSlotContractRegression
    {
        internal static void Run()
        {
            var belt = DedicatedWearableSlotContract.Belt;
            var headBand = DedicatedWearableSlotContract.HeadBand;

            Require(belt.Category == AccessoryCategory.Belt, "Belt descriptor category drifted.");
            Require(belt.SlotId == DedicatedWearableSlotContract.BeltSlotId, "Belt semantic slot identity drifted.");
            Require(belt.WireSlotId == "15" && belt.EquipmentSlotValue == 15,
                "Belt must remain pseudo EquipmentSlot 15 on the EFT wire.");
            Require(belt.UiAnchor == "Pockets" && belt.InsertAfterAnchor,
                "Belt must remain immediately after Pockets (between Pockets and Backpack).");

            Require(headBand.Category == AccessoryCategory.HeadBand, "HeadBand descriptor category drifted.");
            Require(headBand.SlotId == DedicatedWearableSlotContract.HeadBandSlotId, "HeadBand semantic slot identity drifted.");
            Require(headBand.WireSlotId == "16" && headBand.EquipmentSlotValue == 16,
                "HeadBand must remain pseudo EquipmentSlot 16 on the EFT wire.");
            Require(headBand.UiAnchor == "Headwear" && !headBand.InsertAfterAnchor,
                "HeadBand must remain immediately before Headwear (above the head slot).");

            Require(belt.EquipmentSlotValue != headBand.EquipmentSlotValue,
                "Dedicated equipment pseudo-slot values must not collide.");
            Require(DedicatedWearableSlotContract.IsDedicatedWireSlotId("15")
                    && DedicatedWearableSlotContract.IsDedicatedWireSlotId("16")
                    && !DedicatedWearableSlotContract.IsDedicatedWireSlotId("14"),
                "Dedicated wire-slot recognition drifted into vanilla ArmBand=14.");

            Require(!string.Equals(belt.SlotId, "ArmBand", StringComparison.Ordinal),
                "Dedicated Belt must never alias vanilla ArmBand.");
            Require(!string.Equals(headBand.SlotId, "Headwear", StringComparison.Ordinal)
                    && !string.Equals(headBand.SlotId, "FaceCover", StringComparison.Ordinal)
                    && !string.Equals(headBand.SlotId, "Earpiece", StringComparison.Ordinal)
                    && !string.Equals(headBand.SlotId, "ArmBand", StringComparison.Ordinal),
                "Dedicated HeadBand must never alias a vanilla head/arm slot.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
