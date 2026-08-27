using System;

namespace SPTBeltArmbandInventory.Tests
{
    internal static class DedicatedSlotPresentationPolicyRegression
    {
        internal static void Run()
        {
            Assert(DedicatedSlotPresentationPolicy.Caption(RuntimeIdentity.DedicatedBeltWireSlotId, false) == "BELT",
                "Belt EN caption is stable");
            Assert(DedicatedSlotPresentationPolicy.Caption(RuntimeIdentity.DedicatedBeltWireSlotId, true) == "ПОЯС",
                "Belt RU caption is stable");
            Assert(DedicatedSlotPresentationPolicy.Caption(RuntimeIdentity.DedicatedHeadBandWireSlotId, false) == "HEADBAND",
                "HeadBand EN caption is stable");
            Assert(DedicatedSlotPresentationPolicy.Caption(RuntimeIdentity.DedicatedHeadBandWireSlotId, true) == "ПОВЯЗКА НА ГОЛОВУ",
                "HeadBand RU caption is stable");
            Assert(DedicatedSlotPresentationPolicy.Caption("Headwear", true) == null,
                "vanilla slots are never relabeled by dedicated policy");
            Assert(DedicatedSlotPresentationPolicy.LooksRussian("ГОЛОВНОЙ УБОР"),
                "Cyrillic vanilla caption selects Russian dedicated labels");
            Assert(!DedicatedSlotPresentationPolicy.LooksRussian("HEADWEAR"),
                "Latin vanilla caption keeps English dedicated labels");
            Assert(DedicatedSlotPresentationPolicy.ShouldSuppressVanillaHeadwearCompatibility(
                    DedicatedSlotPresentationPolicy.VanillaHeadwearSlotId,
                    RuntimeIdentity.EmergencyHeadBandItemId),
                "exact Emergency HeadBand is rejected from vanilla Headwear");
            Assert(!DedicatedSlotPresentationPolicy.ShouldSuppressVanillaHeadwearCompatibility(
                    DedicatedSlotPresentationPolicy.VanillaHeadwearSlotId,
                    RuntimeIdentity.DedicatedMagazineBeltItemId),
                "Belt is not reclassified by HeadBand guard");
            Assert(!DedicatedSlotPresentationPolicy.ShouldSuppressVanillaHeadwearCompatibility(
                    RuntimeIdentity.DedicatedHeadBandWireSlotId,
                    RuntimeIdentity.EmergencyHeadBandItemId),
                "dedicated HeadBand slot remains available to its exact item");
        }

        static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Dedicated presentation regression failed: " + message);
        }
    }
}
