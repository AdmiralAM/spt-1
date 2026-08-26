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
            Require(belt.SlotId == DedicatedWearableSlotContract.BeltSlotId, "Belt slot identity drifted.");
            Require(belt.UiAnchor == "Pockets" && belt.InsertAfterAnchor,
                "Belt must remain immediately after Pockets (between Pockets and Backpack).");

            Require(headBand.Category == AccessoryCategory.HeadBand, "HeadBand descriptor category drifted.");
            Require(headBand.SlotId == DedicatedWearableSlotContract.HeadBandSlotId, "HeadBand slot identity drifted.");
            Require(headBand.UiAnchor == "Headwear" && !headBand.InsertAfterAnchor,
                "HeadBand must remain immediately before Headwear (above the head slot).");

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
